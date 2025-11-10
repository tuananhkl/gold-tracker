#!/usr/bin/env bash
set -euo pipefail

NS=gold-dev

echo "🔧 Enabling ingress addon (if not already)..."
minikube addons enable ingress >/dev/null 2>&1 || true

echo "📦 Applying namespace..."
kubectl apply -f k8s/base/namespace.yaml

echo "🔐 Applying Elasticsearch secret..."
kubectl -n ${NS} apply -f k8s/efk-remote/secret-elastic.yaml

echo "🛂 Applying Filebeat RBAC..."
kubectl -n ${NS} apply -f k8s/efk-remote/filebeat-rbac.yaml

echo "⚙️  Applying Filebeat ConfigMap..."
kubectl -n ${NS} apply -f k8s/efk-remote/filebeat-configmap.yaml

echo "🐝 Deploying Filebeat DaemonSet..."
kubectl -n ${NS} apply -f k8s/efk-remote/filebeat-daemonset.yaml

echo "⏳ Waiting for Filebeat pods..."
kubectl -n ${NS} rollout status ds/filebeat --timeout=120s

echo "🚀 Applying app Deployment/Service..."
kubectl -n ${NS} apply -f k8s/base/app/

echo "⏳ Waiting for API deployment..."
kubectl -n ${NS} rollout status deploy/gold-tracker-api --timeout=180s

echo "✅ Done. Try:"
echo "  kubectl -n ${NS} get pods,svc"
echo "  minikube service -n ${NS} gold-tracker-api --url"


