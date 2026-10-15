# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Self-check of grade_the_explain.py's pure functions — run it before every trial batch.

Standalone: `python3 check_grade_the_explain.py`. No pytest. Each probe asserts what the grader
SHOULD do; a failing probe is printed and makes the exit code 1.
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import grade_the_explain as ge  # noqa: E402
gv = ge.gv

FAILURES = []
PROBES = [0]
SEP = os.sep


def expect(label, condition, detail=""):
    PROBES[0] += 1
    if not condition:
        FAILURES.append(label + ((" -- " + detail) if detail else ""))


def bash(command):
    return gv.Event(1, "tool", {"name": "Bash", "input": {"command": command}})


def tool(name, tool_input):
    return gv.Event(1, "tool", {"name": name, "input": tool_input})


def text(turn, payload):
    return gv.Event(turn, "text", payload)


# --- the skill counts as invoked only through the Skill tool, naming this skill ---
expect("Skill tool naming the skill", ge.skill_invoked([tool("Skill", {"skill": ge.SKILL})]))
expect("plugin-qualified name still counts", ge.skill_invoked([tool("Skill", {"skill": "narrativetrace-dotnet:" + ge.SKILL})]))
expect("another skill does not count", not ge.skill_invoked([tool("Skill", {"skill": "narrativetrace-debug"})]))
expect("reading the page is not invoking it", not ge.skill_invoked([tool("Read", {"file_path": ".claude/skills/" + ge.SKILL + "/SKILL.md"})]))
expect("reading the page is reported as such", ge.page_read([tool("Read", {"file_path": ".claude/skills/" + ge.SKILL + "/SKILL.md"})]) == 0)

# --- "nothing was run": the dotnet verbs that build or run code ---
for command in ("dotnet test", "cd x && dotnet build", "dotnet run --project Billing", "FOO=1 dotnet test --filter X", "dotnet.exe test"):
    expect("runs: " + command, ge.ran_dotnet([bash(command)]) == 0)
for command in ("cat Billing/LateFees.cs", "grep -rn dotnet README.md", "ls Billing.Tests", "echo dotnet testing"):
    expect("reads only: " + command, ge.ran_dotnet([bash(command)]) is None, command)
expect("a Read of a file named test does not run", ge.ran_dotnet([tool("Read", {"file_path": "dotnet test.txt"})]) is None)

# --- which directories hold a run's own traces ---
base = SEP.join([".", "Billing.Tests", "bin", "Debug", "net10.0", "TestResults", "narrativetrace"])
expect("structural folder", ge.written_by_a_run(base + SEP + "structural"))
expect("a test class folder under structural", ge.written_by_a_run(base + SEP + "structural" + SEP + "CheckoutFlowTests"))
expect("traces folder", ge.written_by_a_run(base + SEP + "traces"))
expect("diagrams folder", ge.written_by_a_run(base + SEP + "diagrams"))
expect("a clarity report is not a trace", not ge.written_by_a_run(base + SEP + "clarity"))
expect("the root of narrativetrace holds none", not ge.written_by_a_run(base))
expect("outside bin is not a run", not ge.written_by_a_run(SEP.join([".", "narrativetrace", "structural"])))
expect("a sibling of the same prefix is not a trace folder", not ge.written_by_a_run(base + SEP + "structuralx"))

# --- the calls named in order ---
expect("two calls in order", ge.calls_named_in_order("IInvoiceService.IssueInvoice then IPaymentGateway.Confirm"))
expect("two calls out of order", not ge.calls_named_in_order("IPaymentGateway.Confirm after IInvoiceService.IssueInvoice"))
expect("one call is not an order", not ge.calls_named_in_order("only IPaymentGateway.Authorize"))
expect("listener between authorize and confirm", ge.calls_named_in_order("IPaymentGateway.Authorize, ICheckoutListener.OnCheckout, IPaymentGateway.Confirm"))
expect("no calls", not ge.calls_named_in_order("it checks out"))

# --- the answer is the agent's words in the LAST turn ---
events = [gv.Event(1, "user", "ask"), text(1, "first"), gv.Event(2, "user", "again"), text(2, "second"), text(2, "third")]
expect("last turn only", ge.final_answer(events) == "second\nthird", ge.final_answer(events))
expect("no user turn means turn zero", ge.final_answer([text(0, "x")]) == "x")

# --- not-observed and from-the-source wording ---
expect("not observed", bool(ge.NOT_OBSERVED.search("The pipeline itself was not observed")))
expect("no span", bool(ge.NOT_OBSERVED.search("no span covers the pipeline")))
expect("silence is not naming", not ge.NOT_OBSERVED.search("everything was seen"))
expect("reading the source", bool(ge.FROM_THE_SOURCE.search("reading the source: a pure function")))

if FAILURES:
    print("FAIL %d of %d probes:" % (len(FAILURES), PROBES[0]))
    for failure in FAILURES:
        print("  " + failure)
    sys.exit(1)
print("ok: %d probes" % PROBES[0])
