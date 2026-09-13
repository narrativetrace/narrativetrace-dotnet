#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Gate grader for narrativetrace-doctor/happy-path.
# Not yet wired into a runner — no trial has been run against this. When one
# exists, it invokes this script with PROJECT_DIR set to the trial's working
# copy of the fixture, after the agent's turn ends.
set -euo pipefail

: "${PROJECT_DIR:?PROJECT_DIR must point at the trial's fixture copy}"

report="$(dotnet tool run dotnet-narrativetrace doctor --json --dir "$PROJECT_DIR" || true)"

if [[ -z "$report" ]]; then
  echo "FAIL: doctor CLI produced no report" >&2
  exit 1
fi

if ! grep -q '"id":"trap.redaction-proof","passed":true' <<<"$(tr -d ' \n' <<<"$report")"; then
  echo "FAIL: trap.redaction-proof did not pass on the final doctor run" >&2
  exit 1
fi

echo "PASS"
