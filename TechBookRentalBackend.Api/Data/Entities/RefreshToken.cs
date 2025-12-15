using System.ComponentModel.DataAnnotations;

namespace TechBookRentalBackend.Api.Data.Entities;

public class RefreshToken
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    public bool IsRevoked => RevokedAt != null;

    public bool IsActive => !IsRevoked && !IsExpired;

    // Foreign key
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
}
