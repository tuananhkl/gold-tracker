#!/bin/bash
# Script to test Telegram bot and get chat ID

set -e

BOT_TOKEN="${TELEGRAM_BOT_TOKEN:-8520040203:AAHzi3CQyoRdgHHsA5OLWnTqrWQ6XidjrtY}"
API_URL="https://api.telegram.org/bot${BOT_TOKEN}"

echo "=== Testing Telegram Bot ==="
echo "Bot Token: ${BOT_TOKEN:0:20}..."

# Get bot info
echo ""
echo "1. Getting bot info..."
curl -s "${API_URL}/getMe" | jq .

# Get updates (to see recent messages and extract chat ID)
echo ""
echo "2. Getting recent updates (send /start to your bot first)..."
UPDATES=$(curl -s "${API_URL}/getUpdates")
echo "$UPDATES" | jq .

# Extract chat ID from updates
CHAT_ID=$(echo "$UPDATES" | jq -r '.result[-1].message.chat.id // empty')
if [ -n "$CHAT_ID" ] && [ "$CHAT_ID" != "null" ]; then
  echo ""
  echo "✅ Found Chat ID: $CHAT_ID"
  echo ""
  echo "To use this chat ID, set:"
  echo "  export TELEGRAM_DEFAULT_CHAT_ID=$CHAT_ID"
  echo "  or update k8s/base/telegram-secret.yaml"
else
  echo ""
  echo "⚠️  No chat ID found. Please send /start to your bot first, then run this script again."
fi

# Test sending a message if chat ID is provided
if [ -n "$TELEGRAM_DEFAULT_CHAT_ID" ]; then
  echo ""
  echo "3. Testing send message..."
  curl -s -X POST "${API_URL}/sendMessage" \
    -H "Content-Type: application/json" \
    -d "{\"chat_id\": \"$TELEGRAM_DEFAULT_CHAT_ID\", \"text\": \"🧪 Test message from Gold Tracker\"}" | jq .
  echo ""
  echo "✅ Test message sent!"
fi

