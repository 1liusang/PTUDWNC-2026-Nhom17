using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;
using CulinaryBlog.Application.Abstractions;
using CulinaryBlog.Application.Features.Auth;
using CulinaryBlog.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CulinaryBlog.IntegrationTests.Auth.AuthApi;

namespace CulinaryBlog.IntegrationTests.Auth;

/// <summary>Việc 1.21: đã cấu hình Client ID thì id_token giả bị từ chối 401, không cần gọi tới Google.</summary>
public sealed class GoogleLoginEndpointsTests(GoogleLoginEndpointsTests.GoogleConfiguredFactory factory)
    : IClassFixture<GoogleLoginEndpointsTests.GoogleConfiguredFactory>
{
    [Theory]
    [InlineData("fake")]
    [InlineData("not.a.jwt")]
    [InlineData("%%%.%%%.%%%")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiIxIn0.")]
    public async Task Google_ForgedToken_Returns401(string idToken)
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"{BasePath}/google", new { idToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AuthErrorCodes.GoogleTokenInvalid, await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task Google_EmptyToken_Returns422()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"{BasePath}/google", new { idToken = string.Empty });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    public sealed class GoogleConfiguredFactory : ApiFactory
    {
        protected override IDictionary<string, string?> Settings
        {
            get
            {
                var settings = base.Settings;
                settings["Authentication:Google:ClientId"] = "integration-tests.apps.googleusercontent.com";
                return settings;
            }
        }

        // Không có database nên Hangfire chưa được đăng ký; token giả bị từ chối trước khi cần tới job.
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services => services.TryAddSingleton<IBackgroundJobService, NoopBackgroundJobService>());
        }

        private sealed class NoopBackgroundJobService : IBackgroundJobService
        {
            public string Enqueue<TJob>(Expression<Func<TJob, Task>> methodCall) => string.Empty;

            public string Schedule<TJob>(Expression<Func<TJob, Task>> methodCall, TimeSpan delay) => string.Empty;

            public void AddOrUpdateRecurring<TJob>(string recurringJobId, Expression<Func<TJob, Task>> methodCall, string cronExpression)
            {
            }
        }
    }
}
