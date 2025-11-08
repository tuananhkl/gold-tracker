#!/usr/bin/env bash
set -euo pipefail

NS=gold-dev

echo "🧪 Testing PostgreSQL connection via port-forward (localhost:5432)"
echo ""

# Check if port-forward is running
if ! pgrep -f "kubectl.*port-forward.*postgres.*5432:5432" >/dev/null; then
  echo "❌ PostgreSQL port-forward is not running!"
  echo "   Run: bash scripts/k8s-dev-up.sh"
  exit 1
fi

# Test connection using docker postgres client
echo "📊 Database Info:"
docker run --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold -c "SELECT version();" 2>&1 | grep -v "password" | tail -n 1

echo ""
echo "📋 Tables in 'gold' schema:"
docker run --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold -c "SELECT table_name FROM information_schema.tables WHERE table_schema = 'gold' AND table_type = 'BASE TABLE' ORDER BY table_name;" 2>&1 | grep -v "password" | tail -n +4 | head -n -2

echo ""
echo "📈 Data Statistics:"
echo "  Price Ticks:"
docker run --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold -t -c "SELECT COUNT(*) FROM gold.price_tick;" 2>&1 | grep -v "password" | xargs
echo "  Products:"
docker run --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold -t -c "SELECT COUNT(*) FROM gold.product;" 2>&1 | grep -v "password" | xargs
echo "  Daily Snapshots:"
docker run --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold -t -c "SELECT COUNT(*) FROM gold.daily_snapshot;" 2>&1 | grep -v "password" | xargs

echo ""
echo "🔍 Latest 5 price ticks:"
docker run --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold -c "SELECT id, product_id, price_buy, price_sell, effective_at, collected_at FROM gold.price_tick ORDER BY collected_at DESC LIMIT 5;" 2>&1 | grep -v "password" | tail -n +4 | head -n -2

echo ""
echo "✅ Database connection test passed!"
echo ""
echo "💡 To connect manually:"
echo "   docker run -it --rm --network host postgres:16 psql -h localhost -p 5432 -U gold -d gold"
echo ""
echo "   Or if you have psql installed:"
echo "   psql -h localhost -p 5432 -U gold -d gold"
echo "   Password: gold"

