#!/bin/bash
# Script to test alerts and telegram notifications

set -e

API_URL="${API_URL:-http://localhost:8080}"
NAMESPACE="${NAMESPACE:-gold-dev}"

echo "=== Testing Alerts & Telegram Integration ==="
echo ""

# Check if running in K8s or local
if kubectl get namespace "$NAMESPACE" &>/dev/null; then
  echo "📦 Running in Kubernetes (namespace: $NAMESPACE)"
  API_URL="http://localhost:8080"  # Assuming port-forward
  echo "Make sure to port-forward: kubectl -n $NAMESPACE port-forward svc/gold-tracker-api 8080:8080"
  echo ""
fi

echo "1. Testing /admin/alerts/evaluate (should return alerts without sending)..."
curl -s -X POST "${API_URL}/admin/alerts/evaluate" | jq .
echo ""

echo "2. Testing /admin/alerts/dispatch (will send to Telegram if configured)..."
DISPATCH_RESULT=$(curl -s -X POST "${API_URL}/admin/alerts/dispatch")
echo "$DISPATCH_RESULT" | jq .
echo ""

SENT=$(echo "$DISPATCH_RESULT" | jq -r '.sent // 0')
TOTAL=$(echo "$DISPATCH_RESULT" | jq -r '.total // 0')

if [ "$SENT" -gt 0 ]; then
  echo "✅ Successfully sent $SENT alerts to Telegram!"
else
  echo "ℹ️  No alerts sent (total candidates: $TOTAL)"
  echo "   This is normal if:"
  echo "   - No price jumps detected"
  echo "   - No scraper errors"
  echo "   - Data is fresh"
fi

echo ""
echo "3. Testing /admin/brief/today (daily brief)..."
curl -s -X POST "${API_URL}/admin/brief/today" | jq .
echo ""

echo "✅ Test completed!"
echo ""
echo "Check your Telegram bot for messages!"

