#!/bin/bash

echo "🚀 Starting KeyCloak Web API Docker Setup..."
echo "========================================"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo "❌ Docker is not running. Please start Docker and try again."
    exit 1
fi

# Check if Docker Compose is available
if ! command -v docker-compose > /dev/null 2>&1; then
    echo "❌ Docker Compose is not installed. Please install Docker Compose and try again."
    exit 1
fi

echo "✅ Docker and Docker Compose are available"
echo ""

echo "🛠️  Building and starting services..."
docker-compose down -v
docker-compose build --no-cache
docker-compose up -d

echo ""
echo "⏳ Waiting for services to be healthy..."

# Wait for all services to be healthy
timeout=300
elapsed=0
while [ $elapsed -lt $timeout ]; do
    if docker-compose ps | grep -q "Up (healthy).*webapi"; then
        echo "✅ All services are healthy and ready!"
        break
    fi
    
    echo "   Still waiting... ($elapsed/${timeout}s)"
    sleep 10
    elapsed=$((elapsed + 10))
done

if [ $elapsed -ge $timeout ]; then
    echo "❌ Services did not become healthy within $timeout seconds"
    echo "Check the logs with: docker-compose logs"
    exit 1
fi

echo ""
echo "🎉 Setup completed successfully!"
echo ""
echo "=== Service URLs ==="
echo "📊 Web API:              http://localhost:5000"
echo "📖 API Documentation:    http://localhost:5000/swagger"
echo "🔐 Keycloak Admin:       http://localhost:8080"
echo "📁 PostgreSQL:           localhost:5432"
echo ""
echo "=== Keycloak Admin Access ==="
echo "👤 Username: admin"
echo "🔑 Password: admin123"
echo ""
echo "=== Test Users (Realm: webapi-realm) ==="
echo "🔴 Admin:     admin / admin123        (roles: admin)"
echo "🟡 User:      john.doe / user123      (roles: user)"
echo "🟢 Moderator: jane.smith / mod123     (roles: moderator, user)"
echo "🔵 Test:      test.user / test123     (roles: user)"
echo ""
echo "=== Quick Test Command ==="
echo "# Get a JWT token:"
echo "curl -X POST http://localhost:8080/realms/webapi-realm/protocol/openid-connect/token \\"
echo "  -H \"Content-Type: application/x-www-form-urlencoded\" \\"
echo "  -d \"grant_type=password\" \\"
echo "  -d \"client_id=keycloak-web-api\" \\"
echo "  -d \"client_secret=web-api-client-secret-123\" \\"
echo "  -d \"username=admin\" \\"
echo "  -d \"password=admin123\""
echo ""
echo "# Test the API:"
echo "curl -X GET http://localhost:5000/api/auth/public"
echo ""
echo "📋 To stop all services: docker-compose down"
echo "🗑️  To reset all data:   docker-compose down -v"