#!/bin/sh
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# narrativetrace-debug / debug-value-divergence (Phase 7 D6) — a symptom ticket whose defect is
# visible only as a value at one boundary.
#
# Fixture: checkout-currency. RateTableConverter rounds to whole units before moving to minor units,
# so 45.99 EUR at 0.93 is charged 4300 instead of 4277 — only the value at
# `ICurrencyConverter.Convert ... #1.3` shows it. Turn 2 is the scripted "yes, pin it".
#
# PASS (every line of grade_the_debug.py): the reproduction's values were read before the fix; the
# converter's span id — one the agent was SHOWN — is named in its own words before the first write
# to production code (Billing/); nothing was promoted before the yes and that turn ended on the
# question; the agent's prose after the fix names the diverging id (quoted trace lines excluded);
# the suite passes; the converter itself changed and now converts the ticket's input to 4277; with
# the fixture's production code put back, a test fails (the regression test); the call shapes read
# from the .md before and after the fix are identical (a red run writes no .nt); an approved
# baseline pins a flow through the converter and no .received.nt is left.
# Near misses it fails: a fix downstream of the span; a route around the span (the delta alone);
# a fix before the span is named; no regression test; never pinned.
set -e
python3 "$(dirname "$0")/../../grade_the_debug.py"
