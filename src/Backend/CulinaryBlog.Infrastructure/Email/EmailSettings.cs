namespace CulinaryBlog.Infrastructure.Email;

public class EmailSettings
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = null!;
    public int Port { get; set; }
    public string SenderEmail { get; set; } = null!;
    public string SenderName { get; set; } = null!;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
