using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MiniTube.Application.Common;
using MiniTube.Infrastructure.Options;

namespace MiniTube.Infrastructure.Services;

public class SmtpEmailSender : IAppEmailSender
{
    private readonly EmailOptions _o;

    public SmtpEmailSender(IOptions<EmailOptions> options) => _o = options.Value;

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(_o.FromName, _o.FromAddress));
        msg.To.Add(MailboxAddress.Parse(to));
        msg.Subject = subject;
        msg.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_o.Host, _o.Port,
            _o.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);

        if (!string.IsNullOrEmpty(_o.UserName))
            await client.AuthenticateAsync(_o.UserName, _o.Password, ct);

        await client.SendAsync(msg, ct);
        await client.DisconnectAsync(true, ct);
    }
}
