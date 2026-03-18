#!/usr/bin/env bash
# ─────────────────────────────────────────────────────────────────────────────
# clean-ports.sh — Kills processes listening on the specified ports.
#
# Usage:
#   ./clean-ports.sh 3000 8080 4222
# ─────────────────────────────────────────────────────────────────────────────

kill -9 $(lsof -t -i:3000)
kill -9 $(lsof -t -i:5050)

