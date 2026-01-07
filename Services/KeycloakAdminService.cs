using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KeycloakWebAPI.Models;

namespace KeycloakWebAPI.Services;

public class KeycloakAdminService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KeycloakAdminService> _logger;
    private readonly string _realm;
    private readonly string _adminUrl;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public KeycloakAdminService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<KeycloakAdminService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _realm = configuration["Keycloak:Realm"] ?? "webapi-realm";
        _adminUrl = configuration["Keycloak:AdminUrl"] ?? "http://localhost:8080";
    }

    private async Task<string> GetAdminAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        var clientId = _configuration["Keycloak:AdminClientId"];
        var clientSecret = _configuration["Keycloak:AdminClientSecret"];
        var tokenUrl = $"{_adminUrl}/realms/{_realm}/protocol/openid-connect/token";

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId!),
            new KeyValuePair<string, string>("client_secret", clientSecret!)
        });

        var response = await _httpClient.PostAsync(tokenUrl, content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(result);

        _accessToken = tokenResponse?.AccessToken ?? throw new Exception("Failed to get access token");
        _tokenExpiry = DateTime.UtcNow.AddSeconds((tokenResponse.ExpiresIn ?? 300) - 30);

        return _accessToken;
    }

    private async Task<HttpRequestMessage> CreateAuthorizedRequestAsync(HttpMethod method, string url)
    {
        var token = await GetAdminAccessTokenAsync();
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    public async Task<List<KeycloakUser>> GetUsersAsync(bool? enabled = null, string? search = null)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users";
        var queryParams = new List<string>();

        if (enabled.HasValue)
            queryParams.Add($"enabled={enabled.Value.ToString().ToLower()}");

        if (!string.IsNullOrWhiteSpace(search))
            queryParams.Add($"search={Uri.EscapeDataString(search)}");

        if (queryParams.Any())
            url += "?" + string.Join("&", queryParams);

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var users = JsonSerializer.Deserialize<List<KeycloakUser>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<KeycloakUser>();

        // Enrich with roles
        foreach (var user in users)
        {
            user.Roles = await GetUserRolesAsync(user.Id!);
        }

        return users;
    }

    public async Task<KeycloakUser?> GetUserByIdAsync(string userId)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}";
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return null;

        var content = await response.Content.ReadAsStringAsync();
        var user = JsonSerializer.Deserialize<KeycloakUser>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (user != null)
        {
            user.Roles = await GetUserRolesAsync(userId);
        }

        return user;
    }

    public async Task<KeycloakUser> CreateUserAsync(CreateKeycloakUserRequest request)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users";

        var userRepresentation = new
        {
            username = request.Username,
            email = request.Email,
            firstName = request.FirstName,
            lastName = request.LastName,
            enabled = request.Enabled ?? true,
            emailVerified = request.EmailVerified ?? false,
            credentials = string.IsNullOrWhiteSpace(request.Password) ? null : new[]
            {
                new
                {
                    type = "password",
                    value = request.Password,
                    temporary = request.TemporaryPassword ?? false
                }
            },
            attributes = request.Attributes
        };

        var json = JsonSerializer.Serialize(userRepresentation);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = await CreateAuthorizedRequestAsync(HttpMethod.Post, url);
        httpRequest.Content = httpContent;

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        // Get the created user ID from Location header
        var location = response.Headers.Location?.ToString();
        var userId = location?.Split('/').Last();

        if (string.IsNullOrEmpty(userId))
            throw new Exception("Failed to retrieve created user ID");

        // Assign roles if specified
        if (request.Roles != null && request.Roles.Any())
        {
            await AssignRolesToUserAsync(userId, request.Roles);
        }

        var createdUser = await GetUserByIdAsync(userId);
        return createdUser ?? throw new Exception("Failed to retrieve created user");
    }

    public async Task<KeycloakUser> UpdateUserAsync(string userId, UpdateKeycloakUserRequest request)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}";

        var updateData = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(request.Email))
            updateData["email"] = request.Email;

        if (!string.IsNullOrWhiteSpace(request.FirstName))
            updateData["firstName"] = request.FirstName;

        if (!string.IsNullOrWhiteSpace(request.LastName))
            updateData["lastName"] = request.LastName;

        if (request.Enabled.HasValue)
            updateData["enabled"] = request.Enabled.Value;

        if (request.EmailVerified.HasValue)
            updateData["emailVerified"] = request.EmailVerified.Value;

        if (request.Attributes != null)
            updateData["attributes"] = request.Attributes;

        var json = JsonSerializer.Serialize(updateData);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var httpRequest = await CreateAuthorizedRequestAsync(HttpMethod.Put, url);
        httpRequest.Content = httpContent;

        var response = await _httpClient.SendAsync(httpRequest);
        response.EnsureSuccessStatusCode();

        // Update password if provided
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            await ResetUserPasswordAsync(userId, request.Password, request.TemporaryPassword ?? false);
        }

        // Update roles if provided
        if (request.Roles != null)
        {
            await UpdateUserRolesAsync(userId, request.Roles);
        }

        var updatedUser = await GetUserByIdAsync(userId);
        return updatedUser ?? throw new Exception("Failed to retrieve updated user");
    }

    public async Task DeleteUserAsync(string userId)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}";
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, url);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetUserPasswordAsync(string userId, string newPassword, bool temporary = false)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}/reset-password";

        var credential = new
        {
            type = "password",
            value = newPassword,
            temporary
        };

        var json = JsonSerializer.Serialize(credential);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Put, url);
        request.Content = httpContent;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task<List<string>> GetUserRolesAsync(string userId)
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}/role-mappings/realm";
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
            return new List<string>();

        var content = await response.Content.ReadAsStringAsync();
        var roles = JsonSerializer.Deserialize<List<RoleRepresentation>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<RoleRepresentation>();

        return roles.Select(r => r.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList();
    }

    private async Task AssignRolesToUserAsync(string userId, List<string> roleNames)
    {
        var allRoles = await GetRealmRolesAsync();
        var rolesToAssign = allRoles.Where(r => roleNames.Contains(r.Name ?? "")).ToList();

        if (!rolesToAssign.Any())
            return;

        var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}/role-mappings/realm";
        var json = JsonSerializer.Serialize(rolesToAssign);
        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

        var request = await CreateAuthorizedRequestAsync(HttpMethod.Post, url);
        request.Content = httpContent;

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }

    private async Task UpdateUserRolesAsync(string userId, List<string> newRoleNames)
    {
        // Get current roles
        var currentRoles = await GetUserRolesAsync(userId);

        // Get all realm roles
        var allRoles = await GetRealmRolesAsync();

        // Find roles to remove (current roles not in new list)
        var rolesToRemove = allRoles.Where(r =>
            currentRoles.Contains(r.Name ?? "") &&
            !newRoleNames.Contains(r.Name ?? "")
        ).ToList();

        // Find roles to add (new roles not in current list)
        var rolesToAdd = allRoles.Where(r =>
            newRoleNames.Contains(r.Name ?? "") &&
            !currentRoles.Contains(r.Name ?? "")
        ).ToList();

        // Remove old roles
        if (rolesToRemove.Any())
        {
            var url = $"{_adminUrl}/admin/realms/{_realm}/users/{userId}/role-mappings/realm";
            var json = JsonSerializer.Serialize(rolesToRemove);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var request = await CreateAuthorizedRequestAsync(HttpMethod.Delete, url);
            request.Content = httpContent;

            await _httpClient.SendAsync(request);
        }

        // Add new roles
        if (rolesToAdd.Any())
        {
            await AssignRolesToUserAsync(userId, rolesToAdd.Select(r => r.Name ?? "").ToList());
        }
    }

    private async Task<List<RoleRepresentation>> GetRealmRolesAsync()
    {
        var url = $"{_adminUrl}/admin/realms/{_realm}/roles";
        var request = await CreateAuthorizedRequestAsync(HttpMethod.Get, url);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<RoleRepresentation>>(content, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<RoleRepresentation>();
    }
}

// Helper classes for JSON serialization
public class TokenResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int? ExpiresIn { get; set; }
}

public class RoleRepresentation
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
}
