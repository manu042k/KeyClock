using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using KeyClockWebAPI.Models;

namespace KeyClockWebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AuthController(ILogger<AuthController> logger, IConfiguration configuration, HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    [HttpGet("public")]
    public ActionResult<ApiResponse<string>> GetPublicEndpoint()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "This is a public endpoint accessible to everyone",
            Data = "Public data"
        });
    }

    [HttpGet("protected")]
    [Authorize]
    public ActionResult<ApiResponse<object>> GetProtectedEndpoint()
    {
        var userInfo = new
        {
            UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
            Username = User.FindFirst("preferred_username")?.Value,
            Email = User.FindFirst(ClaimTypes.Email)?.Value,
            Roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList(),
            Claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        };

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "This is a protected endpoint accessible to authenticated users",
            Data = userInfo
        });
    }

    [HttpGet("admin-only")]
    [Authorize(Policy = "AdminOnly")]
    public ActionResult<ApiResponse<string>> GetAdminOnlyEndpoint()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "This endpoint is accessible only to users with 'admin' role",
            Data = "Admin-only data"
        });
    }

    [HttpGet("user-only")]
    [Authorize(Policy = "UserOnly")]
    public ActionResult<ApiResponse<string>> GetUserOnlyEndpoint()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "This endpoint is accessible only to users with 'user' role",
            Data = "User-only data"
        });
    }

    [HttpGet("admin-or-user")]
    [Authorize(Policy = "AdminOrUser")]
    public ActionResult<ApiResponse<string>> GetAdminOrUserEndpoint()
    {
        return Ok(new ApiResponse<string>
        {
            Success = true,
            Message = "This endpoint is accessible to users with 'admin' or 'user' role",
            Data = "Admin or User data"
        });
    }


    [HttpGet("callback")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, [FromQuery] string? error_description)
    {
        // Log all query parameters for debugging
        _logger.LogInformation("Callback received - Code: {Code}, State: {State}, Error: {Error}, ErrorDescription: {ErrorDescription}",
            code ?? "null", state ?? "null", error ?? "null", error_description ?? "null");

        // Check if there was an OAuth error
        if (!string.IsNullOrEmpty(error))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = $"OAuth error: {error}. Description: {error_description ?? "No description provided"}",
                Data = new { error, error_description }
            });
        }

        // Check if authorization code is missing
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Authorization code is required. This usually happens when the user denied access or there was an OAuth error.",
                Data = new
                {
                    receivedParameters = Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()),
                    possibleCauses = new[] {
                        "User denied authorization",
                        "Keycloak client configuration issue",
                        "Invalid redirect URI",
                        "OAuth2 flow error"
                    }
                }
            });
        }

        try
        {
            var keycloakConfig = _configuration.GetSection("Keycloak");
            var clientId = keycloakConfig["ClientId"];
            var clientSecret = keycloakConfig["ClientSecret"];
            var authority = keycloakConfig["Authority"];
            var internalAuthority = keycloakConfig["InternalAuthority"] ?? authority;
            var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/callback";

            // Exchange authorization code for tokens (use internal URL for container-to-container communication)
            var tokenEndpoint = $"{internalAuthority}/protocol/openid-connect/token";
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("redirect_uri", redirectUri)
            });

            var tokenResponse = await _httpClient.PostAsync(tokenEndpoint, tokenRequest);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

            _logger.LogInformation($"Token exchange response status: {tokenResponse.StatusCode}");
            _logger.LogInformation($"Token exchange response: {tokenContent}");

            if (!tokenResponse.IsSuccessStatusCode)
            {
                _logger.LogError($"Token exchange failed: {tokenContent}");
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to exchange authorization code for tokens",
                    Data = new
                    {
                        StatusCode = (int)tokenResponse.StatusCode,
                        Error = tokenContent,
                        TokenEndpoint = tokenEndpoint,
                        RedirectUri = redirectUri
                    }
                });
            }

            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);

            // Extract return URL from state
            var returnUrl = "/";
            if (!string.IsNullOrEmpty(state))
            {
                try
                {
                    var decodedState = Encoding.UTF8.GetString(Convert.FromBase64String(state));
                    var stateParts = decodedState.Split('|');
                    if (stateParts.Length > 1)
                    {
                        returnUrl = stateParts[1];
                    }
                }
                catch
                {
                    // If state decoding fails, use default return URL
                }
            }

            // For API, return the tokens as JSON
            // In a real application, you might want to set HTTP-only cookies or redirect to a frontend app
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Login successful",
                Data = new
                {
                    AccessToken = tokenData.GetProperty("access_token").GetString(),
                    RefreshToken = tokenData.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
                    ExpiresIn = tokenData.GetProperty("expires_in").GetInt32(),
                    TokenType = tokenData.GetProperty("token_type").GetString(),
                    ReturnUrl = returnUrl
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during OAuth callback processing");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Internal server error during authentication",
                Data = null
            });
        }
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Refresh token is required",
                Data = null
            });
        }

        try
        {
            var keycloakConfig = _configuration.GetSection("Keycloak");
            var clientId = keycloakConfig["ClientId"];
            var clientSecret = keycloakConfig["ClientSecret"];
            var authority = keycloakConfig["Authority"];
            var internalAuthority = keycloakConfig["InternalAuthority"] ?? authority;

            var tokenEndpoint = $"{internalAuthority}/protocol/openid-connect/token";
            var tokenRequest = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("client_id", clientId),
                new KeyValuePair<string, string>("client_secret", clientSecret),
                new KeyValuePair<string, string>("refresh_token", request.RefreshToken)
            });

            var tokenResponse = await _httpClient.PostAsync(tokenEndpoint, tokenRequest);
            var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
            {
                return BadRequest(new ApiResponse<object>
                {
                    Success = false,
                    Message = "Failed to refresh token",
                    Data = null
                });
            }

            var tokenData = JsonSerializer.Deserialize<JsonElement>(tokenContent);

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Token refreshed successfully",
                Data = new
                {
                    AccessToken = tokenData.GetProperty("access_token").GetString(),
                    RefreshToken = tokenData.TryGetProperty("refresh_token", out var refresh) ? refresh.GetString() : null,
                    ExpiresIn = tokenData.GetProperty("expires_in").GetInt32(),
                    TokenType = tokenData.GetProperty("token_type").GetString()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new ApiResponse<object>
            {
                Success = false,
                Message = "Internal server error during token refresh",
                Data = null
            });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        try
        {
            var keycloakConfig = _configuration.GetSection("Keycloak");
            var clientId = keycloakConfig["ClientId"];
            var clientSecret = keycloakConfig["ClientSecret"];
            var authority = keycloakConfig["Authority"];
            var internalAuthority = keycloakConfig["InternalAuthority"] ?? authority;

            if (!string.IsNullOrEmpty(request.RefreshToken))
            {
                // Revoke the refresh token
                var revokeEndpoint = $"{internalAuthority}/protocol/openid-connect/revoke";
                var revokeRequest = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("token", request.RefreshToken),
                    new KeyValuePair<string, string>("token_type_hint", "refresh_token")
                });

                await _httpClient.PostAsync(revokeEndpoint, revokeRequest);
            }

            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Logout successful",
                Data = null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout");
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Logout completed (with warnings)",
                Data = null
            });
        }
    }
}