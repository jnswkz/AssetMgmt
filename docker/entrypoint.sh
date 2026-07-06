#!/usr/bin/env bash
set -euo pipefail

# Start SQL Server in the background
/opt/mssql/bin/sqlservr &

# Wait for SQL Server to be ready
echo "Waiting for SQL Server to accept connections..."
for i in {1..60}; do
  if /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1" -C >/dev/null 2>&1; then
    echo "SQL Server is up!"
    break
  fi
  sleep 2
done

# Run all .sql scripts in /migrations (alphabetical order)
if compgen -G "/migrations/*.sql" > /dev/null; then
  echo "Running migration scripts..."
  for script in /migrations/*.sql; do
    echo "Executing $script"
    /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -i "$script" -b -C
  done
  echo "All migrations completed."
else
  echo "No migration scripts found."
fi

# Launch the .NET API
echo "Starting AssetMgmt API..."
exec dotnet /app/AssetMgmt.dll
