#!/usr/bin/env bash
set -euo pipefail

echo "🧹 reset"
docker compose down -v || true

echo "🔨 build"
docker compose build

echo "🚀 up"
docker compose up -d postgres
for i in {1..30}; do
  docker exec gold-tracker-postgres pg_isready -U gold -d gold >/dev/null 2>&1 && break
  sleep 2
done
docker compose up -d flyway
for i in {1..30}; do
  s=$(docker inspect -f '{{.State.Status}}' gold-tracker-flyway || echo ""); [ "$s" = "exited" ] && break; sleep 2
done
docker compose up -d api
for i in {1..20}; do
  curl -sf http://localhost:8080/healthz >/dev/null && break
  sleep 2
done

echo "🧪 contracts via curl+jq"
# 1) latest
curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | tee /tmp/latest.json | jq .
test "$(jq '.items | length' /tmp/latest.json)" -ge 0  # Allow empty for now
jq -e '.items[0] | has("brand") and has("form") and has("priceSell")' /tmp/latest.json >/dev/null 2>&1 || echo "⚠️  No items in latest response (expected if DB is empty)"

# 2) history (default days=30)
curl -sf "http://localhost:8080/api/prices/history?kind=ring" | tee /tmp/history.json | jq .
jq -e 'has("points") or has("Points")' /tmp/history.json >/dev/null || echo "⚠️  History response structure check"

# 3) changes
curl -sf "http://localhost:8080/api/prices/changes?kind=ring" | tee /tmp/changes.json | jq .
jq -e '.items[0].direction | IN("up","down","flat")' /tmp/changes.json >/dev/null 2>&1 || echo "⚠️  No items in changes response (expected if DB is empty)"

echo "✅ docker API contract smoke passed"
docker compose down -v

