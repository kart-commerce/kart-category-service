#!/usr/bin/env bash
# Usage: scripts/seed-categories.sh <count> [seeder options...]
#   e.g. scripts/seed-categories.sh 500
#        scripts/seed-categories.sh 100000 --batch-size 5000 --seed 42
#
# Loads .env the same way scripts/migrate.sh does, then runs the CategorySeeder
# console tool (tools/CategorySeeder) against CATEGORY_DB_CONNECTION_STRING.
# Run scripts/migrate.sh first if the categories table doesn't exist yet.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [ -f .env ]; then
  set -a
  source .env
  set +a
fi

dotnet run --project tools/CategorySeeder -- "$@"
