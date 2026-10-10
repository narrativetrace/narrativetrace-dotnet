#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# World-state verifier for the PUBLISHED init prompt itself (Phase 3 milestone 5: the human-gated
# skills-install step). Shared by every add-narrative-tracing case — the two init-prompt cases call
# it directly, the two registry cases call it last, after grade_the_registry.sh has graded what only
# a registry case can be asked. Invoked with PROJECT_DIR set to the trial's working copy after the
# agent's turn, and $1 naming the service boundary the fixture defines. World-state, never text
# equality: grades exactly what the published prompt promises, nothing the agent's own report says.
set -euo pipefail

: "${PROJECT_DIR:?PROJECT_DIR must point at the trial fixture copy}"
cd "$PROJECT_DIR"

# The fixture's own service, per llms.txt's "Install and first trace" block / sixty-seconds.md —
# both start from the same IOrderService boundary. A near-miss identifier (a wrapper, a subtype,
# a differently-cased name) must not count.
EXPECTED_SERVICE="${1:-IOrderService}"

# 1. The project declares NarrativeTrace. An array (not a newline-joined string) so a path
# containing a space — a project named `dotnet new console -n "My App"`, say — is never
# word-split into two bogus arguments.
mapfile -t csproj_files < <(find . -name '*.csproj' -not -path '*/bin/*' -not -path '*/obj/*')
if [[ ${#csproj_files[@]} -eq 0 ]]; then
  echo "FAIL: no *.csproj found under \$PROJECT_DIR" >&2
  exit 1
fi
declaring_csproj=""
for candidate_csproj in "${csproj_files[@]}"; do
  if grep -qE 'Include="[^"]*NarrativeTrace[^"]*"' "$candidate_csproj"; then
    declaring_csproj="$candidate_csproj"
    break
  fi
done
if [[ -z "$declaring_csproj" ]]; then
  echo "FAIL: no *.csproj under \$PROJECT_DIR declares a NarrativeTrace package/project reference" >&2
  exit 1
fi

# cd into the directory holding the declaring project file: the agent may have nested a fresh
# console app in a subdirectory (`dotnet new console -n Hello && cd Hello`, per llms.txt), so
# dotnet run/test/tool run must execute there, not at the fixture root (milestone 0's first
# real trial: the empty-project case nested the app and the grader's root-level `dotnet run`
# found no trace line — this closes that harness gap).
cd "$(dirname "$declaring_csproj")"

# 2. The program runs; stdout carries a trace line naming the fixture's own service. Markdown
# emphasis/backticks are stripped before matching, and the match is on a whole identifier token
# (not a substring), so a near-miss name is rejected the same as a missing one.
output="$(dotnet run --no-build 2>&1 || true)"
if ! grep -qE 'trace: .+ \([0-9a-f]{7}\)' <<<"$output"; then
  echo "FAIL: no 'trace: <name> (<id>)' line in program output" >&2
  exit 1
fi
normalized="$(sed -E 's/[`*_]+//g' <<<"$output")"
if ! grep -oE '[A-Za-z_][A-Za-z0-9_]*' <<<"$normalized" | grep -qx "$EXPECTED_SERVICE"; then
  echo "FAIL: stdout names no '$EXPECTED_SERVICE' as a standalone identifier (a near-miss name doesn't count)" >&2
  exit 1
fi

# 3. A test asserting [REDACTED] exists, and the test run passes.
if ! grep -rlq '\[REDACTED\]' --include='*.cs' . 2>/dev/null; then
  echo "FAIL: no *.cs file under \$PROJECT_DIR asserts [REDACTED]" >&2
  exit 1
fi
if ! dotnet test --nologo -v quiet; then
  echo "FAIL: 'dotnet test' did not pass under \$PROJECT_DIR" >&2
  exit 1
fi

# 4. Step 3's human gate (Phase 3 milestone 5, D13): the new prompt step ("Install the CLI... then
# run init --dry-run and show me the diff... Run it for real only after I have seen the diff.")
# allows two faithful outcomes, graded by world state, never by the agent's own report:
#   - applied:   a page under .agents/skills/ carries our provenance line
#   - previewed: the CLI is available as a local tool, but the skills were never applied
tool_manifest=""
for candidate in .config/dotnet-tools.json dotnet-tools.json; do
  if [[ -f "$candidate" ]]; then
    tool_manifest="$candidate"
    break
  fi
done
if grep -rlq -- '<!-- installed by narrativetrace init from ' .agents/skills 2>/dev/null; then
  step3_outcome="applied"
elif [[ -n "$tool_manifest" ]] && grep -qi 'narrativetrace.cli' "$tool_manifest"; then
  step3_outcome="previewed"
else
  echo "FAIL: step 3's human gate was not met — neither does .agents/skills/ carry our provenance" \
    "nor is the NarrativeTrace CLI installed as a local tool (.config/dotnet-tools.json or" \
    "dotnet-tools.json)" >&2
  exit 1
fi

# 5. The doctor report is FULLY green except, on the previewed branch only, config.skills-installed
# (D13: that finding is graded on the applied branch only — on the previewed branch it legitimately
# reads "not installed", and that must not fail the trial). llms.txt's own "Doctor" section
# documents the try-local-then-global fallback, so a faithful agent run legitimately ends up on
# either form. The doctor CLI's own EXIT CODE is not the gate here (D13's exemption means exit 1 is
# legitimate on the previewed branch) — presence of a well-formed report is: only escalate to the
# global-install fallback when the local-manifest form produced no report at all.
doctor_report="$(mktemp)"
dotnet tool run dotnet-narrativetrace doctor --json >"$doctor_report" 2>&1 || true
if ! grep -q '"findings"' "$doctor_report"; then
  if command -v dotnet-narrativetrace >/dev/null 2>&1; then
    dotnet-narrativetrace doctor --json >"$doctor_report" 2>&1 || true
  fi
fi
if ! grep -q '"findings"' "$doctor_report"; then
  echo "FAIL: doctor CLI produced no report (tried 'dotnet tool run' and the global-install fallback)" >&2
  cat "$doctor_report" >&2
  rm -f "$doctor_report"
  exit 1
fi
normalized_doctor="$(tr -d ' \n' < "$doctor_report")"
other_failures="$(grep -oE '"id":"[^"]+","passed":false' <<<"$normalized_doctor" \
  | grep -v '"id":"config.skills-installed"' || true)"
if [[ -n "$other_failures" ]]; then
  echo "FAIL: doctor report has failing checks other than config.skills-installed:" >&2
  echo "$other_failures" >&2
  cat "$doctor_report" >&2
  rm -f "$doctor_report"
  exit 1
fi
if [[ "$step3_outcome" == "applied" ]] \
  && ! grep -q '"id":"config.skills-installed","passed":true' <<<"$normalized_doctor"; then
  echo "FAIL: config.skills-installed is not green even though the skills were applied" >&2
  cat "$doctor_report" >&2
  rm -f "$doctor_report"
  exit 1
fi
rm -f "$doctor_report"

# 6. No .received.nt anywhere in the trial (searched from the fixture root — the absolute
# PROJECT_DIR env var, since step 1 above may have changed the working directory).
if find "$PROJECT_DIR" -name '*.received.nt' -print -quit | grep -q .; then
  echo "FAIL: a .received.nt file exists under \$PROJECT_DIR" >&2
  exit 1
fi

echo "PASS ($step3_outcome)"
