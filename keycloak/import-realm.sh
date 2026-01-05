#!/bin/sh

echo "🔄 Starting Keycloak realm import process..."
echo "============================================="

# Wait for Keycloak to be ready with retries
MAX_RETRIES=10
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    echo "⏳ Waiting for Keycloak to be ready... (attempt $((RETRY_COUNT + 1))/$MAX_RETRIES)"
    
    # Check if Keycloak is responding
    if curl -s -f http://keycloak:8080/realms/master > /dev/null 2>&1; then
        echo "✅ Keycloak is ready!"
        break
    fi
    
    RETRY_COUNT=$((RETRY_COUNT + 1))
    if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
        echo "❌ Keycloak is not ready after $MAX_RETRIES attempts"
        exit 1
    fi
    
    sleep 10
done

# Get admin token
echo "🔑 Getting admin token..."
ADMIN_TOKEN=$(curl -s -X POST http://keycloak:8080/realms/master/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "username=admin" \
  -d "password=admin123" \
  -d "grant_type=password" \
  -d "client_id=admin-cli" | jq -r '.access_token')

if [ "$ADMIN_TOKEN" = "null" ] || [ -z "$ADMIN_TOKEN" ]; then
  echo "❌ Failed to get admin token. Keycloak may not be ready yet."
  exit 1
fi

echo "✅ Admin token obtained successfully"

# Check if realm already exists
echo "🔍 Checking if realm already exists..."
REALM_EXISTS=$(curl -s -H "Authorization: Bearer $ADMIN_TOKEN" \
  http://keycloak:8080/admin/realms/webapi-realm -o /dev/null -w "%{http_code}")

if [ "$REALM_EXISTS" = "200" ]; then
    echo "ℹ️  Realm 'webapi-realm' already exists. Skipping import."
else
    # Import the realm
    echo "📥 Importing realm configuration..."
    IMPORT_RESPONSE=$(curl -s -w "%{http_code}" -X POST http://keycloak:8080/admin/realms \
      -H "Authorization: Bearer $ADMIN_TOKEN" \
      -H "Content-Type: application/json" \
      -d @/opt/keycloak/data/import/realm-config.json)
    
    HTTP_CODE="${IMPORT_RESPONSE: -3}"
    
    if [ "$HTTP_CODE" = "201" ]; then
        echo "✅ Realm 'webapi-realm' imported successfully!"
    elif [ "$HTTP_CODE" = "409" ]; then
        echo "ℹ️  Realm 'webapi-realm' already exists."
    else
        echo "⚠️  Realm import response: HTTP $HTTP_CODE"
        echo "   This might be normal if the realm already exists."
    fi
fi

# Verify realm is accessible
echo "🔍 Verifying realm accessibility..."
if curl -s -f http://keycloak:8080/realms/webapi-realm > /dev/null; then
    echo "✅ Realm 'webapi-realm' is accessible"
else
    echo "❌ Failed to access realm 'webapi-realm'"
    exit 1
fi

echo "Keycloak setup completed!"
echo ""
echo "=== Keycloak Access Information ==="
echo "Keycloak Admin Console: http://localhost:8080"
echo "Admin Username: admin"
echo "Admin Password: admin123"
echo ""
echo "=== Test Users ==="
echo "1. Admin User:"
echo "   Username: admin"
echo "   Password: admin123"
echo "   Roles: admin"
echo ""
echo "2. Regular User:"
echo "   Username: john.doe"
echo "   Password: user123"
echo "   Roles: user"
echo ""
echo "3. Moderator User:"
echo "   Username: jane.smith"
echo "   Password: mod123"
echo "   Roles: moderator, user"
echo ""
echo "4. Test User:"
echo "   Username: test.user"
echo "   Password: test123"
echo "   Roles: user"
echo ""
echo "=== Get Token Example ==="
echo "curl -X POST http://localhost:8080/realms/webapi-realm/protocol/openid-connect/token \\"
echo "  -H \"Content-Type: application/x-www-form-urlencoded\" \\"
echo "  -d \"grant_type=password\" \\"
echo "  -d \"client_id=keycloak-web-api\" \\"
echo "  -d \"client_secret=web-api-client-secret-123\" \\"
echo "  -d \"username=admin\" \\"
echo "  -d \"password=admin123\""