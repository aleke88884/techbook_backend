using System.ComponentModel.DataAnnotations;

namespace TechBookRentalBackend.Api.Models;

// JWT Configuration
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    public int RefreshTokenExpirationDays { get; set; } = 7;
}

// Requests
public class RegisterRequest
{
    [Required(ErrorMessage = "Email міндетті / Email обязателен")]
    [EmailAddress(ErrorMessage = "Email форматы дұрыс емес / Неверный формат email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Құпия сөз міндетті / Пароль обязателен")]
    [MinLength(6, ErrorMessage = "Құпия сөз кемінде 6 таңба / Пароль минимум 6 символов")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Аты міндетті / Имя обязательно")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Тегі міндетті / Фамилия обязательна")]
    public string LastName { get; set; } = string.Empty;

    public string? Phone { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email міндетті / Email обязателен")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Құпия сөз міндетті / Пароль обязателен")]
    public string Password { get; set; } = string.Empty;
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

// Responses
public class AuthResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpires { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }

    public static AuthResponse Ok(string accessToken, string refreshToken, DateTime expires, UserDto user) => new()
    {
        Success = true,
        AccessToken = accessToken,
        RefreshToken = refreshToken,
        AccessTokenExpires = expires,
        User = user
    };

    public static AuthResponse Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Role { get; set; } = string.Empty;
}
