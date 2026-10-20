#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Gate grader for add-narrative-tracing/happy-path — the NO-TESTS premise (B-92). The empty project has
# no test runner and the page must not add one, so the first trace is the program's own console output.
# The grader runs the program itself and reads what it printed and how it exited: world state, never
# output-text equality, and nothing about files — the program writes none by design.
# Invoked with PROJECT_DIR set to the trial's working copy of the fixture, after the agent's turn ends.
set -euo pipefail

: "${PROJECT_DIR:?PROJECT_DIR must point at the trial fixture copy}"

cd "$PROJECT_DIR"

# The agent may have nested a fresh console app in a subdirectory; run where NarrativeTrace is declared.
mapfile -t csproj_files < <(find . -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*')
declaring_csproj=""
for candidate in "${csproj_files[@]}"; do
  if grep -qE 'Include="[^"]*NarrativeTrace[^"]*"' "$candidate"; then
    declaring_csproj="$candidate"
    break
  fi
done
if [[ -z "$declaring_csproj" ]]; then
  echo "FAIL: no *.csproj under \$PROJECT_DIR declares a NarrativeTrace package reference" >&2
  exit 1
fi
cd "$(dirname "$declaring_csproj")"

dotnet build --nologo -v quiet

run_exit=0
output="$(dotnet run --no-build 2>&1)" || run_exit=$?
if [[ "$run_exit" -ne 0 ]]; then
  echo "FAIL: the program did not exit 0 (exit $run_exit)" >&2
  echo "$output" >&2
  exit 1
fi
if ! grep -qE 'trace: .+ \([0-9a-f]{7}\)' <<<"$output"; then
  echo "FAIL: no 'trace: <name> (<id>)' line in program output" >&2
  exit 1
fi
if ! grep -qE '^[[:space:]]*(└──|├──) .+\(.*\) → ' <<<"$output"; then
  echo "FAIL: the trace shows no traced call line ('<Type>.<Method>(...) → <result>')" >&2
  exit 1
fi

doctor_report="$(mktemp)"
dotnet tool run dotnet-narrativetrace doctor --json >"$doctor_report" 2>&1 || true
if ! grep -q '"findings"' "$doctor_report"; then
  echo "FAIL: doctor CLI produced no report" >&2
  cat "$doctor_report" >&2
  rm -f "$doctor_report"
  exit 1
fi
toolchain_failures="$(tr -d ' \n' <"$doctor_report" | grep -oE '"id":"toolchain\.[^"]+","passed":false' || true)"
rm -f "$doctor_report"
if [[ -n "$toolchain_failures" ]]; then
  echo "FAIL: doctor toolchain findings do not hold:" >&2
  echo "$toolchain_failures" >&2
  exit 1
fi

echo "PASS"
