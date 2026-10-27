using System.Net;

namespace MiniTube.Infrastructure.Services;

internal static class EmailTemplates
{
    public static (string Subject, string Body) ConfirmEmail(string lang, string displayName, string link)
    {
        var name = WebUtility.HtmlEncode(displayName);
        return lang == "en"
            ? ("Confirm your MiniTube email",
               Layout($"Hi {name},", "Please confirm your email address to unlock uploading videos.", "Confirm email", link,
                      "This link is valid for 2 hours. If you didn't create an account, you can ignore this email."))
            : ("Xác nhận email MiniTube của bạn",
               Layout($"Xin chào {name},", "Vui lòng xác nhận địa chỉ email để có thể tải video lên.", "Xác nhận email", link,
                      "Liên kết có hiệu lực trong 2 giờ. Nếu bạn không tạo tài khoản, hãy bỏ qua email này."));
    }

    public static (string Subject, string Body) ResetPassword(string lang, string displayName, string link)
    {
        var name = WebUtility.HtmlEncode(displayName);
        return lang == "en"
            ? ("Reset your MiniTube password",
               Layout($"Hi {name},", "We received a request to reset your password.", "Reset password", link,
                      "This link is valid for 2 hours. If you didn't request this, you can safely ignore this email."))
            : ("Đặt lại mật khẩu MiniTube",
               Layout($"Xin chào {name},", "Chúng tôi nhận được yêu cầu đặt lại mật khẩu của bạn.", "Đặt lại mật khẩu", link,
                      "Liên kết có hiệu lực trong 2 giờ. Nếu bạn không yêu cầu, hãy bỏ qua email này."));
    }

    private static string Layout(string greeting, string message, string buttonText, string link, string footer)
    {
        var href = WebUtility.HtmlEncode(link);
        return $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:480px;margin:auto;padding:24px;color:#0f0f0f">
          <h2 style="margin:0 0 16px;color:#ff0000">MiniTube</h2>
          <p>{greeting}</p>
          <p>{message}</p>
          <p style="margin:24px 0">
            <a href="{href}" style="background:#ff0000;color:#fff;text-decoration:none;padding:12px 20px;border-radius:20px;font-weight:bold">{buttonText}</a>
          </p>
          <p style="font-size:12px;color:#606060">{footer}</p>
          <p style="font-size:12px;color:#606060;word-break:break-all">{href}</p>
        </div>
        """;
    }
}
