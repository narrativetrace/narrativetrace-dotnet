#!/usr/bin/env bash
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# World-state verifier for add-narrative-tracing/init-prompt-aspnetcore-project (Phase 6 D4). Exit 0 =
# gate passed. Run with cwd set to the scaffolded fixture copy; $NARRATIVETRACE_CLI_DLL names the built
# CLI the grader runs the doctor with — the agent's own report and its own tool restore are never
# trusted.
#
# THE CASE
#
# Fixture: `aspnetcore-project` — an existing ASP.NET Core minimal API, one service boundary
# (IOrderService.PlaceOrder behind GET /orders/{customerId}), no NarrativeTrace. Turn 1 is the published
# init prompt, byte for byte; turn 2 is the approval the prompt itself asks for ("run it for real only
# after I have seen the diff"), so the installed add-narrative-tracing skill — whose framework step runs
# the doctor and applies every framework fix it prints — is what the agent follows.
#
# Expected trajectory: install the CLI and NarrativeTrace from the local feed, preview then apply init,
# run the doctor, apply config.registration-pair's printed fix (add NarrativeTrace.AspNetCore,
# AddNarrativeTrace + UseNarrativeTrace, wrap the service against HttpContext.GetNarrativeContext()),
# add the redaction test, run the doctor again.
#
# THE GATE (graded here, on every model) — see grade_the_aspnetcore_case.py's docstring: the project is
# still ASP.NET Core; config.registration-pair green; a request the GRADER sends gets the old answer
# and its trace names the boundary in the app's own output; the redaction test passes; no .received.nt.
#
# NOT GRADED HERE
#
# Whether the agent pasted the trace or explained it (the prompt's last step), and every other doctor
# finding (reported by id, never gated).
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
exec python3 -I -B "$here/../../grade_the_aspnetcore_case.py"
