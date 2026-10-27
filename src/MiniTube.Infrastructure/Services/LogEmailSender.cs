using Microsoft.Extensions.Logging;
using MiniTube.Application.Common;

namespace MiniTube.Infrastructure.Services;

/// <summary>Used when no SMTP host is configured: prints the email so you can copy the link in dev.</summary>
public class LogEmailSender : IAppEmailSender
{
    private readonly ILogger<LogEmailSender> _logger;

    public LogEmailSender(ILogger<LogEmailSender> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogWarning("[DEV EMAIL - SMTP not configured] To: {To} | Subject: {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
