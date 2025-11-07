#!/usr/bin/env bash
set -euo pipefail

echo "🧹 Reset"
docker compose down -v || true

echo "🔨 Build"
docker compose build

echo "🚀 Up postgres + flyway + api"
docker compose up -d postgres

# wait pg healthy
for i in {1..30}; do
  if docker exec gold-tracker-postgres pg_isready -U gold -d gold >/dev/null 2>&1; then break; fi
  sleep 2
done

docker compose up -d flyway

# wait flyway exit
for i in {1..30}; do
  s=$(docker inspect -f '{{.State.Status}}' gold-tracker-flyway || echo "")
  [ "$s" = "exited" ] && break
  sleep 2
done

# run API with scraper disabled for deterministic control
docker compose up -d api

# wait api
for i in {1..20}; do
  curl -sf http://localhost:8080/healthz >/dev/null && break
  sleep 2
done

echo "🧪 Trigger one-shot DOJI scrape"
curl -sf -X POST "http://localhost:8080/admin/scrape/doji?mode=once" | jq .

echo "📈 Check latest ring"
curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq .

test "$(curl -sf "http://localhost:8080/api/prices/latest?kind=ring" | jq '.items | length')" -ge 1

echo "📆 Trigger snapshot today"
curl -sf -X POST "http://localhost:8080/admin/snapshot/daily" | jq .

echo "🔁 Check changes"
curl -sf "http://localhost:8080/api/prices/changes?kind=ring" | jq .

echo "✅ Scraper + Snapshot Docker test passed"

echo "🧹 Down"
docker compose down -v

