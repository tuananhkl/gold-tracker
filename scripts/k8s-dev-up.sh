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
docker build -t gold-tracker-ui:dev -f ui-code-ref/Dockerfile ./ui-code-ref

echo "📦 Applying Kustomize overlay"
kubectl apply -k k8s/overlays/dev

echo "⏳ Waiting for PostgreSQL"
kubectl -n ${NS} rollout status statefulset/postgres --timeout=240s

echo "⏳ Waiting for Flyway job"
kubectl -n ${NS} wait --for=condition=Complete --timeout=420s job/flyway-migrate

echo "⏳ Waiting for API deployment"
kubectl -n ${NS} rollout status deployment/gold-tracker-api --timeout=240s

echo "⏳ Waiting for UI deployment"
kubectl -n ${NS} rollout status deployment/gold-tracker-ui --timeout=240s

echo "🔌 Port-forward API 8080 (listening on 0.0.0.0)"
# Kill previous port-forward if any
pkill -f "kubectl.*port-forward.*gold-tracker-api.*8080:8080" 2>/dev/null || true

PORT_FWD_API_CMD="kubectl -n ${NS} port-forward --address 0.0.0.0 svc/gold-tracker-api 8080:8080"
nohup bash -c "$PORT_FWD_API_CMD" >/tmp/gold_pf_api.log 2>&1 &
PF_API_PID=$!
disown ${PF_API_PID} 2>/dev/null || true
sleep 2
for i in {1..30}; do
  if curl -sf http://localhost:8080/healthz >/dev/null 2>&1; then
    echo "✅ API healthy"
    break
  fi
  sleep 1
  [[ $i -eq 30 ]] && { echo "❌ API not healthy"; kubectl -n ${NS} get all -o wide; kubectl -n ${NS} logs deploy/gold-tracker-api --tail=200 || true; exit 1; }
done

echo "🔌 Port-forward UI 3000 (listening on 0.0.0.0)"
# Kill previous port-forward if any
pkill -f "kubectl.*port-forward.*gold-tracker-ui.*3000:80" 2>/dev/null || true

PORT_FWD_UI_CMD="kubectl -n ${NS} port-forward --address 0.0.0.0 svc/gold-tracker-ui 3000:80"
nohup bash -c "$PORT_FWD_UI_CMD" >/tmp/gold_pf_ui.log 2>&1 &
PF_UI_PID=$!
disown ${PF_UI_PID} 2>/dev/null || true
sleep 2
for i in {1..30}; do
  if curl -sf http://localhost:3000/ >/dev/null 2>&1; then
    echo "✅ UI healthy"
    break
  fi
  sleep 1
  [[ $i -eq 30 ]] && { echo "❌ UI not healthy"; kubectl -n ${NS} get all -o wide; kubectl -n ${NS} logs deploy/gold-tracker-ui --tail=200 || true; exit 1; }
done

echo "🔌 Port-forward PostgreSQL 5432 (listening on 0.0.0.0)"
# Kill previous port-forward if any
pkill -f "kubectl.*port-forward.*postgres.*5432:5432" 2>/dev/null || true

PORT_FWD_PG_CMD="kubectl -n ${NS} port-forward --address 0.0.0.0 svc/postgres 5432:5432"
nohup bash -c "$PORT_FWD_PG_CMD" >/tmp/gold_pf_pg.log 2>&1 &
PF_PG_PID=$!
disown ${PF_PG_PID} 2>/dev/null || true
sleep 2
for i in {1..30}; do
  if pg_isready -h localhost -p 5432 -U gold -d gold >/dev/null 2>&1 || nc -z localhost 5432 2>/dev/null; then
    echo "✅ PostgreSQL port-forward ready"
    break
  fi
  sleep 1
  [[ $i -eq 30 ]] && { echo "⚠️  PostgreSQL port-forward may not be ready (check manually)"; break; }
done

echo "✅ Cluster ready"
echo ""
echo "🔗 Service URLs (via port-forward):"
echo "  API:      http://localhost:8080"
echo "  UI:       http://localhost:3000"
echo "  Database: localhost:5432"
echo "    - Host:     localhost"
echo "    - Port:     5432"
echo "    - Database: gold"
echo "    - Username: gold"
echo "    - Password: gold"
echo ""
echo "📝 Note: Port-forwards are running in background. Check logs:"
echo "  API:      tail -f /tmp/gold_pf_api.log"
echo "  UI:       tail -f /tmp/gold_pf_ui.log"
echo "  Database: tail -f /tmp/gold_pf_pg.log"
echo ""
echo "💡 Connect to database:"
echo "  psql -h localhost -p 5432 -U gold -d gold"
