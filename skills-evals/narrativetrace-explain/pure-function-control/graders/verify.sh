#!/bin/sh
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# narrativetrace-explain / pure-function-control — the negative case, and the one that proves the
# skill's cost rule rather than its loop.
#
# Fixture: checkout-service. LateFees.FeeFor is a pure function with no collaborator: 150 cents per
# day late, zero when it is not late. Its whole behaviour is one line of source, so the skill's first
# step must answer FROM THE SOURCE and stop. PASS: no project or build file changed (nothing was
# installed), no source changed, dotnet never ran, nothing was traced, and the answer names the
# method, the rate and the not-late case. Loading the skill to make that decision is fine — its first
# step IS the decision; installing, running or tracing after it is the failure.
# Near misses: a trace of a pure function; an answer that runs the suite "to be sure"; an answer
# that describes the method without its numbers.
set -e
python3 "$(dirname "$0")/../../grade_the_explain.py" --kind control
