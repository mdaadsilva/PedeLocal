#!/usr/bin/env bash
# Runs every scenario once at PROFILE=smoke — meant for CI and quick local sanity checks before a
# real load/stress run. See docs/PERFORMANCE_TESTING.md.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")"

if ! command -v k6 >/dev/null 2>&1; then
  echo "k6 not found on PATH — see docs/PERFORMANCE_TESTING.md#instalação-do-k6" >&2
  exit 1
fi

export PROFILE=smoke
mkdir -p results

status=0
for scenario in scenarios/*.js; do
  name="$(basename "$scenario" .js)"
  echo "=== smoke: $name ==="
  if ! k6 run --summary-export "results/${name}-smoke-summary.json" "$scenario"; then
    echo "!!! smoke FAILED: $name" >&2
    status=1
  fi
done

exit $status
