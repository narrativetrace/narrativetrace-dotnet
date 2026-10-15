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
# The WHOLE real path (`fresh-session-sees-the-skills` is the installer's question alone: the
# harness runs this checkout's own `init`, one turn). Red here with that one green is the prompt
# path — an agent that stopped before the install — not the installer.
#
# Turn 1 is the published init prompt, byte for byte (InitPromptDriftTest enforces that): the agent
# installs NarrativeTrace and the skills into an empty project. Turn 2 is a NEW SESSION in the same
# project (case.json: "freshTurns": [2]) that asks a doctor-shaped question. What is graded is what
# that second session FINDS: its init event must list every NarrativeTrace skill, and it must reach
# for the doctor — the skill by name, or the doctor command itself.
#
# WHY A FRESH SESSION
#
# Benchmark shakedown run 4 (2026-10-10, item 166): the install wrote `.agents/skills` and the
# `AGENTS.md` section, the doctor reported the skills installed, and the next Claude Code session
# listed no NarrativeTrace skill — it loads `.claude/skills` only, which the installer wrote just
# where a `.claude/` directory or a `CLAUDE.md` already existed. The install cases grade the
# session that ran the install, where that gap is invisible by construction.
set -e

python3 "$(dirname "$0")/../../grade_the_fresh_session.py"
