using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CulinaryBlog.Infrastructure.Auth;

/// <summary>Khóa công khai Google dùng để ký id_token (JWKS).</summary>
public interface IGoogleSigningKeySource
{
    /// <param name="forceRefresh">Bỏ qua cache, dùng khi token có <c>kid</c> chưa thấy (Google vừa xoay khóa).</param>
    Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken cancellationToken);
}

/// <summary>
/// Tải và cache JWKS của Google. Kết nối ưu tiên IPv4, mỗi địa chỉ chờ tối đa vài giây: ở mạng có IPv6
/// "hỏng" (DNS trả AAAA nhưng không đi được), HttpClient mặc định của .NET thử IPv6 trước và treo ~20 giây.
/// </summary>
public sealed class GoogleSigningKeys : IGoogleSigningKeySource, IDisposable
{
    public const string JwksUrl = "https://www.googleapis.com/oauth2/v3/certs";

    private static readonly TimeSpan ConnectTimeoutPerAddress = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultCacheLifetime = TimeSpan.FromHours(1);

    // Không tải lại quá dày khi gặp token có kid lạ (token giả mạo cũng có thể có kid lạ).
    private static readonly TimeSpan MinRefreshInterval = TimeSpan.FromMinutes(1);

    private readonly HttpClient _http;
    private readonly TimeProvider _time;
    private readonly ILogger<GoogleSigningKeys> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyCollection<SecurityKey> _keys = [];
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;
    private DateTimeOffset _fetchedAt = DateTimeOffset.MinValue;

    public GoogleSigningKeys(TimeProvider time, ILogger<GoogleSigningKeys> logger)
    {
        _time = time;
        _logger = logger;
        _http = new HttpClient(new SocketsHttpHandler { ConnectCallback = ConnectPreferringIPv4Async })
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
    }

    public async Task<IReadOnlyCollection<SecurityKey>> GetKeysAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (!NeedsRefresh(now, forceRefresh))
        {
            return _keys;
        }

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            now = _time.GetUtcNow();
            if (!NeedsRefresh(now, forceRefresh))
            {
                return _keys;
            }

            using var response = await _http.GetAsync(JwksUrl, cancellationToken);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            _keys = [.. new JsonWebKeySet(json).GetSigningKeys()];
            _fetchedAt = now;
            _expiresAt = now + (response.Headers.CacheControl?.MaxAge ?? DefaultCacheLifetime);
            _logger.LogInformation("Fetched {Count} Google signing keys, cached until {ExpiresAt}", _keys.Count, _expiresAt);
            return _keys;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose()
    {
        _http.Dispose();
        _refreshLock.Dispose();
    }

    private bool NeedsRefresh(DateTimeOffset now, bool forceRefresh) =>
        _keys.Count == 0 || now >= _expiresAt || (forceRefresh && now - _fetchedAt >= MinRefreshInterval);

    private static async ValueTask<Stream> ConnectPreferringIPv4Async(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);
        Exception? lastError = null;

        foreach (var address in addresses.OrderBy(address => address.AddressFamily == AddressFamily.InterNetwork ? 0 : 1))
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(ConnectTimeoutPerAddress);
                await socket.ConnectAsync(address, context.DnsEndPoint.Port, timeout.Token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception exception) when (exception is SocketException or OperationCanceledException
                && !cancellationToken.IsCancellationRequested)
            {
                socket.Dispose();
                lastError = exception;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException($"Could not connect to {context.DnsEndPoint.Host}.", lastError);
    }
}
