#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Gate grader for add-narrative-tracing/happy-path.
# Not yet wired into a runner — no trial has been run against this. When one
# exists, it invokes this script with PROJECT_DIR set to the trial's working
# copy of the fixture, after the agent's turn ends.
set -euo pipefail

: "${PROJECT_DIR:?PROJECT_DIR must point at the trial's fixture copy}"

cd "$PROJECT_DIR"

dotnet build --nologo -v quiet

output="$(dotnet run --no-build 2>&1 || true)"
if ! grep -qE 'trace: .+ \([0-9a-f]{7}\)' <<<"$output"; then
  echo "FAIL: no 'trace: <name> (<id>)' line in program output" >&2
  exit 1
fi

exit_code=0
dotnet tool run dotnet-narrativetrace doctor --dir "$PROJECT_DIR" || exit_code=$?
if [[ "$exit_code" -ne 0 ]]; then
  echo "FAIL: doctor CLI did not exit 0 (exit $exit_code)" >&2
  exit 1
fi

echo "PASS"
