using System.Text.Json.Serialization;

namespace KeycloakWebAPI.Models;

public class KeycloakUser
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; }
    public bool EmailVerified { get; set; }
    public long? CreatedTimestamp { get; set; }
    public Dictionary<string, List<string>>? Attributes { get; set; }

    [JsonIgnore]
    public List<string> Roles { get; set; } = new();
}

public class CreateKeycloakUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Password { get; set; }
    public bool? TemporaryPassword { get; set; }
    public bool? Enabled { get; set; }
    public bool? EmailVerified { get; set; }
    public List<string>? Roles { get; set; }
    public Dictionary<string, List<string>>? Attributes { get; set; }
}

public class UpdateKeycloakUserRequest
{
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Password { get; set; }
    public bool? TemporaryPassword { get; set; }
    public bool? Enabled { get; set; }
    public bool? EmailVerified { get; set; }
    public List<string>? Roles { get; set; }
    public Dictionary<string, List<string>>? Attributes { get; set; }
}
