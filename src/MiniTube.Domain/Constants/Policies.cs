namespace MiniTube.Domain.Constants;

public static class Policies
{
    /// <summary>Requires the "email_verified=true" claim. Use it on endpoints like video upload.</summary>
    public const string VerifiedEmail = "VerifiedEmail";
}
