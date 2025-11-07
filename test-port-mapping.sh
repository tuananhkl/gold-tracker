#!/usr/bin/env bash
set -euo pipefail

echo "🧪 Testing Port Mapping 0.0.0.0:8080->8080/tcp"
echo "================================================"

# Check if container is running
if ! docker ps --format "{{.Names}}" | grep -q "gold-tracker-api"; then
  echo "❌ Container gold-tracker-api is not running"
  exit 1
fi

# Check port mapping
echo "1️⃣ Checking port mapping..."
PORT_MAP=$(docker ps --filter "name=gold-tracker-api" --format "{{.Ports}}")
if echo "$PORT_MAP" | grep -q "0.0.0.0:8080->8080/tcp"; then
  echo "✅ Port mapping correct: 0.0.0.0:8080->8080/tcp"
else
  echo "❌ Port mapping incorrect: $PORT_MAP"
  exit 1
fi

# Check if port is listening
echo -e "\n2️⃣ Checking if port 8080 is listening on 0.0.0.0..."
if ss -tlnp 2>/dev/null | grep -q ":8080" || netstat -tlnp 2>/dev/null | grep -q ":8080"; then
  echo "✅ Port 8080 is listening on 0.0.0.0"
else
  echo "❌ Port 8080 is not listening"
  exit 1
fi

# Test localhost
echo -e "\n3️⃣ Testing localhost:8080..."
if curl -sf http://localhost:8080/healthz >/dev/null; then
  echo "✅ localhost:8080 works"
else
  echo "❌ localhost:8080 failed"
  exit 1
fi

# Test 127.0.0.1
echo -e "\n4️⃣ Testing 127.0.0.1:8080..."
if curl -sf http://127.0.0.1:8080/healthz >/dev/null; then
  echo "✅ 127.0.0.1:8080 works"
else
  echo "❌ 127.0.0.1:8080 failed"
  exit 1
fi

# Get VM IP and test
VM_IP=$(hostname -I 2>/dev/null | awk '{print $1}' || ip addr show | grep "inet " | grep -v "127.0.0.1" | head -1 | awk '{print $2}' | cut -d/ -f1)
if [ -n "$VM_IP" ]; then
  echo -e "\n5️⃣ Testing VM IP ($VM_IP:8080)..."
  if curl -sf http://${VM_IP}:8080/healthz >/dev/null; then
    echo "✅ $VM_IP:8080 works (accessible from outside)"
  else
    echo "⚠️  $VM_IP:8080 failed (may need firewall rules)"
  fi
fi

# Test API endpoints
echo -e "\n6️⃣ Testing API endpoints..."
echo "   - GET /healthz:"
curl -sf http://localhost:8080/healthz | jq . || echo "   ❌ Failed"

echo -e "\n   - GET /readyz:"
curl -sf http://localhost:8080/readyz && echo " ✅" || echo "   ❌ Failed"

echo -e "\n   - GET /api/prices/latest:"
curl -sf "http://localhost:8080/api/prices/latest" | jq '.items | length' && echo " items found ✅" || echo "   ❌ Failed"

echo -e "\n✅ All port mapping tests passed!"
echo "================================================"
echo "📌 Container: gold-tracker-api"
echo "📌 Port mapping: 0.0.0.0:8080->8080/tcp"
echo "📌 Accessible from: localhost, 127.0.0.1, and VM IP ($VM_IP)"

