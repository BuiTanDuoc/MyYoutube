using System.ComponentModel.DataAnnotations;
using MiniTube.Application.Users;

namespace MiniTube.Application.Auth;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, RegularExpression(@"^[a-zA-Z0-9_.]{3,30}$",
        ErrorMessage = "UserName must be 3-30 characters: letters, digits, '_' or '.'")]
    public string UserName { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(100)]
    public string Password { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string DisplayName { get; set; } = string.Empty;
}

public class LoginRequest
{
    /// <summary>Email or username.</summary>
    [Required]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}
