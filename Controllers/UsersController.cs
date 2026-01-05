using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KeyClockWebAPI.Models;

namespace KeyClockWebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private static readonly List<User> _users = new();

    public UsersController(ILogger<UsersController> logger)
    {
        _logger = logger;

        // Initialize with sample data if empty
        if (!_users.Any())
        {
            _users.AddRange(new List<User>
            {
                new User { Id = 1, Username = "admin", Email = "admin@example.com", FirstName = "Admin", LastName = "User", Roles = new List<string> { "admin" } },
                new User { Id = 2, Username = "user1", Email = "user1@example.com", FirstName = "Regular", LastName = "User", Roles = new List<string> { "user" } },
                new User { Id = 3, Username = "moderator", Email = "mod@example.com", FirstName = "Moderator", LastName = "User", Roles = new List<string> { "moderator", "user" } }
            });
        }
    }

    [HttpGet]
    public ActionResult<ApiResponse<List<User>>> GetAllUsers()
    {
        return Ok(new ApiResponse<List<User>>
        {
            Success = true,
            Message = "Users retrieved successfully",
            Data = _users
        });
    }

    [HttpGet("{id}")]
    public ActionResult<ApiResponse<User>> GetUser(int id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new ApiResponse<User>
            {
                Success = false,
                Message = "User not found"
            });
        }

        return Ok(new ApiResponse<User>
        {
            Success = true,
            Message = "User retrieved successfully",
            Data = user
        });
    }

    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult<ApiResponse<User>> CreateUser([FromBody] User user)
    {
        if (user == null)
        {
            return BadRequest(new ApiResponse<User>
            {
                Success = false,
                Message = "Invalid user data"
            });
        }

        user.Id = _users.Any() ? _users.Max(u => u.Id) + 1 : 1;
        user.CreatedAt = DateTime.UtcNow;
        _users.Add(user);

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new ApiResponse<User>
        {
            Success = true,
            Message = "User created successfully",
            Data = user
        });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult<ApiResponse<User>> UpdateUser(int id, [FromBody] User updatedUser)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new ApiResponse<User>
            {
                Success = false,
                Message = "User not found"
            });
        }

        user.Username = updatedUser.Username;
        user.Email = updatedUser.Email;
        user.FirstName = updatedUser.FirstName;
        user.LastName = updatedUser.LastName;
        user.IsActive = updatedUser.IsActive;
        user.Roles = updatedUser.Roles;

        return Ok(new ApiResponse<User>
        {
            Success = true,
            Message = "User updated successfully",
            Data = user
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult<ApiResponse<bool>> DeleteUser(int id)
    {
        var user = _users.FirstOrDefault(u => u.Id == id);

        if (user == null)
        {
            return NotFound(new ApiResponse<bool>
            {
                Success = false,
                Message = "User not found"
            });
        }

        _users.Remove(user);

        return Ok(new ApiResponse<bool>
        {
            Success = true,
            Message = "User deleted successfully",
            Data = true
        });
    }
}