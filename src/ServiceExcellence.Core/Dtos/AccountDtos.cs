using System.ComponentModel.DataAnnotations;
using ServiceExcellence.Core.Common;

namespace ServiceExcellence.Core.Dtos;

public class LoginRequest
{
    [Required, StringLength(60)]
    public string Username { get; set; } = "";

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public int? DealerId { get; set; }
}

public class ChangePasswordRequest
{
    [Required, DataType(DataType.Password), Display(Name = "Current password")]
    public string CurrentPassword { get; set; } = "";

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password), Display(Name = "New password")]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}

// ---------------------------------------------------------------------------
// Users
// ---------------------------------------------------------------------------

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";
    public int? DealerId { get; set; }
    public string? DealerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Internal record used for authentication only; never returned by the API.</summary>
public class UserCredentials
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
    public int? DealerId { get; set; }
    public bool IsActive { get; set; }
}

public class UserSaveRequest
{
    [Required, StringLength(60, MinimumLength = 3)]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use letters, digits, '.', '_' or '-' only.")]
    public string Username { get; set; } = "";

    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [Required]
    public string Role { get; set; } = "";

    [Display(Name = "Dealer")]
    public int? DealerId { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    /// <summary>Required when creating a user; optional on update (blank keeps the current password).</summary>
    [StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string? Password { get; set; }
}

public class UserQuery : PagedQuery
{
    public string? Role { get; set; }
}

// ---------------------------------------------------------------------------
// Dealers
// ---------------------------------------------------------------------------

public class DealerDto
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ContactPhone { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DealerSaveRequest
{
    [Required, StringLength(30)]
    public string Code { get; set; } = "";

    [Required, StringLength(200)]
    public string Name { get; set; } = "";

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(30), Display(Name = "Contact phone")]
    public string? ContactPhone { get; set; }

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

// ---------------------------------------------------------------------------
// Dashboard & audit
// ---------------------------------------------------------------------------

public class DashboardDto
{
    public int Divisions { get; set; }
    public int Models { get; set; }
    public int Variants { get; set; }
    public int Dealers { get; set; }
    public Dictionary<string, int> SopsByStatus { get; set; } = new();
    public Dictionary<string, int> PublishedByActivity { get; set; } = new();
    public IReadOnlyList<SopListItemDto> RecentSops { get; set; } = Array.Empty<SopListItemDto>();
}

public class AuditLogDto
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";
    public int EntityId { get; set; }
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditQuery : PagedQuery
{
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
