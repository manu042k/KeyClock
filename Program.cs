using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "KeyCloak Web API", Version = "v1" });

    var keycloakConfig = builder.Configuration.GetSection("Keycloak");
    var authority = keycloakConfig["Authority"];
    var clientId = keycloakConfig["ClientId"];

    // Add OAuth2 Authorization Code flow
    c.AddSecurityDefinition("OAuth2", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.OAuth2,
        Flows = new Microsoft.OpenApi.Models.OpenApiOAuthFlows
        {
            AuthorizationCode = new Microsoft.OpenApi.Models.OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri($"{authority}/protocol/openid-connect/auth"),
                TokenUrl = new Uri($"{authority}/protocol/openid-connect/token"),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID Connect scope" },
                    { "profile", "Access to user profile" },
                    { "email", "Access to user email" }
                }
            }
        }
    });

    // Add JWT Bearer authentication to Swagger (for manual token input)
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "OAuth2"
                }
            },
            new[] { "openid", "profile", "email" }
        },
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var keycloakConfig = builder.Configuration.GetSection("Keycloak");

        // Use InternalAuthority for fetching JWKS inside Docker
        var internalAuthority = keycloakConfig["InternalAuthority"];
        var publicAuthority = keycloakConfig["Authority"];

        // Use internal authority if available (for Docker), otherwise public
        var authority = !string.IsNullOrEmpty(internalAuthority) ? internalAuthority : publicAuthority;

        options.Authority = authority;

        // Set MetadataAddress to fetch JWKS from internal authority
        if (!string.IsNullOrEmpty(internalAuthority))
        {
            options.MetadataAddress = $"{internalAuthority}/.well-known/openid-configuration";
        }

        options.Audience = keycloakConfig["Audience"];
        options.RequireHttpsMetadata = bool.Parse(keycloakConfig["RequireHttpsMetadata"] ?? "true");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = publicAuthority, // Always validate against public issuer in tokens
            ValidateAudience = false, // Keycloak doesn't always include audience in access tokens
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = ClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var claimsIdentity = context.Principal.Identity as ClaimsIdentity;

                // Extract roles from realm_access claim
                var realmAccess = context.Principal.FindFirst("realm_access")?.Value;
                if (!string.IsNullOrEmpty(realmAccess))
                {
                    var realmAccessJson = System.Text.Json.JsonDocument.Parse(realmAccess);
                    if (realmAccessJson.RootElement.TryGetProperty("roles", out var realmRoles))
                    {
                        foreach (var role in realmRoles.EnumerateArray())
                        {
                            claimsIdentity?.AddClaim(new Claim(ClaimTypes.Role, role.GetString()));
                        }
                    }
                }

                // Extract roles from resource_access claim
                var resourceAccess = context.Principal.FindFirst("resource_access")?.Value;
                if (!string.IsNullOrEmpty(resourceAccess))
                {
                    var resourceAccessJson = System.Text.Json.JsonDocument.Parse(resourceAccess);
                    var clientId = keycloakConfig["ClientId"];

                    if (resourceAccessJson.RootElement.TryGetProperty(clientId, out var clientAccess) &&
                        clientAccess.TryGetProperty("roles", out var roles))
                    {
                        foreach (var role in roles.EnumerateArray())
                        {
                            claimsIdentity?.AddClaim(new Claim(ClaimTypes.Role, role.GetString()));
                        }
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

// Add Authorization
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("admin"));
    options.AddPolicy("UserOnly", policy => policy.RequireRole("user"));
    options.AddPolicy("AdminOrUser", policy => policy.RequireRole("admin", "user"));
});

// Add HttpClient for OAuth2 token exchange
builder.Services.AddHttpClient();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.WithOrigins(
                "http://localhost:3000",
                "http://localhost:5000",
                "http://localhost:5001",
                "http://localhost:8080",
                "https://localhost:5000",
                "https://localhost:5001"
            )
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
        });

    options.AddPolicy("SwaggerPolicy",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "KeyCloak Web API v1");
        c.OAuthClientId(builder.Configuration["Keycloak:ClientId"]);
        c.OAuthClientSecret(builder.Configuration["Keycloak:ClientSecret"]);
        c.OAuthRealm("webapi-realm");
        c.OAuthAppName("KeyCloak Web API");
        c.OAuthUsePkce();
    });
}

// app.UseHttpsRedirection(); // Commented out for Docker development

app.UseCors("SwaggerPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();