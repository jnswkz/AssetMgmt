#!/usr/bin/env bash
# =============================================================
# One-shot database initializer for docker-compose.
# Runs the full DB-first init script + the disposal migration,
# but only once â€” guarded on whether AssetMgmt already has users.
# =============================================================
set -euo pipefail

if [ -x /opt/mssql-tools18/bin/sqlcmd ]; then
  SQLCMD_BIN=/opt/mssql-tools18/bin/sqlcmd
else
  SQLCMD_BIN=/opt/mssql-tools/bin/sqlcmd
fi

SQLCMD=("$SQLCMD_BIN" -S db -U sa -P "$SA_PASSWORD" -C)

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
  echo "db-init: AssetMgmt already initialized (${COUNT} users). Applying idempotent upgrades."
  for script in /sql/002_*.sql /sql/003_*.sql /sql/004_*.sql /sql/005_*.sql; do
    [ -f "$script" ] || continue
    echo "db-init: running $(basename "$script") ..."
    "${SQLCMD[@]}" -b -i "$script"
  done
  exit 0
fi

echo "db-init: running migration scripts ..."
for script in /sql/*.sql; do
  echo "db-init: running $(basename "$script") ..."
  "${SQLCMD[@]}" -b -i "$script"
done

echo "db-init: complete."
