using CulinaryBlog.Application.Abstractions;
using CulinaryBlog.Application.Features.Auth.RefreshTokenCleanup;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Auth;

/// <summary>Đăng ký job định kỳ của module Auth khi API khởi động (việc 1.20).</summary>
/// <remarks><paramref name="jobs"/> là null khi không có connection string (Hangfire không được đăng ký, ví dụ test không DB).</remarks>
internal sealed class AuthRecurringJobsRegistration(
    ILogger<AuthRecurringJobsRegistration> logger,
    IBackgroundJobService? jobs = null) : IHostedService
{
    public const string RefreshTokenCleanupJobId = "auth-refresh-token-cleanup";

    // Theo ADR-014: dọn refresh token hết hạn lúc 01:00 UTC mỗi ngày.
    private const string DailyAt1AmUtc = "0 1 * * *";

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (jobs is null)
        {
            return Task.CompletedTask;
        }

        try
        {
            jobs.AddOrUpdateRecurring<RefreshTokenCleanupJob>(
                RefreshTokenCleanupJobId,
                job => job.ExecuteAsync(CancellationToken.None),
                DailyAt1AmUtc);
        }
        catch (Exception exception)
        {
            // Giống DatabaseInitializer: DB chưa chạy thì chỉ ghi log, API vẫn lên.
            logger.LogError(exception, "Could not register recurring job {JobId}", RefreshTokenCleanupJobId);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
