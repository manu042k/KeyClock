# Keycloak User Management API

A production-ready ASP.NET Core Web API integrated with Keycloak for authentication and authorization, featuring **Keycloak Admin API** integration for real user management CRUD operations via Swagger UI.

## Features

- **Keycloak Integration**: Full OAuth2/OpenID Connect authentication
- **Keycloak Admin API**: Direct user management in Keycloak (not a local database)
- **Swagger Authorization**: One-click "Authorize" button in Swagger UI
- **Role-Based Access Control**: Admin and User roles with proper authorization
- **User CRUD Operations**: Create, Read, Update, Delete users directly in Keycloak
- **Production-Ready**: Users created via API can immediately login
- **Docker Support**: Keycloak and PostgreSQL in Docker Compose
- **Service Account**: Automated admin API access via client credentials

## Architecture

This implementation uses the **correct production pattern**:

1. **Keycloak** is the single source of truth for user identity
2. **Admin API** manages users directly in Keycloak
3. **Service Account** (admin-cli client) provides API access to Keycloak
4. Users created via the API **can immediately login** to your application
5. No local database for user storage - all data is in Keycloak

## Prerequisites

- .NET 8.0 SDK
- Docker and Docker Compose
- curl (for setup script)
- jq (for JSON parsing in setup script)

## Quick Start

### 1. Start Keycloak and PostgreSQL

```bash
docker-compose up -d
```

Wait for Keycloak to be fully ready (about 30-60 seconds).

### 2. Configure Keycloak

Run the setup script to create realm, client, roles, and test users:

```bash
./keycloak-setup.sh
```

This will create:

- **Realm**: `webapi-realm`
- **Client**: `webapi-client` with secret `your-client-secret-here` (for user authentication)
- **Admin Client**: `admin-cli` with secret `admin-cli-secret` (for API user management)
- **Service Account**: Configured with realm-management permissions
- **Roles**: Admin, User, Manager
- **Test Users**:
  - Admin user: username `admin`, password `admin123` (role: Admin)
  - Regular user: username `user`, password `user123` (role: User)

### 3. Run the .NET Application

```bash
dotnet restore
dotnet run
```

The API will start on:

- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### 4. Access Swagger UI

Open your browser and navigate to:

```
http://localhost:5000/swagger
```

## Using Swagger Authorization

1. **Click the "Authorize" button** (🔓 icon) in the top-right of Swagger UI
2. **Check the scopes**: `openid`, `profile`, `email`
3. **Click "Authorize"**
4. You'll be redirected to Keycloak login page
5. **Login with test credentials**:
   - Username: `admin` Password: `admin123` (for Admin access)
   - OR Username: `user` Password: `user123` (for read-only access)
6. After successful login, you'll be redirected back to Swagger
7. The 🔓 icon will change to 🔒, indicating you're authorized
8. **Now you can test the APIs!**

## API Endpoints

### All endpoints interact directly with Keycloak

| Method | Endpoint          | Description                   | Required Role          |
| ------ | ----------------- | ----------------------------- | ---------------------- |
| GET    | `/api/users`      | Get all users from Keycloak   | Any authenticated user |
| GET    | `/api/users/{id}` | Get user by ID from Keycloak  | Any authenticated user |
| GET    | `/api/users/me`   | Get current user info         | Any authenticated user |
| POST   | `/api/users`      | **Create user in Keycloak**   | **Admin only**         |
| PUT    | `/api/users/{id}` | **Update user in Keycloak**   | **Admin only**         |
| DELETE | `/api/users/{id}` | **Delete user from Keycloak** | **Admin only**         |

**Important**: Users created via the API are immediately able to login because they're created directly in Keycloak.

### Example API Requests

#### Get All Users

```bash
GET /api/users
```

#### Get User by ID

```bash
GET /api/users/{keycloak-user-id}
```

#### Create User (Admin only)

**This creates a real user in Keycloak who can immediately login!**

```bash
POST /api/users
Content-Type: application/json

{
  "username": "newuser",
  "email": "newuser@example.com",
  "firstName": "New",
  "lastName": "User",
  "password": "password123",
  "temporaryPassword": false,
  "roles": ["User"]
}
```

#### Update User (Admin only)

```bash
PUT /api/users/{keycloak-user-id}
Content-Type: application/json

{
  "email": "updated@example.com",
  "firstName": "Updated",
  "enabled": true,
  "roles": ["Admin", "User"]
}
```

#### Delete User (Admin only)

```bash
DELETE /api/users/{keycloak-user-id}
```

## Manual Token Testing (Alternative to Swagger)

If you want to test with curl or Postman:

### 1. Get Access Token

