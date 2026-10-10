#!/bin/sh
# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
# narrativetrace-verify / verify-unintended-interaction (Phase 7 D6) — the owner's demonstration.
#
# Fixture: checkout-service. Its checkout runs through a COMPILED third-party pipeline whose
# ICheckoutListener hook is documented as running once the payment is confirmed, but runs after
# IPaymentGateway.Authorize and BEFORE IPaymentGateway.Confirm — the cause is outside the readable
# source (cross-port item 9). The task asks for a receipt "once their payment is confirmed"; the
# natural solution — a listener — passes every test while the structural trace shows
# INotificationService.Send before IPaymentGateway.Confirm. Turn 2 is the scripted "yes, pin it".
#
# PASS (every line of grade_the_verify.py --kind interaction): the intent is in the transcript before
# the traced run and before any .nt is read; a structural trace reached the agent and no rendered
# narrative before it; nothing was promoted before the yes, and the turn before the yes ended on the
# question; the suite passes and, in the grader's own final run, the receipt follows the
# confirmation; a .approved.nt pins that fixed flow and no .received.nt is left; the agent's prose
# after its first structural read cites a span id that is in the pinned baseline. Whether every call
# line of what was promoted was in a reply before the yes is REPORTED, not gated.
# Near misses it fails, each for its own reason: done on green tests; read the trace but did not
# act; values first; never pinned; pinned before the yes; a report without an id; intent after run.
set -e
python3 "$(dirname "$0")/../../grade_the_verify.py" --kind interaction
