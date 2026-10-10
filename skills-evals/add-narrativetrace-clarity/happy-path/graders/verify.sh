#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# World-state verifier for add-narrativetrace-clarity/happy-path. Exit 0 = gate passed. Run with cwd
# set to the scaffolded fixture copy; $NARRATIVETRACE_CLI_DLL names the built CLI the grader scans and
# checks with — the agent's own reports and its own tool restore are never trusted.
#
# THE CASE
#
# Fixture: `clarity-consumer` — a console project whose public boundary is `IOrderHandler.Handle(string
# data)` and its implementation, flagged HIGH twice over by the clarity gate (generic verb, vague
# parameter). The prompt asks the agent to run Clarity, read the report and fix what it flags.
#
# Expected trajectory: restore the local tool, build, scan, read both reports, rename the member and
# the parameter by the report's suggestions, update Program.cs, and re-run until the gate exits 0.
#
# THE GATE (graded here, on every model)
#
# The grader rebuilds the project, scans the built assembly itself and requires
# `clarity-check --max-high-issues 0` to exit 0 — and refuses a pass that is vacuous: at least two
# scenarios scanned (the interface and its implementation are both still public), the program still
# printing what it printed before, and still calling a method on the handler with the same argument.
# Deleting the members, hiding the types or hard-coding the output all pass the gate and fail here.
#
# NOT GRADED HERE
#
# Whether the new names are good ones, and whether the agent explained the scores.
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
exec python3 -I -B "$here/../../grade_the_gate.py"
