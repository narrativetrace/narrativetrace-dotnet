#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Gate grader for add-narrative-tracing/existing-service (B-92). The fixture has tests, so the first
# trace is a test run through the xUnit integration and it leaves files. World state only: the grader
# runs `dotnet test` itself and looks for the files; nothing the agent says counts.
set -euo pipefail

: "${PROJECT_DIR:?PROJECT_DIR must point at the trial fixture copy}"
cd "$PROJECT_DIR"

test_log="$(mktemp)"
if ! dotnet test --nologo --logger "console;verbosity=normal" >"$test_log" 2>&1; then
  echo "FAIL: 'dotnet test' did not exit 0 under \$PROJECT_DIR" >&2
  cat "$test_log" >&2
  rm -f "$test_log"
  exit 1
fi
if ! grep -qE 'Passed .*Placing_an_order_confirms_it_for_the_customer' "$test_log"; then
  echo "FAIL: the fixture's own test no longer passes (or was removed)" >&2
  cat "$test_log" >&2
  rm -f "$test_log"
  exit 1
fi
rm -f "$test_log"

# The integration's output directory: <output-dir>/traces/<Class>/<slug>.md and
# <output-dir>/structural/<Class>/<slug>.nt, <output-dir> = TestResults/narrativetrace by default.
if ! find . -path '*/narrativetrace/traces/*' -name '*.md' -print -quit | grep -q .; then
  echo "FAIL: no .md trace file under a narrativetrace/traces/ output directory after 'dotnet test'" >&2
  exit 1
fi
if ! find . -path '*/narrativetrace/structural/*' -name '*.nt' -not -name '*.received.nt' -print -quit | grep -q .; then
  echo "FAIL: no .nt trace file under a narrativetrace/structural/ output directory after 'dotnet test'" >&2
  exit 1
fi

doctor_report="$(mktemp)"
dotnet tool run dotnet-narrativetrace doctor --json >"$doctor_report" 2>&1 || true
if ! grep -q '"findings"' "$doctor_report" && command -v dotnet-narrativetrace >/dev/null 2>&1; then
  dotnet-narrativetrace doctor --json >"$doctor_report" 2>&1 || true
fi
if ! grep -q '"findings"' "$doctor_report"; then
  echo "FAIL: doctor CLI produced no report" >&2
  cat "$doctor_report" >&2
  rm -f "$doctor_report"
  exit 1
fi
toolchain_failures="$(tr -d ' \n' <"$doctor_report" | grep -oE '"id":"toolchain\.[^"]+","passed":false' || true)"
if [[ -n "$toolchain_failures" ]]; then
  echo "FAIL: doctor toolchain findings do not hold:" >&2
  echo "$toolchain_failures" >&2
  rm -f "$doctor_report"
  exit 1
fi
rm -f "$doctor_report"

echo "PASS"
