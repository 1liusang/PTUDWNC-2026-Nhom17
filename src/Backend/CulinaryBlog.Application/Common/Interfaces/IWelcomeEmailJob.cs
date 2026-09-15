namespace CulinaryBlog.Application.Common.Interfaces;

/// <summary>Hangfire job contract (FR-JOB-001). Implemented in Infrastructure, enqueued from Application.</summary>
public interface IWelcomeEmailJob
{
    Task ExecuteAsync(string userId, CancellationToken cancellationToken = default);
}
