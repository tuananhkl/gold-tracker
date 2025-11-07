#!/usr/bin/env bash
set -euo pipefail

NS=gold-dev
API_IMG=gold-tracker-api:dev
FLY_IMG=gold-tracker-flyway:dev

echo "🛑 Stopping docker-compose (if any)"
docker compose down -v || true

echo "🚀 Ensuring minikube is up"
minikube status || minikube start

echo "🐳 Switching Docker to minikube daemon"
eval "$(minikube -p minikube docker-env)"

echo "🛠️  Building images into minikube Docker"
docker build -t ${API_IMG} -f src/GoldTracker.Api/Dockerfile .
docker build -t ${FLY_IMG} -f docker/flyway/Dockerfile .

echo "📦 Applying Kustomize overlay"
kubectl apply -k k8s/overlays/dev

echo "⏳ Waiting for PostgreSQL"
kubectl -n ${NS} rollout status statefulset/postgres --timeout=240s

echo "⏳ Waiting for Flyway job"
kubectl -n ${NS} wait --for=condition=Complete --timeout=420s job/flyway-migrate

echo "⏳ Waiting for API deployment"
kubectl -n ${NS} rollout status deployment/gold-tracker-api --timeout=240s

echo "🔌 Port-forward API 8080"
# Kill previous port-forward if any
pkill -f "kubectl.*port-forward.*gold-tracker-api.*8080:8080" 2>/dev/null || true
kubectl -n ${NS} port-forward deploy/gold-tracker-api 8080:8080 >/tmp/gold_pf.log 2>&1 &
for i in {1..30}; do
  if curl -sf http://localhost:8080/healthz >/dev/null 2>&1; then
    echo "✅ API healthy"
    break
  fi
  sleep 1
  [[ $i -eq 30 ]] && { echo "❌ API not healthy"; kubectl -n ${NS} get all -o wide; kubectl -n ${NS} logs deploy/gold-tracker-api --tail=200 || true; exit 1; }
done

echo "✅ Cluster ready"
