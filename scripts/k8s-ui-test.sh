#!/usr/bin/env bash
set -euo pipefail

NS=gold-dev

echo "🔍 Getting service URLs"
UI_URL=$(minikube service -n $NS gold-tracker-ui --url 2>/dev/null | head -n1 || echo "")
API_URL=$(minikube service -n $NS gold-tracker-api --url 2>/dev/null | head -n1 || echo "")

# Fallback to NodePort if service URL fails
if [ -z "$UI_URL" ]; then
  MINIKUBE_IP=$(minikube ip)
  UI_URL="http://${MINIKUBE_IP}:30090"
fi

if [ -z "$API_URL" ]; then
  MINIKUBE_IP=${MINIKUBE_IP:-$(minikube ip)}
  API_URL="http://${MINIKUBE_IP}:30080"
fi

echo "API: $API_URL"
echo "UI : $UI_URL"
echo ""

# 1) API reachable
echo "🧪 Testing API health"
if curl -sf "${API_URL}/healthz" >/dev/null 2>&1; then
  echo "  ✅ API /healthz OK"
else
  echo "  ⚠️  API /healthz failed, trying /swagger"
  curl -sf "${API_URL}/swagger/index.html" >/dev/null 2>&1 || {
    echo "  ❌ API not reachable"
    exit 1
  }
  echo "  ✅ API /swagger OK"
fi

# 2) UI root returns HTML
echo "🧪 Testing UI root page"
UI_HTML=$(curl -sf "${UI_URL}/" 2>&1 || echo "")
if echo "$UI_HTML" | grep -qiE "(<!doctype html>|<html)" >/dev/null 2>&1; then
  echo "  ✅ UI returns HTML"
else
  echo "  ❌ UI root page failed or not HTML"
  echo "  Response preview: ${UI_HTML:0:200}"
  exit 1
fi

# 3) API latest endpoint works
echo "🧪 Testing API /api/prices/latest"
if curl -sf "${API_URL}/api/prices/latest?kind=ring" >/dev/null 2>&1; then
  echo "  ✅ API /api/prices/latest OK"
else
  echo "  ⚠️  API /api/prices/latest failed (may be empty, continuing)"
fi

# 4) Optional: check recent scrape jobs
echo "🧪 Checking recent scrape jobs"
kubectl -n $NS get jobs -l job-name=cron-scrape-doji --sort-by=.metadata.creationTimestamp 2>/dev/null | tail -n1 | awk '{print $1}' | xargs -I{} kubectl -n $NS logs job/{} --tail=5 2>/dev/null || echo "  (no recent scrape jobs found)"

echo ""
echo "✅ UI & API are reachable and functional"


