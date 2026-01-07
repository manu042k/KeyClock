using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KeycloakWebAPI.DTOs;
using KeycloakWebAPI.Models;
using KeycloakWebAPI.Services;
using System.Security.Claims;

namespace KeycloakWebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly KeycloakAdminService _keycloakService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(KeycloakAdminService keycloakService, ILogger<UsersController> logger)
    {
        _keycloakService = keycloakService;
        _logger = logger;
    }

    /// <summary>
    /// Get all users from Keycloak (requires authentication)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UserResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IEnumerable<UserResponseDto>>> GetUsers(
        [FromQuery] bool? enabled = null,
        [FromQuery] string? search = null)
    {
        LogUserInfo();

        try
        {
            var users = await _keycloakService.GetUsersAsync(enabled, search);

            var userDtos = users.Select(u => new UserResponseDto
            {
                Id = u.Id ?? "",
                Username = u.Username ?? "",
                Email = u.Email ?? "",
                FirstName = u.FirstName ?? "",
                LastName = u.LastName ?? "",
                Enabled = u.Enabled,
                EmailVerified = u.EmailVerified,
                CreatedAt = u.CreatedTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(u.CreatedTimestamp.Value).DateTime
                    : null,
                Roles = u.Roles,
                CustomAttributes = u.Attributes
            }).ToList();

            return Ok(userDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving users from Keycloak");
            return StatusCode(500, new { message = "Error retrieving users from Keycloak", error = ex.Message });
        }
    }

    /// <summary>
    /// Get user by ID from Keycloak (requires authentication)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponseDto>> GetUser(string id)
    {
        LogUserInfo();

        try
        {
            var user = await _keycloakService.GetUserByIdAsync(id);

            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found in Keycloak", id);
                return NotFound(new { message = $"User with ID {id} not found" });
            }

            var userDto = new UserResponseDto
            {
                Id = user.Id ?? "",
                Username = user.Username ?? "",
                Email = user.Email ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Enabled = user.Enabled,
                EmailVerified = user.EmailVerified,
                CreatedAt = user.CreatedTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(user.CreatedTimestamp.Value).DateTime
                    : null,
                Roles = user.Roles,
                CustomAttributes = user.Attributes
            };

            return Ok(userDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving user {UserId} from Keycloak", id);
            return StatusCode(500, new { message = "Error retrieving user from Keycloak", error = ex.Message });
        }
    }

    /// <summary>
    /// Create a new user in Keycloak (requires Admin role)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponseDto>> CreateUser([FromBody] CreateUserDto createUserDto)
    {
        LogUserInfo();

        try
        {
            var createRequest = new CreateKeycloakUserRequest
            {
                Username = createUserDto.Username,
                Email = createUserDto.Email,
                FirstName = createUserDto.FirstName,
                LastName = createUserDto.LastName,
                Password = createUserDto.Password,
                TemporaryPassword = createUserDto.TemporaryPassword,
                Enabled = true,
                EmailVerified = false,
                Roles = createUserDto.Roles,
                Attributes = createUserDto.CustomAttributes
            };

            var user = await _keycloakService.CreateUserAsync(createRequest);

            _logger.LogInformation("User {Username} created successfully in Keycloak with ID {UserId}",
                user.Username, user.Id);

            var userDto = new UserResponseDto
            {
                Id = user.Id ?? "",
                Username = user.Username ?? "",
                Email = user.Email ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Enabled = user.Enabled,
                EmailVerified = user.EmailVerified,
                CreatedAt = user.CreatedTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(user.CreatedTimestamp.Value).DateTime
                    : null,
                Roles = user.Roles,
                CustomAttributes = user.Attributes
            };

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error creating user in Keycloak");
            return BadRequest(new { message = "Failed to create user in Keycloak", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user in Keycloak");
            return StatusCode(500, new { message = "Error creating user in Keycloak", error = ex.Message });
        }
    }

    /// <summary>
    /// Update an existing user in Keycloak (requires Admin role)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(UserResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<UserResponseDto>> UpdateUser(string id, [FromBody] UpdateUserDto updateUserDto)
    {
        LogUserInfo();

        try
        {
            // Check if user exists first
            var existingUser = await _keycloakService.GetUserByIdAsync(id);
            if (existingUser == null)
            {
                _logger.LogWarning("User with ID {UserId} not found in Keycloak for update", id);
                return NotFound(new { message = $"User with ID {id} not found" });
            }

            var updateRequest = new UpdateKeycloakUserRequest
            {
                Email = updateUserDto.Email,
                FirstName = updateUserDto.FirstName,
                LastName = updateUserDto.LastName,
                Password = updateUserDto.Password,
                TemporaryPassword = updateUserDto.TemporaryPassword,
                Enabled = updateUserDto.Enabled,
                EmailVerified = updateUserDto.EmailVerified,
                Roles = updateUserDto.Roles,
                Attributes = updateUserDto.CustomAttributes
            };

            var user = await _keycloakService.UpdateUserAsync(id, updateRequest);

            _logger.LogInformation("User {Username} (ID: {UserId}) updated successfully in Keycloak",
                user.Username, user.Id);

            var userDto = new UserResponseDto
            {
                Id = user.Id ?? "",
                Username = user.Username ?? "",
                Email = user.Email ?? "",
                FirstName = user.FirstName ?? "",
                LastName = user.LastName ?? "",
                Enabled = user.Enabled,
                EmailVerified = user.EmailVerified,
                CreatedAt = user.CreatedTimestamp.HasValue
                    ? DateTimeOffset.FromUnixTimeMilliseconds(user.CreatedTimestamp.Value).DateTime
                    : null,
                Roles = user.Roles,
                CustomAttributes = user.Attributes
            };

            return Ok(userDto);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error updating user in Keycloak");
            return BadRequest(new { message = "Failed to update user in Keycloak", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserId} in Keycloak", id);
            return StatusCode(500, new { message = "Error updating user in Keycloak", error = ex.Message });
        }
    }

    /// <summary>
    /// Delete a user from Keycloak (requires Admin role)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(string id)
    {
        LogUserInfo();

        try
        {
            // Check if user exists first
            var user = await _keycloakService.GetUserByIdAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User with ID {UserId} not found in Keycloak for deletion", id);
                return NotFound(new { message = $"User with ID {id} not found" });
            }

            await _keycloakService.DeleteUserAsync(id);

            _logger.LogInformation("User {Username} (ID: {UserId}) deleted successfully from Keycloak",
                user.Username, id);

            return NoContent();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error deleting user from Keycloak");
            return BadRequest(new { message = "Failed to delete user from Keycloak", error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user {UserId} from Keycloak", id);
            return StatusCode(500, new { message = "Error deleting user from Keycloak", error = ex.Message });
        }
    }

    /// <summary>
    /// Get current authenticated user info
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value ??
                      User.FindFirst("preferred_username")?.Value ??
                      "Unknown";

        var email = User.FindFirst(ClaimTypes.Email)?.Value ??
                   User.FindFirst("email")?.Value;

        var roles = User.FindAll(ClaimTypes.Role)
                       .Select(c => c.Value)
                       .ToList();

        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

        return Ok(new
        {
            username,
            email,
            roles,
            claims,
            isAuthenticated = User.Identity?.IsAuthenticated ?? false
        });
    }

    private void LogUserInfo()
    {
        var username = User.FindFirst(ClaimTypes.Name)?.Value ??
                      User.FindFirst("preferred_username")?.Value ??
                      "Unknown";

        var roles = string.Join(", ", User.FindAll(ClaimTypes.Role).Select(c => c.Value));

        _logger.LogInformation("Request by user: {Username}, Roles: {Roles}", username, roles);
    }
}
