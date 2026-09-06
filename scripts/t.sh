#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Quick test loop: scripts/t.sh <test-project-dir> [filter]
# Prints only the result summary + failures/errors, keeps TDD cycles fast.
set -uo pipefail
proj="$1"; filter="${2:-}"
args=(test "$proj" --nologo -v q)
[ -n "$filter" ] && args+=(--filter "FullyQualifiedName~$filter")
out=$(dotnet "${args[@]}" 2>&1)
code=$?
echo "$out" | grep -E "error |Failed!|Passed!|Failed: |Passed: |Assert|Expected|Actual|\[FAIL\]" | grep -v "warning" | head -40
exit $code
