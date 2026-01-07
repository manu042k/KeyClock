using System.ComponentModel.DataAnnotations;

namespace KeycloakWebAPI.DTOs;

public class CreateUserDto
{
    [Required(ErrorMessage = "Username is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 100 characters")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required")]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    public bool TemporaryPassword { get; set; } = false;

    public List<string>? Roles { get; set; }

    public Dictionary<string, List<string>>? CustomAttributes { get; set; }
}

public class UpdateUserDto
{
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string? Email { get; set; }

    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string? Password { get; set; }

    public bool? TemporaryPassword { get; set; }

    public bool? Enabled { get; set; }

    public bool? EmailVerified { get; set; }

    public List<string>? Roles { get; set; }

    public Dictionary<string, List<string>>? CustomAttributes { get; set; }
}

public class UserResponseDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime? CreatedAt { get; set; }
    public List<string> Roles { get; set; } = new();
    public Dictionary<string, List<string>>? CustomAttributes { get; set; }
}
