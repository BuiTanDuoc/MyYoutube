using Microsoft.AspNetCore.Identity;
using MiniTube.Application.Common;

namespace MiniTube.Infrastructure.Services;

internal static class IdentityResultExtensions
{
    public static void ThrowIfFailed(this IdentityResult result, string message = "Validation failed")
    {
        if (result.Succeeded) return;

        var errors = result.Errors
            .GroupBy(e => MapField(e.Code))
            .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray());

        throw AppException.BadRequest(message, errors);
    }

    private static string MapField(string code) => code switch
    {
        "DuplicateUserName" or "InvalidUserName" => "UserName",
        "DuplicateEmail" or "InvalidEmail" => "Email",
        "PasswordMismatch" => "CurrentPassword",
        var c when c.StartsWith("Password") => "Password",
        _ => "General"
    };
}
