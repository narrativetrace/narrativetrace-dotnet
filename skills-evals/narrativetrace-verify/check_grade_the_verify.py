# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Self-check for grade_the_verify.py's ORDER logic, on synthetic transcripts — no trial, no build.

Run: python3 skills-evals/narrativetrace-verify/check_grade_the_verify.py   (exit 1 on any wrong verdict)

Each probe builds a transcript the way the runner writes one (turn markers, then stream-json records)
and asserts which checks pass and fail. The STATE half (the suite, the final .nt, the baselines) is
rehearsed on real scratch trees; see skills-evals/README.md's Phase 7 section.
"""

import io
import json
import os
import sys
import tempfile
from contextlib import redirect_stdout

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import grade_the_verify as gv  # noqa: E402

NT = "#1 - ICheckoutService.Checkout(order) → value\n  #1.4 - IPaymentGateway.Confirm(authorizationId)\n  #1.5 - INotificationService.Send(customerId, message)"
MD = "- **ICheckoutService.Checkout**(order: `x`) → `\"INV-1\"` #1"


def user(turn, text):
    return {"nt_turn": turn, "role": "user", "text": text}


def say(text):
    return {"type": "assistant", "message": {"content": [{"type": "text", "text": text}]}}


def bash(command, output="", tool_id="t"):
    return [
        {"type": "assistant", "message": {"content": [{"type": "tool_use", "id": tool_id, "name": "Bash", "input": {"command": command}}]}},
        {"type": "user", "message": {"content": [{"type": "tool_result", "tool_use_id": tool_id, "content": output}]}},
    ]


def read(path, output):
    return [
        {"type": "assistant", "message": {"content": [{"type": "tool_use", "id": "r", "name": "Read", "input": {"file_path": path}}]}},
        {"type": "user", "message": {"content": [{"type": "tool_result", "tool_use_id": "r", "content": output}]}},
    ]


def solved(**overrides):
    parts = {
        "skill_page": [{"type": "user", "message": {"content": [{"type": "text", "text": "under the word Intent"}]}}],
        "intent": [say("tracing: two collaborators.\nIntent: Checkout issues, authorizes, confirms, then sends the receipt once.")],
        "run": bash("dotnet test --filter FullyQualifiedName~CheckoutFlowTests", "Passed!"),
        "nt": read("Billing.Tests/bin/Debug/net10.0/TestResults/narrativetrace/structural/CheckoutFlowTests/customer_checks_out.nt", "     3\t" + NT),
        "values": [],
        "report": [say("The trace showed #1.5 INotificationService.Send after #1.4 IPaymentGateway.Confirm.")],
        "ask": [say("Here is the .received.nt:\n" + NT + "\n\nShall I pin it as the baseline?")],
        "early_promotion": [],
        "yes": [user(2, "yes, pin it")],
        "promote": bash("dotnet tool run dotnet-narrativetrace approve", "approved: x"),
        "closing": [say("Pinned.")],
    }
    parts.update(overrides)
    records = [user(1, "task")]
    for key in ["skill_page", "intent", "run", "nt", "values", "report", "ask", "early_promotion", "yes", "promote", "closing"]:
        records.extend(parts[key])
    return records


def verdict_of(records):
    with tempfile.NamedTemporaryFile("w", suffix=".jsonl", delete=False, encoding="utf-8") as handle:
        for record in records:
            handle.write(json.dumps(record) + "\n")
        path = handle.name
    try:
        events = gv.read_events(path)
    finally:
        os.remove(path)
    out = io.StringIO()
    verdict = gv.Verdict()
    with redirect_stdout(out):
        _last, first_nt = gv.grade_order(events, verdict)
        verdict.check(first_nt is not None, "a structural trace reached the agent", "none")
        gv.grade_report(events, verdict, first_nt, [NT])
    return out.getvalue()


PROBES = [
    ("solved", solved(), []),
    ("intent written after the run", solved(intent=[], report=[say("Intent: issue, authorize, confirm, send. The trace showed #1.5 after #1.4.")]),
     ["the intent is written before the traced run", "the intent is written before any structural trace is read"]),
    ("the skill page's own 'Intent' is not the agent's", solved(intent=[say("tracing: two collaborators.")]),
     ["the intent is written before the traced run", "the intent is written before any structural trace is read"]),
    ("values opened first", solved(nt=read("x.md", MD) + read("x.nt", NT)), ["values were not opened before the structural trace"]),
    ("no structural read at all", solved(nt=[]),
     ["a structural trace reached the agent", "the intent is written before any structural trace is read",
      "the report names what the trace showed by span id"]),
    ("promoted before the yes", solved(early_promotion=bash("dotnet tool run dotnet-narrativetrace approve", "approved")),
     ["nothing was promoted before the scripted yes"]),
    ("promoted by copying before the yes", solved(early_promotion=bash("cp a.received.nt a.approved.nt")),
     ["nothing was promoted before the scripted yes"]),
    ("promoted by writing the file before the yes", solved(early_promotion=[{"type": "assistant", "message": {"content": [
        {"type": "tool_use", "id": "w", "name": "Write", "input": {"file_path": "Billing.Tests/narratives/X/a.approved.nt", "content": NT}}]}}]),
     ["nothing was promoted before the scripted yes"]),
    ("reading a baseline is not promoting it", solved(early_promotion=bash("cat Billing.Tests/narratives/X/a.approved.nt", NT)), []),
    ("text after the question", solved(ask=[say("Shall I pin it?"), say("Meanwhile, note that the suite is green.")]),
     ["the turn before the yes ended on the question"]),
    ("a bold question still asks", solved(ask=[say(NT + "\n\n**Shall I pin it as the baseline?**")]), []),
    ("the report cites only inside a quoted trace line", solved(report=[say("The receipt now goes out last.")]),
     ["the report names what the trace showed by span id"]),
    ("an id the baseline does not have", solved(report=[say("The trace showed #9.9 last.")]),
     ["the report names what the trace showed by span id"]),
    # A run made before the Intent does not make the trial fail when the run whose trace was READ came
    # after it (cross-port item 8: the traced run is the run whose trace was first read).
    ("a run before the intent, re-run after it", solved(run=bash("dotnet test", "Passed!", "a") + [say("Intent: ...")] + bash("dotnet test", "Passed!", "b"),
                                                       intent=[]), []),
    ("the only run is before the intent", solved(run=bash("dotnet test", "Passed!", "a") + [say("Intent: ...")], intent=[]),
     ["the intent is written before the traced run"]),
]


RUNSETTINGS_BY_PRINTF = (
    "printf '%s\\n' '<?xml version=\"1.0\"?>' '<!-- differs from its committed "
    "narratives/<TestClass>/<scenario>.approved.nt fails with the diff. -->' '<RunSettings />' "
    "> Billing.Tests/approval.runsettings"
)

# (command, promotes?) — the write TARGET decides, never a path quoted inside written text.
PROMOTION_PROBES = [
    (RUNSETTINGS_BY_PRINTF, False),  # found by a real trial (batch 4, trial 4): '<scenario>' is not a redirect
    ("echo 'see <a>.approved.nt' > notes.txt", False),
    ("cat Billing.Tests/narratives/X/a.approved.nt", False),
    ("grep -n mv Billing.Tests/narratives/X/a.approved.nt", False),
    ("cp Billing.Tests/narratives/X/a.approved.nt /tmp/backup.nt", False),
    ("cat a.received.nt > Billing.Tests/narratives/X/a.approved.nt", True),
    ("cat a.received.nt >Billing.Tests/narratives/X/a.approved.nt", True),
    ("cat a.received.nt >> a.approved.nt", True),
    ("cp a.received.nt a.approved.nt", True),
    ("mv -f a.received.nt 'a.approved.nt'", True),
    ("cat a.received.nt | tee a.approved.nt", True),
    ("cd Billing.Tests && dotnet tool run dotnet-narrativetrace approve", True),
    ("dotnet-narrativetrace approve --dir .", True),
    ("dotnet tool run dotnet-narrativetrace approved", False),
]


def main():
    wrong = 0
    for command, expected in PROMOTION_PROBES:
        got = gv.promotes(gv.Event(1, "tool", {"name": "Bash", "input": {"command": command}}))
        if got != expected:
            wrong += 1
            print("WRONG promotes(%r): %s, expected %s" % (command[:70], got, expected))
    for name, records, expected_failures in PROBES:
        output = verdict_of(records)
        failed = {line[5:].split(" — ")[0] for line in output.splitlines() if line.startswith("FAIL ")}
        if failed != set(expected_failures):
            wrong += 1
            print("WRONG %s: failed %s, expected %s" % (name, sorted(failed), sorted(expected_failures)))
        else:
            print("ok    %s" % name)
    print("%d probe(s), %d wrong" % (len(PROBES) + len(PROMOTION_PROBES), wrong))
    sys.exit(1 if wrong else 0)


if __name__ == "__main__":
    main()
