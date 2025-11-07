#!/usr/bin/env bash
set -euo pipefail

echo "🧹 Reset stack"
docker compose down -v || true

echo "🔨 Build images"
docker compose build --no-cache

echo "🚀 Bring up postgres + flyway + api"
docker compose up -d postgres
# wait pg ready
echo "⏳ Waiting for Postgres..."
for i in {1..30}; do
  if docker exec gold-tracker-postgres pg_isready -U gold -d gold >/dev/null 2>&1; then
    echo "✅ Postgres ready"; break
  fi
  sleep 2
done

docker compose up -d flyway
# wait flyway finished
echo "⏳ Waiting for Flyway to finish..."
for i in {1..30}; do
  st=$(docker inspect -f '{{.State.Status}}' gold-tracker-flyway || echo "")
  if [ "$st" = "exited" ]; then echo "✅ Flyway migrated"; break; fi
  sleep 2
done

echo "🧪 Running pgTAP tests"
docker compose run --rm pgtap-runner

echo "🚀 Starting API"
docker compose up -d api

echo "⏳ Waiting for API health..."
for i in {1..20}; do
  if curl -sf http://localhost:8080/healthz >/dev/null; then
    echo "✅ API healthy"
    break
  fi
  sleep 2
done

echo "📌 Smoke tests"
echo "→ /healthz";      curl -sf http://localhost:8080/healthz | jq . || true
echo "→ latest (ring)"; curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq . || true
echo "→ changes";       curl -sf "http://localhost:8080/api/prices/changes?kind=ring" | jq . || true
echo "→ history 30d";   curl -sf "http://localhost:8080/api/prices/history?kind=ring&days=30" | jq . || true

# minimal assertions (fail fast if empty)
test "$(curl -sf http://localhost:8080/healthz | jq -r '.status')" = "healthy"
items_count=$(curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq '.items | length' || echo "0")
if [ "$items_count" -ge 1 ]; then
  dir=$(curl -sf "http://localhost:8080/api/prices/changes?kind=ring" | jq -r '.items[0].direction // "flat"')
  case "$dir" in up|down|flat) :;; *) echo "direction invalid: $dir"; exit 1;; esac
else
  echo "⚠️  No price data found (expected if V4 seed not run or DB empty)"
fi

echo "✅ All Docker integration checks passed"

echo "🧾 API logs tail"
docker logs --tail=80 gold-tracker-api || true

echo "🧹 Cleanup"
docker compose down -v

