using CulinaryBlog.Application.Common.Interfaces;
using CulinaryBlog.Domain.Entities;
using Hangfire;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace CulinaryBlog.Infrastructure.Jobs;

/// <summary>FR-JOB-001: gửi email chào mừng bất đồng bộ sau khi đăng ký thành công.</summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 60, 300, 1800 })]
public class WelcomeEmailJob : IWelcomeEmailJob
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<WelcomeEmailJob> _logger;

    public WelcomeEmailJob(UserManager<ApplicationUser> userManager, IEmailSender emailSender, ILogger<WelcomeEmailJob> logger)
    {
        _userManager = userManager;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async Task ExecuteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            _logger.LogWarning("WelcomeEmailJob: user {UserId} không còn tồn tại, bỏ qua.", userId);
            return;
        }

        var subject = "Chào mừng bạn đến với Culinary Blog!";
        var htmlBody = $"""
            <h2>Xin chào {user.DisplayName},</h2>
            <p>Cảm ơn bạn đã đăng ký tài khoản tại Culinary Blog. Bạn có thể bắt đầu khám phá và chia sẻ công thức nấu ăn ngay bây giờ.</p>
            <p>Trân trọng,<br/>Culinary Blog Team</p>
            """;

        await _emailSender.SendAsync(user.Email!, subject, htmlBody, cancellationToken);
    }
}
