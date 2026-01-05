# KeyCloak Web API

A comprehensive ASP.NET Core Web API project integrated with Keycloak for authentication and authorization using JWT tokens.

## Features

- 🔐 **JWT Authentication**: Integration with Keycloak for secure authentication
- 🛡️ **Role-based Authorization**: Different access levels (Admin, User, Public)
- 📝 **Swagger Documentation**: Interactive API documentation with JWT support
- 🐳 **Docker Support**: Complete Docker Compose setup for Keycloak
- 🎯 **Sample Controllers**: Demonstration of various authorization scenarios
- ⚡ **RESTful API**: Clean and well-structured API endpoints

## Project Structure

```
KeyClockWebAPI/
├── Controllers/
│   ├── AuthController.cs      # Authentication demos
│   ├── UsersController.cs     # User management (Admin only)
│   └── ProductsController.cs  # Product management (Mixed permissions)
├── Models/
│   └── Models.cs             # Data models and DTOs
├── Properties/
│   └── launchSettings.json   # Launch profiles
├── appsettings.json          # Development configuration
├── appsettings.Production.json # Production configuration
├── docker-compose.yml        # Keycloak setup
├── Program.cs               # Application startup
└── KeyClockWebAPI.csproj   # Project file
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Docker and Docker Compose
- Visual Studio 2022 or VS Code

### 1. Set up Keycloak

Start Keycloak using Docker Compose:

```bash
docker-compose up -d
```

Wait for the services to start (usually takes 1-2 minutes). You can check the status with:

```bash
docker-compose ps
```

### 2. Configure Keycloak

1. **Access Keycloak Admin Console**:

   - URL: http://localhost:8080
   - Username: `admin`
   - Password: `admin123`

2. **Create a Realm** (or use the master realm):

   - Go to the realm dropdown (top left)
   - Click "Create Realm"
   - Name: `myrealm` (or keep using `master`)

3. **Create a Client**:

   - Go to Clients → Create client
   - Client ID: `keycloak-web-api`
   - Client authentication: ON
   - Authorization: ON
   - Valid redirect URIs: `*`
   - Web origins: `*`

4. **Create Roles**:

   - Go to Realm roles
   - Create roles: `admin`, `user`, `moderator`

5. **Create Users**:

   - Go to Users → Add user
   - Create test users and assign roles in the "Role mapping" tab

6. **Update Configuration**:
   - Update `appsettings.json` with your realm and client details:

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/myrealm",
    "Audience": "keycloak-web-api",
    "ClientId": "keycloak-web-api",
    "RequireHttpsMetadata": "false"
  }
}
```

### 3. Run the API

```bash
# Restore packages
dotnet restore

# Run the application
dotnet run
```

The API will be available at:

- HTTP: http://localhost:5000
- HTTPS: https://localhost:5001
- Swagger UI: http://localhost:5000/swagger

## API Endpoints

### Authentication Controller (`/api/auth`)

| Endpoint         | Method | Auth       | Description          |
| ---------------- | ------ | ---------- | -------------------- |
| `/public`        | GET    | None       | Public endpoint      |
| `/protected`     | GET    | JWT        | Shows user info      |
| `/admin-only`    | GET    | Admin role | Admin only access    |
| `/user-only`     | GET    | User role  | User only access     |
| `/admin-or-user` | GET    | Admin/User | Admin or User access |

### Users Controller (`/api/users`)

| Endpoint | Method | Auth  | Description     |
| -------- | ------ | ----- | --------------- |
| `/`      | GET    | JWT   | Get all users   |
| `/{id}`  | GET    | JWT   | Get user by ID  |
| `/`      | POST   | Admin | Create new user |
| `/{id}`  | PUT    | Admin | Update user     |
| `/{id}`  | DELETE | Admin | Delete user     |

### Products Controller (`/api/products`)

| Endpoint               | Method | Auth  | Description              |
| ---------------------- | ------ | ----- | ------------------------ |
| `/`                    | GET    | None  | Get all products         |
| `/{id}`                | GET    | None  | Get product by ID        |
| `/category/{category}` | GET    | None  | Get products by category |
| `/`                    | POST   | Admin | Create new product       |
| `/{id}`                | PUT    | Admin | Update product           |
| `/{id}`                | DELETE | Admin | Delete product           |

## Getting JWT Tokens

### Method 1: Using Postman or curl

1. **Get Token from Keycloak**:

```bash
curl -X POST http://localhost:8080/realms/myrealm/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=keycloak-web-api" \
  -d "client_secret=YOUR_CLIENT_SECRET" \
  -d "username=YOUR_USERNAME" \
  -d "password=YOUR_PASSWORD"
```

2. **Use the access_token in API calls**:

```bash
curl -X GET http://localhost:5000/api/auth/protected \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

### Method 2: Using Swagger UI

1. Open Swagger UI: http://localhost:5000/swagger
2. Click the "Authorize" button (lock icon)
3. Enter: `Bearer YOUR_ACCESS_TOKEN`
4. Now you can test protected endpoints directly from Swagger

## Configuration

### appsettings.json

```json
{
  "Keycloak": {
    "Authority": "http://localhost:8080/realms/myrealm",
    "Audience": "keycloak-web-api",
    "ClientId": "keycloak-web-api",
    "RequireHttpsMetadata": "false"
  }
}
```

### Key Configuration Properties

- **Authority**: Your Keycloak realm URL
- **Audience**: The client ID that should receive the token
- **ClientId**: Your Keycloak client ID
- **RequireHttpsMetadata**: Set to false for development

## Authorization Policies

The API includes several authorization policies:

- **AdminOnly**: Requires `admin` role
- **UserOnly**: Requires `user` role
- **AdminOrUser**: Requires either `admin` or `user` role

## Docker Commands

```bash
# Start Keycloak
docker-compose up -d

# View logs
docker-compose logs -f keycloak

# Stop services
docker-compose down

# Reset everything (removes data)
docker-compose down -v
```

## Development Tips

1. **Debug JWT Tokens**: Use [jwt.io](https://jwt.io) to decode and inspect your JWT tokens
2. **Check Logs**: Monitor application logs for authentication issues
3. **Swagger Testing**: Use the built-in Swagger UI for easy API testing
4. **Role Mapping**: Ensure users have proper role assignments in Keycloak

## Troubleshooting

### Common Issues

1. **401 Unauthorized**:

   - Check if JWT token is valid and not expired
   - Verify the token is properly formatted in Authorization header
   - Ensure user has required roles

2. **403 Forbidden**:

   - User is authenticated but lacks required role
   - Check role assignments in Keycloak

3. **Connection Issues**:
   - Verify Keycloak is running: `docker-compose ps`
   - Check if ports 8080 and 5432 are available
   - Verify Authority URL matches your Keycloak setup

### Debug Mode

Enable detailed logging by updating `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore.Authentication": "Debug",
      "Microsoft.AspNetCore.Authorization": "Debug"
    }
  }
}
```

## Production Deployment

1. **Update appsettings.Production.json** with production Keycloak URLs
2. **Enable HTTPS**: Set `RequireHttpsMetadata: true`
3. **Secure Secrets**: Use Azure Key Vault or similar for sensitive configuration
4. **Update CORS**: Configure specific origins instead of allowing all

## License

This project is licensed under the MIT License.
