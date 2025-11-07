#!/usr/bin/env bash
set -euo pipefail

echo "🩺 health"
curl -sf http://localhost:8080/healthz | jq .

echo "🧪 latest (may be empty if no scraper run)"
curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq .

echo "🧪 trigger one-shot scrape"
curl -sf -X POST "http://localhost:8080/admin/scrape/doji?mode=once" | jq .
sleep 2

echo "🧪 latest after scrape"
curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq .
test "$(curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq '.items | length')" -ge 1

echo "🧪 trigger snapshot"
curl -sf -X POST "http://localhost:8080/admin/snapshot/daily" | jq .

echo "🧪 changes"
curl -sf "http://localhost:8080/api/prices/changes?kind=ring" | jq .

echo "🧪 PVC persistence check (restart pg pod)"
PGPOD=$(kubectl -n gold-dev get pod -l app=postgres -o jsonpath='{.items[0].metadata.name}')
kubectl -n gold-dev delete pod "$PGPOD" --wait=false
kubectl -n gold-dev rollout status statefulset/postgres --timeout=120s
# quick query via api again
curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq .

echo "✅ all k8s dev tests passed"

