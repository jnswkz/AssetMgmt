#!/usr/bin/env bash
# =============================================================
# One-shot database initializer for docker-compose.
# Runs the full DB-first init script + the disposal migration,
# but only once — guarded on whether AssetMgmt already has users.
# =============================================================
set -euo pipefail

SQLCMD=(/opt/mssql-tools18/bin/sqlcmd -S db -U sa -P "$SA_PASSWORD" -C)

echo "db-init: waiting for SQL Server to accept connections..."
for _ in $(seq 1 60); do
  if "${SQLCMD[@]}" -Q "SELECT 1" >/dev/null 2>&1; then
    break
  fi
  sleep 2
done

# Idempotency guard: if AssetMgmt exists and already has seeded users, do nothing.
# (The SQL seed INSERTs and the disposal migration are not re-runnable.)
COUNT=$("${SQLCMD[@]}" -h -1 -W -Q \
  "SET NOCOUNT ON; IF DB_ID('AssetMgmt') IS NULL SELECT 0 ELSE SELECT COUNT(*) FROM AssetMgmt.asset.users" \
  2>/dev/null | tr -d '[:space:]' || echo 0)

if [ "${COUNT:-0}" -gt 0 ]; then
  echo "db-init: AssetMgmt already initialized (${COUNT} users). Skipping."
  exit 0
fi

echo "db-init: running database-init.sql ..."
"${SQLCMD[@]}" -b -i /sql/database-init.sql

echo "db-init: running 001_asset_disposals.sql ..."
"${SQLCMD[@]}" -b -i /sql/001_asset_disposals.sql

echo "db-init: complete."
