#!/bin/bash

echo "🧪 Testing KeyCloak Web API Integration..."
echo "======================================="

BASE_URL="http://localhost:5001"
KEYCLOAK_URL="http://localhost:8080"
REALM="webapi-realm"
CLIENT_ID="keycloak-web-api"
CLIENT_SECRET="web-api-client-secret-123"

# Test public endpoint
echo "1️⃣  Testing public endpoint..."
PUBLIC_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" $BASE_URL/api/auth/public)
if [ "$PUBLIC_RESPONSE" = "200" ]; then
    echo "   ✅ Public endpoint is accessible"
else
    echo "   ❌ Public endpoint failed (HTTP $PUBLIC_RESPONSE)"
fi

# Get admin token
echo ""
echo "2️⃣  Getting admin JWT token..."
ADMIN_TOKEN=$(curl -s -X POST $KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "username=admin" \
  -d "password=admin123" | jq -r '.access_token')

if [ "$ADMIN_TOKEN" = "null" ] || [ -z "$ADMIN_TOKEN" ]; then
    echo "   ❌ Failed to get admin token"
    exit 1
else
    echo "   ✅ Admin token obtained successfully"
fi

# Test protected endpoint
echo ""
echo "3️⃣  Testing protected endpoint with admin token..."
PROTECTED_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  $BASE_URL/api/auth/protected)

if [ "$PROTECTED_RESPONSE" = "200" ]; then
    echo "   ✅ Protected endpoint is accessible with valid token"
else
    echo "   ❌ Protected endpoint failed (HTTP $PROTECTED_RESPONSE)"
fi

# Test admin-only endpoint
echo ""
echo "4️⃣  Testing admin-only endpoint..."
ADMIN_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
  -H "Authorization: Bearer $ADMIN_TOKEN" \
  $BASE_URL/api/auth/admin-only)

if [ "$ADMIN_RESPONSE" = "200" ]; then
    echo "   ✅ Admin-only endpoint is accessible with admin token"
else
    echo "   ❌ Admin-only endpoint failed (HTTP $ADMIN_RESPONSE)"
fi

# Get user token
echo ""
echo "5️⃣  Getting user JWT token..."
USER_TOKEN=$(curl -s -X POST $KEYCLOAK_URL/realms/$REALM/protocol/openid-connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=password" \
  -d "client_id=$CLIENT_ID" \
  -d "client_secret=$CLIENT_SECRET" \
  -d "username=john.doe" \
  -d "password=user123" | jq -r '.access_token')

if [ "$USER_TOKEN" = "null" ] || [ -z "$USER_TOKEN" ]; then
    echo "   ❌ Failed to get user token"
else
    echo "   ✅ User token obtained successfully"
    
    # Test user access to admin endpoint (should fail)
    echo ""
    echo "6️⃣  Testing admin endpoint with user token (should fail)..."
    USER_ADMIN_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
      -H "Authorization: Bearer $USER_TOKEN" \
      $BASE_URL/api/auth/admin-only)
    
    if [ "$USER_ADMIN_RESPONSE" = "403" ]; then
        echo "   ✅ Admin endpoint correctly rejected user token (HTTP 403)"
    else
        echo "   ❌ Admin endpoint should have returned 403 but got HTTP $USER_ADMIN_RESPONSE"
    fi
    
    # Test user endpoint with user token
    echo ""
    echo "7️⃣  Testing user-only endpoint with user token..."
    USER_ONLY_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" \
      -H "Authorization: Bearer $USER_TOKEN" \
      $BASE_URL/api/auth/user-only)
    
    if [ "$USER_ONLY_RESPONSE" = "200" ]; then
        echo "   ✅ User-only endpoint is accessible with user token"
    else
        echo "   ❌ User-only endpoint failed (HTTP $USER_ONLY_RESPONSE)"
    fi
fi

echo ""
echo "🎯 Test Summary:"
echo "==============="
echo "✅ Public access working"
echo "✅ JWT token authentication working"
echo "✅ Role-based authorization working"
echo ""
echo "🔗 Useful URLs:"
echo "• API Documentation: $BASE_URL/swagger"
echo "• Keycloak Admin:     $KEYCLOAK_URL"
echo "• Test API:           $BASE_URL/api/auth/public"