#!/bin/sh
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# Transcript verifier for the FRESH-SESSION case. Exit 0 = gate passed.
# Run with cwd set to the scaffolded fixture copy; $NARRATIVETRACE_TRANSCRIPT points at this
# trial's evidence, which lives outside this project.
#
# THE CASE
#
# The INSTALLER's question. case.json declares `"registry": "narrativetrace-init"`: before the
# agent starts, the harness runs THIS CHECKOUT's own built CLI (`dotnet <dll> init`) in the empty
# scratch project — never a published package, which predates the checkout — and a delivery turns
# the harness's own page seeding off. The agent then gets ONE turn: a fresh session with a
# doctor-shaped question. What is graded is what that session FINDS: its init event must list every
# NarrativeTrace skill, and it must reach for the doctor — the skill by name, or the doctor command.
#
# Any lane can run it: nothing here depends on the agent reaching the install.
# `fresh-session-after-the-prompt` is the whole real path (the published prompt first); red there
# with this one green is the prompt path, not the installer.
set -e

python3 "$(dirname "$0")/../../grade_the_fresh_session.py"
