#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# dev.sh — Orchestrates the full Second Brain IA stack with automatic port cleaning.
#
# Usage:
#   ./dev.sh         # Cleans ports and runs docker-compose up
#   ./dev.sh --build # Cleans ports and runs docker-compose up --build
# ─────────────────────────────────────────────────────────────────────────────

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLEAN_SCRIPT="$SCRIPT_DIR/scripts/clean-ports.sh"

# Ensure the clean script is executable
chmod +x "$CLEAN_SCRIPT"

echo "🧹 Cleaning up project ports and old build assets..."
# 3000 (React), 8080 (API), 4222 (NATS), 8000 (DynamoDB), 6379 (Redis), 5050 (Legacy API)
$CLEAN_SCRIPT 3000 8080 4222 8000 6379 5050
rm -rf client/dist

echo "🐳 Checking Docker connection..."
if ! docker info > /dev/null 2>&1; then
  echo "❌ Error: Cannot connect to the Docker daemon."
  echo "👉 Please make sure Docker Desktop is open and running on your Mac."
  echo "💡 Tip: Try running 'docker context use desktop-linux' in your terminal."
  exit 1
fi

COMPOSE_FILE="docker-compose.yml"
if [[ "$*" == *"--simple"* ]]; then
  COMPOSE_FILE="docker-compose.simple.yml"
  # Remove --simple from args so it doesn't break docker compose
  shift
fi

echo "🚀 Launching the full stack via Docker Compose (using $COMPOSE_FILE)..."
docker compose -f "$COMPOSE_FILE" up --build "$@"
