namespace MiniTube.Application.Common;

public interface IAppEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}