```bash
curl -X POST "http://localhost:8080/realms/webapi-realm/protocol/openid-connect/token" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "client_id=webapi-client" \
  -d "client_secret=your-client-secret-here" \
  -d "username=admin" \
  -d "password=admin123" \
  -d "grant_type=password"
```

### 2. Use Token in API Requests

```bash
curl -X GET "http://localhost:5000/api/users" \
  -H "Authorization: Bearer YOUR_ACCESS_TOKEN"
```

## Project Structure

```
KeyClock/
├── Controllers/
│   └── UsersController.cs       # User CRUD via Keycloak Admin API
├── Services/
│   └── KeycloakAdminService.cs  # Keycloak Admin API client
├── Models/
│   └── KeycloakUser.cs          # Keycloak user representation
├── DTOs/
│   └── UserDtos.cs              # API Data Transfer Objects
├── Program.cs                   # Application startup & configuration
├── appsettings.json             # Development configuration
├── appsettings.Production.json  # Production configuration
├── docker-compose.yml           # Docker services
├── keycloak-setup.sh           # Keycloak initialization script
├── KeycloakWebAPI.csproj       # Project file
└── README.md                    # This file
```

## Configuration

### appsettings.json

Key configurations:

```json
{
  "Keycloak": {
    "Realm": "webapi-realm",
    "AdminUrl": "http://localhost:8080",
    "Authority": "http://localhost:8080/realms/webapi-realm",
    "Audience": "account",
    "MetadataAddress": "http://localhost:8080/realms/webapi-realm/.well-known/openid-configuration",
    "AdminClientId": "admin-cli",
    "AdminClientSecret": "admin-cli-secret"
  },
  "Swagger": {
    "AuthorizationUrl": "http://localhost:8080/realms/webapi-realm/protocol/openid-connect/auth",
    "TokenUrl": "http://localhost:8080/realms/webapi-realm/protocol/openid-connect/token",
    "ClientId": "webapi-client",
    "ClientSecret": "your-client-secret-here"
  }
}
```

**Key Configuration Points:**

- `AdminClientId` and `AdminClientSecret`: Used by the API to authenticate with Keycloak Admin API
- `ClientId` and `ClientSecret`: Used by Swagger UI for user authentication

## Production Deployment

For production:

1. **Update `appsettings.Production.json`**:
   - Change Keycloak URLs to production server
   - Use proper client secrets (never commit secrets to git!)
   - Enable HTTPS metadata validation
2. **Security Best Practices**:

   - Use environment variables for secrets
   - Enable HTTPS everywhere
   - Configure proper CORS policies
   - Use a real database (SQL Server, PostgreSQL, etc.)
   - Enable rate limiting
   - Add proper logging and monitoring

3. **Keycloak Production Setup**:
   - Use PostgreSQL or MySQL as Keycloak database
   - Enable HTTPS with proper SSL certificates
   - Configure proper realm settings
   - Set up backup and recovery
   - Enable proper session management

## Testing the Flow

1. **Start services**: `docker-compose up -d`
2. **Setup Keycloak**: `./keycloak-setup.sh` (creates realm, clients, service account with admin permissions)
3. **Run API**: `dotnet run`
4. **Open Swagger**: `http://localhost:5000/swagger`
5. **Click Authorize**: Use `admin/admin123` credentials
6. **Test GET /api/users**: Should return users from Keycloak (including admin and user)
7. **Test POST /api/users**: Create a new user - **they can immediately login!**
8. **Verify**: Try logging in with the newly created user credentials
9. **Test with User role**: Logout, authorize with `user/user123`, try POST (should get 403 Forbidden)

## Key Differences from Database Approach

**Wrong Approach (what was originally provided)**:

- Users stored in local in-memory database
- Creating a user via API doesn't allow them to login
- Two separate user stores (Keycloak + local DB)
- No synchronization between systems

**Correct Approach (current implementation)**:

- Users managed directly in Keycloak via Admin API
- Creating a user via API immediately enables login
- Single source of truth for user identity
- Service account with proper permissions
- Production-ready architecture

## Troubleshooting

### Keycloak not starting

```bash
docker-compose logs keycloak
```

### Token validation fails

- Check that Keycloak is running: `curl http://localhost:8080/health/ready`
- Verify realm and client configuration in Keycloak admin console
- Check authority URLs in appsettings.json

### Swagger authorization not working

- Clear browser cookies
- Check client redirect URIs in Keycloak
- Verify client secret matches in both Keycloak and appsettings.json

## Useful Links

- **Swagger UI**: http://localhost:5000/swagger
- **Keycloak Admin**: http://localhost:8080/admin (admin/admin)
- **Keycloak Realm**: http://localhost:8080/realms/webapi-realm/.well-known/openid-configuration
