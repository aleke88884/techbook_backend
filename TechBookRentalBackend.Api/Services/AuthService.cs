using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TechBookRentalBackend.Api.Data;
using TechBookRentalBackend.Api.Data.Entities;
using TechBookRentalBackend.Api.Models;

namespace TechBookRentalBackend.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext context,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger)
    {
        _context = context;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check if user exists
        if (await _context.Users.AnyAsync(u => u.Email.ToLower() == request.Email.ToLower()))
        {
            return AuthResponse.Fail("Бұл email тіркелген / Этот email уже зарегистрирован");
        }

        // Create user
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Phone = request.Phone,
            Role = "User",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);

        await _context.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email}", user.Email);

        return AuthResponse.Ok(
            accessToken,
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            MapToDto(user));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return AuthResponse.Fail("Email немесе құпия сөз қате / Неверный email или пароль");
        }

        if (!user.IsActive)
        {
            return AuthResponse.Fail("Аккаунт белсенді емес / Аккаунт неактивен");
        }

        // Generate tokens
        var accessToken = GenerateAccessToken(user);
        var refreshToken = await GenerateRefreshTokenAsync(user.Id);

        await _context.SaveChangesAsync();

        _logger.LogInformation("User logged in: {Email}", user.Email);

        return AuthResponse.Ok(
            accessToken,
            refreshToken.Token,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            MapToDto(user));
    }

    public async Task<AuthResponse> RefreshTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Token == token);

        if (refreshToken == null)
        {
            return AuthResponse.Fail("Жарамсыз токен / Недействительный токен");
        }

        if (!refreshToken.IsActive)
        {
            return AuthResponse.Fail("Токен мерзімі өтті / Токен истек");
        }

        var user = refreshToken.User;

        if (!user.IsActive)
        {
            return AuthResponse.Fail("Аккаунт белсенді емес / Аккаунт неактивен");
        }

        // Revoke old token
        refreshToken.RevokedAt = DateTime.UtcNow;

        // Generate new tokens
        var newAccessToken = GenerateAccessToken(user);
        var newRefreshToken = await GenerateRefreshTokenAsync(user.Id);

        refreshToken.ReplacedByToken = newRefreshToken.Token;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Token refreshed for user: {Email}", user.Email);

        return AuthResponse.Ok(
            newAccessToken,
            newRefreshToken.Token,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            MapToDto(user));
    }

    public async Task<bool> RevokeTokenAsync(string token)
    {
        var refreshToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token);

        if (refreshToken == null || !refreshToken.IsActive)
        {
            return false;
        }

        refreshToken.RevokedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Token revoked");

        return true;
    }

    private string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<RefreshToken> GenerateRefreshTokenAsync(Guid userId)
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Convert.ToBase64String(randomBytes),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays),
            CreatedAt = DateTime.UtcNow,
            UserId = userId
        };

        _context.RefreshTokens.Add(refreshToken);

        // Remove old refresh tokens for this user
        var oldTokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && (r.RevokedAt != null || r.ExpiresAt < DateTime.UtcNow))
            .ToListAsync();

        _context.RefreshTokens.RemoveRange(oldTokens);

        return refreshToken;
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.Phone,
        Role = user.Role
    };
}
