using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using KeycloakWebAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register HttpClient for KeycloakAdminService
builder.Services.AddHttpClient<KeycloakAdminService>();

// Configure JWT Authentication with Keycloak
var keycloakConfig = builder.Configuration.GetSection("Keycloak");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = keycloakConfig["Authority"] ?? throw new InvalidOperationException("Keycloak Authority not configured");
    options.Audience = keycloakConfig["Audience"] ?? "account";
    options.MetadataAddress = keycloakConfig["MetadataAddress"];
    options.RequireHttpsMetadata = bool.Parse(keycloakConfig["RequireHttpsMetadata"] ?? "false");

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidIssuer = keycloakConfig["Authority"],
        ValidateAudience = bool.Parse(keycloakConfig["ValidateAudience"] ?? "false"),
        ValidateLifetime = bool.Parse(keycloakConfig["ValidateLifetime"] ?? "true"),
        ValidateIssuerSigningKey = false,
        SignatureValidator = delegate (string token, TokenValidationParameters parameters) { return new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token); },
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("User", "Admin"));
});

// Configure Swagger with OAuth2
var swaggerConfig = builder.Configuration.GetSection("Swagger");
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Keycloak User Management API",
        Version = "v1",
        Description = "A production-ready ASP.NET Core Web API with Keycloak authentication and user management",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@example.com"
        }
    });

    // Define the OAuth2 security scheme
    c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                AuthorizationUrl = new Uri(swaggerConfig["AuthorizationUrl"] ?? ""),
                TokenUrl = new Uri(swaggerConfig["TokenUrl"] ?? ""),
                Scopes = new Dictionary<string, string>
                {
                    { "openid", "OpenID" },
                    { "profile", "Profile" },
                    { "email", "Email" }
                }
            }
        },
        Description = "Keycloak OAuth2 Authorization"
    });

    // Add security requirement
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { "openid", "profile", "email" }
        }
    });
});

// Configure CORS for production
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Keycloak User Management API v1");
        c.OAuthClientId(swaggerConfig["ClientId"]);
        c.OAuthClientSecret(swaggerConfig["ClientSecret"]);
        c.OAuthUsePkce();
        c.DisplayRequestDuration();
    });
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
