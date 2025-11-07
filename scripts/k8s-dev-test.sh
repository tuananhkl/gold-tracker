#!/usr/bin/env bash
set -euo pipefail
NS=gold-dev

echo "🩺 /healthz"
curl -sf http://localhost:8080/healthz | jq .

echo "🧪 latest (may be empty)"
curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq .

echo "▶️  trigger one-shot DOJI scrape"
curl -sf -X POST "http://localhost:8080/admin/scrape/doji?mode=once" | jq .
sleep 3

echo "🧪 latest after scrape (should have >=1 item)"
COUNT=$(curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | tee /tmp/latest.json | jq '.items | length')
echo "items: $COUNT"
test "$COUNT" -ge 1 || { echo "❌ expected >=1 item"; kubectl -n ${NS} logs deploy/gold-tracker-api --tail=200 || true; exit 1; }

echo "🧪 snapshot"
curl -sf -X POST "http://localhost:8080/admin/snapshot/daily" | jq .

echo "🧪 changes"
curl -sf "http://localhost:8080/api/prices/changes?kind=ring" | jq .

echo "🧪 PVC persistence (restart postgres pod)"
PGPOD=$(kubectl -n ${NS} get pod -l app=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n ${NS} delete pod "$PGPOD" --wait=false
kubectl -n ${NS} rollout status statefulset/postgres --timeout=240s

echo "⌛ verifying API after Postgres restart"
for i in {1..10}; do
  if curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq .; then
    break
  fi
  sleep 2
  if [ $i -eq 10 ]; then
    echo "❌ API did not recover after Postgres restart"
    kubectl -n ${NS} logs deploy/gold-tracker-api --tail=200 || true
    exit 1
  fi
done

echo "✅ all k8s dev tests passed"
