#!/usr/bin/env bash
set -euo pipefail

CLUSTER_NAME=${CLUSTER_NAME:-gold-dev}

if ! kind get clusters | grep -q "^${CLUSTER_NAME}$"; then
  echo "⛏️  creating kind cluster: $CLUSTER_NAME"
  kind create cluster --name "$CLUSTER_NAME"
fi

echo "🛠️  building docker images"
docker build -t gold-tracker-api:dev -f src/GoldTracker.Api/Dockerfile .
docker build -t gold-tracker-flyway:dev -f docker/flyway/Dockerfile .

echo "📦 loading images into kind"
kind load docker-image gold-tracker-api:dev --name "$CLUSTER_NAME"
kind load docker-image gold-tracker-flyway:dev --name "$CLUSTER_NAME"

echo "🚀 applying kustomize overlay"
kubectl apply -k k8s/overlays/dev

echo "⏳ waiting for postgres"
kubectl -n gold-dev rollout status statefulset/postgres --timeout=120s

echo "⏳ running flyway migration job"
kubectl -n gold-dev wait --for=condition=Complete --timeout=180s job/flyway-migrate

echo "⏳ waiting for api"
kubectl -n gold-dev rollout status deployment/gold-tracker-api --timeout=120s

echo "🔌 port-forward 8080"
kubectl -n gold-dev port-forward deploy/gold-tracker-api 8080:8080 >/tmp/gold_pf.log 2>&1 &
sleep 3

echo "✅ cluster up"

