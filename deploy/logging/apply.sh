#!/usr/bin/env bash
set -euo pipefail

echo "🚀 Setting up production logging stack..."

# 1. Create namespace
echo "📦 Creating namespace..."
kubectl apply -f namespace.yaml

# 2. Create secret (if not exists)
if ! kubectl -n logging get secret es-basic-auth >/dev/null 2>&1; then
  echo "🔐 Creating Elasticsearch secret..."
  kubectl -n logging create secret generic es-basic-auth \
    --from-literal=ES_HOST=http://192.168.31.156:9200 \
    --from-literal=ES_USER=elastic \
    --from-literal=ES_PASS='Tuananh123.'
  echo "✅ Secret created"
else
  echo "✅ Secret already exists"
fi

# 3. Apply ConfigMap
echo "📋 Applying Fluent Bit ConfigMap..."
kubectl apply -f configmap-fluent-bit.yaml

# 4. Apply DaemonSet
echo "🚀 Deploying Fluent Bit DaemonSet..."
kubectl apply -f daemonset-fluent-bit.yaml

# 5. Wait for pods
echo "⏳ Waiting for Fluent Bit pods to be ready..."
kubectl -n logging wait --for=condition=ready pod -l app=fluent-bit --timeout=120s || true

# 6. Show status
echo ""
echo "📊 Fluent Bit Status:"
kubectl -n logging get pods -l app=fluent-bit

echo ""
echo "✅ Logging stack deployed!"
echo ""
echo "🧪 Test logging:"
echo "  curl -H 'x-trace-id: test-123' http://testcursor.derrick.local/gold/api/test/logging"
echo ""
echo "📊 Check logs in Kibana:"
echo "  Index pattern: logs-dev-*"
echo "  Search: traceId:\"test-123\""

