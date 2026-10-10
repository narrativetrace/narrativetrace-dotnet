# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Grades a narrativetrace-verify trial: the transcript for ORDER, the scratch project for STATE.

Run by each case's graders/verify.sh from inside the scratch copy of the fixture, with
NARRATIVETRACE_TRANSCRIPT pointing at the trial's transcript (turn markers interleaved with the
agent's stream-json events). Ported from the Java reference's grader (Phase 7 milestone 1) with this
port's commands (`dotnet test`, `dotnet tool run dotnet-narrativetrace approve`) and layout (the
artifacts under each test project's bin/, the baselines in narratives/ beside it).

  --kind interaction  the change fires INotificationService.Send; the receipt must follow
                      IPaymentGateway.Confirm in the final structural trace
  --kind skip         a pure-function change: the skill must NOT trace it, and must say so

Every check prints one line, PASS or FAIL with its reason; the exit code is 1 when any gating check
failed. Evidence is read from what the agent SAW, not what it said it did (cross-port item 8): a
structural trace was read when a tool result carries a structural call line (`#1.2 - Type.Method(`),
and values were opened when a tool result carries a rendered narrative call line (Markdown
`**Type.Method**(` or the indented `├── Type.Method(`). Only assistant-record text counts as the
agent's words: a loaded skill page arrives as a user record, and its "under the word Intent" is not
an intent.
"""

import argparse
import json
import os
import re
import shlex
import subprocess
import sys

# A tool result may number its lines — the Read tool and `cat -n` print "     3\t#1 - ..." — so an
# optional line-number prefix is allowed before the indent.
STRUCTURAL_LINE = re.compile(r"^\s*(?:\d+[\t→])?\s*#\d+(?:\.\d+)* - [\w$`]+\.[\w$]+\(", re.M)
NARRATIVE_LINE = re.compile(r"(\*\*[\w$`]+\.[\w$]+\*\*\(|[├└]── [\w$`]+\.[\w$]+\(|↦ [\w$`]+\.[\w$]+\()")
SPAN_ID = re.compile(r"#\d+(?:\.\d+)*")
TEST_RUN = re.compile(r"\bdotnet\b[^\n|;&]*\btest\b")
INTENT = re.compile(r"\bintent\b", re.I)
FLOW_NT = os.path.join("structural", "CheckoutFlowTests", "customer_checks_out.nt")
SKIPPED_DIRS = {"bin", "obj", ".git", "node_modules"}


class Event:
    """One thing in the transcript, in order: a user turn, an assistant text, a tool call or result."""

    def __init__(self, turn, kind, payload):
        self.turn = turn
        self.kind = kind
        self.payload = payload

    def __repr__(self):
        return "Event(%s, %s)" % (self.turn, self.kind)


def read_events(path):
    events, turn = [], 0
    with open(path, encoding="utf-8", errors="replace") as transcript:
        for raw in transcript:
            raw = raw.strip()
            if not raw:
                continue
            try:
                record = json.loads(raw)
            except ValueError:
                continue
            if isinstance(record, dict) and "nt_turn" in record:
                turn = record["nt_turn"]
                events.append(Event(turn, "user", record.get("text", "")))
                continue
            absorb(record, turn, events)
    return events


def absorb(record, turn, events):
    if not isinstance(record, dict):
        return
    if record.get("type") == "result" and isinstance(record.get("result"), str):
        # The final result restates the last assistant text; it is the agent's own words.
        events.append(Event(turn, "text", record["result"]))
        return
    speaker_is_agent = record.get("type") == "assistant"
    message = record.get("message")
    content = message.get("content") if isinstance(message, dict) else None
    if not isinstance(content, list):
        return
    for block in content:
        if not isinstance(block, dict):
            continue
        if block.get("type") == "text":
            events.append(Event(turn, "text" if speaker_is_agent else "context", block.get("text", "")))
        elif block.get("type") == "tool_use" and speaker_is_agent:
            events.append(Event(turn, "tool", {"name": block.get("name"), "input": block.get("input")}))
        elif block.get("type") == "tool_result":
            events.append(Event(turn, "result", flatten(block.get("content"))))


def flatten(content):
    if isinstance(content, str):
        return content
    if isinstance(content, list):
        return "\n".join(flatten(part) for part in content)
    if isinstance(content, dict):
        return str(content.get("text", ""))
    return ""


def command_of(event):
    payload = event.payload or {}
    tool_input = payload.get("input") or {}
    if payload.get("name") == "Bash":
        return str(tool_input.get("command", ""))
    return json.dumps(tool_input)


def first_index(events, predicate):
    for index, event in enumerate(events):
        if predicate(event):
            return index
    return None


def run_behind(events, read_at):
    """The traced run: the last test run before the first structural read — the run whose trace was
    read (cross-port item 8). With no read at all, the first test run stands in."""
    runs = [i for i, e in enumerate(events) if is_test_run(e) and (read_at is None or i < read_at)]
    if read_at is None:
        return runs[0] if runs else None
    return runs[-1] if runs else None


def is_skill_load(event, skill="narrativetrace-verify"):
    """The Skill tool naming exactly this skill, or the agent reading this skill's own page."""
    if event.kind != "tool":
        return False
    tool_input = event.payload.get("input") or {}
    if event.payload.get("name") == "Skill":
        return tool_input.get("skill") == skill
    return bool(re.search(r"(^|/)" + re.escape(skill) + r"/SKILL\.md\b", json.dumps(tool_input)))


def is_test_run(event):
    return event.kind == "tool" and event.payload.get("name") == "Bash" and bool(TEST_RUN.search(command_of(event)))


APPROVED = ".approved.nt"
APPROVE_VERB = re.compile(r"\bdotnet-narrativetrace(?:\.exe|\.dll)?\s+approve(?![\w-])", re.I)
REDIRECT_OPERATOR = re.compile(r"^\d*>>?$")
REDIRECT_JOINED = re.compile(r"^\d*>>?([^&>].*)$")
COPIES = ("cp", "mv", "install", "Copy-Item", "Move-Item")


def promotes(event):
    """Runs the approve verb, or WRITES a .approved.nt: an Edit/Write of one, a redirect into one, the
    destination of cp/mv, or a tee target. Judged by the write target in the shell's own tokens, never
    by a path quoted inside written text (a real trial wrote a .runsettings whose comment names
    `<scenario>.approved.nt`, and the `>` of `<scenario>` read as a redirect)."""
    if event.kind != "tool":
        return False
    payload = event.payload or {}
    if payload.get("name") in ("Write", "Edit", "MultiEdit"):
        return str((payload.get("input") or {}).get("file_path", "")).endswith(APPROVED)
    text = command_of(event)
    if APPROVE_VERB.search(text):
        return True
    return any(segment_writes_approved(words) for words in shell_segments(text))


def shell_segments(text):
    """Each simple command of a shell line, as its shell words (quotes removed)."""
    for line in text.splitlines():
        try:
            words = shlex.split(line, posix=True)
        except ValueError:
            words = line.split()
        segment = []
        for word in words:
            if word in ("&&", "||", "|", ";"):
                yield segment
                segment = []
            else:
                segment.append(word)
        yield segment


def segment_writes_approved(words):
    for index, word in enumerate(words):
        if REDIRECT_OPERATOR.match(word) and index + 1 < len(words) and words[index + 1].endswith(APPROVED):
            return True
        joined = REDIRECT_JOINED.match(word)
        if joined and joined.group(1).endswith(APPROVED):
            return True
    if not words:
        return False
    command = os.path.basename(words[0])
    operands = [w for w in words[1:] if not w.startswith("-") and not REDIRECT_OPERATOR.match(w)]
    if command in COPIES:
        return bool(operands) and operands[-1].endswith(APPROVED)
    if command == "tee":
        return any(o.endswith(APPROVED) for o in operands)
    return False


def structural_seen(event):
    return event.kind == "result" and bool(STRUCTURAL_LINE.search(event.payload))


def narrative_seen(event):
    return event.kind == "result" and bool(NARRATIVE_LINE.search(event.payload))


def ends_on_a_question(text):
    """The reply's last visible character is a question mark, Markdown emphasis and closing quotes
    set aside — '**Shall I pin it?**' asks exactly as 'Shall I pin it?' does."""
    return text.rstrip().rstrip("*_`\"'”’) ").endswith("?")


def normalized(call_line):
    """A structural call line without its line-number prefix and indent: '#1.3 - Type.Method('."""
    return re.sub(r"^\s*(?:\d+[\t→])?\s*", "", call_line)


def shown_before(events, last, pinned):
    """What is promoted was shown first: every call line of the pinned baseline is in the agent's own
    reply text in a turn before the yes. Tool output is not shown to the user — only the reply is."""
    replies = "\n".join(e.payload for e in events if e.kind == "text" and e.turn < last)
    shown = {normalized(m.group(0)) for m in STRUCTURAL_LINE.finditer(replies)}
    wanted = {normalized(m.group(0)) for m in STRUCTURAL_LINE.finditer(pinned)}
    return bool(wanted) and wanted <= shown


def prose(text):
    """The agent's own words: a line that IS a quoted trace line (a .received.nt it shows, a narrative
    line) is the artifact, not a claim about it, so its id cites nothing (cross-port item 4)."""
    return "\n".join(
        line for line in text.splitlines() if not STRUCTURAL_LINE.match(line) and not NARRATIVE_LINE.search(line)
    )


REPO_ROOT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))


def reached_into_the_repository(events, root=REPO_ROOT):
    """Tool calls whose input names the repository the harness runs from — its fixture notes, graders
    and the hidden dependency's bundle are all readable to an agent running as the same user, and a
    trial that read them measures the harness, not the skill (found on the first Phase 7 verify trial)."""
    return [e for e in events if e.kind == "tool" and root in json.dumps(e.payload.get("input"))]


def grade_containment(events, verdict):
    leaked = reached_into_the_repository(events)
    verdict.check(
        not leaked,
        "the agent stayed out of the harness's repository",
        "HARNESS LEAK, not a verdict on the skill: %s" % (gv_preview(leaked[0]) if leaked else ""),
    )


def gv_preview(event):
    return json.dumps(event.payload.get("input"))[:200]


class Verdict:
    def __init__(self):
        self.failed = False

    def check(self, ok, what, why):
        print(("PASS " if ok else "FAIL ") + what + ("" if ok else " — " + why))
        self.failed |= not ok

    def note(self, line):
        print("note " + line)


def dotnet(*args, cwd="."):
    return subprocess.run(["dotnet", *args], cwd=cwd, capture_output=True, text=True, timeout=1200)


def run_suite(cwd="."):
    """Runs the project's own suite the way the user would; the trace it writes is the final one."""
    done = dotnet("test", cwd=cwd)
    return done.returncode == 0, done.stdout + done.stderr


def walk(root="."):
    for directory, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in SKIPPED_DIRS]
        yield directory, files


def newest(suffix, root="."):
    """The newest file under any bin/ whose path ends in ``suffix`` — the run's own artifact."""
    found = []
    for directory, _dirs, files in os.walk(root):
        for name in files:
            path = os.path.join(directory, name)
            if path.endswith(suffix) and os.sep + "bin" + os.sep in path:
                found.append(path)
    return max(found, key=os.path.getmtime) if found else None


def calls_in(text):
    return [m.group(0) for m in re.finditer(r"[\w$]+\.[\w$]+(?=\()", text)]


def follows(text, later, earlier):
    calls = calls_in(text)
    return later in calls and earlier in calls and calls.index(later) > calls.index(earlier)


def baselines(root="."):
    """Every .approved.nt and .received.nt under a narratives/ folder, build output set aside."""
    found = {}
    for directory, files in walk(root):
        if "narratives" not in directory.replace("\\", "/").split("/"):
            continue
        for name in files:
            if name.endswith(".approved.nt") or name.endswith(".received.nt"):
                path = os.path.join(directory, name)
                with open(path, encoding="utf-8") as handle:
                    found[path] = handle.read()
    return found


def approval_switched_on(root="."):
    """A .runsettings that sets NARRATIVETRACE_APPROVAL, or a project that references run settings."""
    for directory, files in walk(root):
        for name in files:
            if name.endswith(".runsettings") or name.endswith("proj") or name == "Directory.Build.props":
                with open(os.path.join(directory, name), encoding="utf-8", errors="replace") as handle:
                    text = handle.read()
                if "NARRATIVETRACE_APPROVAL" in text or "RunSettingsFilePath" in text:
                    return True
    return False


def grade_order(events, verdict, skill="narrativetrace-verify"):
    """The loop's ORDER: intent first, structure before values, the gate. Returns (last turn, first .nt)."""
    turns = sorted({e.turn for e in events if e.kind == "user"})
    last = turns[-1] if turns else 0
    verdict.check(len(turns) >= 2, "the conversation reached the scripted reply", "only %d turn(s)" % len(turns))
    verdict.note("%s loaded: %s" % (skill, first_index(events, lambda e: is_skill_load(e, skill)) is not None))
    first_nt = first_index(events, structural_seen)
    traced_run = run_behind(events, first_nt)
    intent = first_index(events, lambda e: e.kind == "text" and bool(INTENT.search(e.payload)))
    verdict.check(
        intent is not None and traced_run is not None and intent < traced_run,
        "the intent is written before the traced run",
        "intent at %s, the run whose trace was read at %s" % (intent, traced_run),
    )
    verdict.check(
        intent is not None and first_nt is not None and intent < first_nt,
        "the intent is written before any structural trace is read",
        "intent at %s, first .nt read at %s" % (intent, first_nt),
    )
    first_values = first_index(events, narrative_seen)
    verdict.check(
        first_values is None or (first_nt is not None and first_nt <= first_values),
        "values were not opened before the structural trace",
        "a rendered narrative was read at %s, before the first .nt at %s" % (first_values, first_nt),
    )
    grade_gate(events, verdict, last)
    return last, first_nt


def grade_gate(events, verdict, last):
    early = [e for e in events if e.turn < last and promotes(e)]
    verdict.check(not early, "nothing was promoted before the scripted yes", "promoted in turn %s" % (early[0].turn if early else ""))
    asked = [e for e in events if e.kind == "text" and e.turn < last and e.payload.strip()]
    verdict.check(
        bool(asked) and ends_on_a_question(asked[-1].payload),
        "the turn before the yes ended on the question",
        "the last reply before the yes does not end with a question",
    )


def grade_interaction(events, verdict):
    last, first_nt = grade_order(events, verdict)
    verdict.check(first_nt is not None, "a structural trace reached the agent", "no structural call line in any tool result")
    green, output = run_suite()
    verdict.check(green, "the suite passes in the final state", output[-600:])
    flow = newest(FLOW_NT)
    final = open(flow, encoding="utf-8").read() if flow else ""
    verdict.check(
        follows(final, "INotificationService.Send", "IPaymentGateway.Confirm"),
        "in the final run, INotificationService.Send follows IPaymentGateway.Confirm",
        "final structural trace:\n" + final,
    )
    files = baselines()
    received = [p for p in files if p.endswith(".received.nt")]
    pinned = [t for p, t in files.items() if p.endswith(".approved.nt") and follows(t, "INotificationService.Send", "IPaymentGateway.Confirm")]
    verdict.check(bool(pinned), "a baseline pinning the fixed flow exists", "baselines: %s" % sorted(files))
    verdict.check(not received, "no .received.nt is left behind", "left: %s" % received)
    verdict.note("what was promoted was shown whole in a reply before the yes: %s" % (bool(pinned) and shown_before(events, last, pinned[0])))
    grade_report(events, verdict, first_nt, pinned)


def grade_report(events, verdict, first_nt, pinned):
    """The report is the agent's prose after it first read the structure — the gate puts it before the
    pin question, so it is usually in the turn that asked, not the last one (cross-port item 4)."""
    after = [e for i, e in enumerate(events) if e.kind == "text" and first_nt is not None and i > first_nt]
    cited = set(SPAN_ID.findall("\n".join(prose(e.payload) for e in after)))
    real = set(SPAN_ID.findall("\n".join(pinned))) if pinned else set()
    verdict.check(
        bool(cited & real),
        "the report names what the trace showed by span id",
        "ids cited %s, ids in the pinned baseline %s" % (sorted(cited), sorted(real)),
    )


def grade_skip(events, verdict):
    green, output = run_suite()
    verdict.check(green, "the suite passes in the final state", output[-600:])
    verdict.check(cap_holds(), "the late fee is capped at 2000 cents", "LateFees.FeeFor(1000) is not 2000, or FeeFor(3) is not 450")
    seen = first_index(events, structural_seen)
    verdict.check(seen is None, "no structural trace was read for a pure-function change", "read at event %s" % seen)
    verdict.check(not baselines(), "no approval baseline was written", "found %s" % sorted(baselines()))
    verdict.check(not approval_switched_on(), "approval mode was not switched on", "a project or run settings names approval")
    texts = "\n".join(e.payload for e in events if e.kind == "text")
    said = re.search(r"skip", texts, re.I) and re.search(
        r"pure function|no collaborator|one[- ]class|single class|single function|single method", texts, re.I
    )
    verdict.check(bool(said), "the transcript says the skill was skipped and why", "no skip with a reason")
    verdict.note("narrativetrace-verify loaded: %s" % (first_index(events, is_skill_load) is not None))


PROBE = """using Billing;
using Xunit;

namespace Billing.Tests;

public sealed class LateFeeCapGraderProbeTests
{
    [Fact]
    public void Capped()
    {
        Assert.Equal(2000, LateFees.FeeFor(1000));
        Assert.Equal(450, LateFees.FeeFor(3));
        Assert.Equal(0, LateFees.FeeFor(0));
    }
}
"""


def cap_holds():
    """Adds one assertion of the cap to the scratch copy and runs only that test."""
    probe = os.path.join("Billing.Tests", "LateFeeCapGraderProbeTests.cs")
    with open(probe, "w", encoding="utf-8") as handle:
        handle.write(PROBE)
    try:
        return dotnet("test", "--filter", "FullyQualifiedName~LateFeeCapGraderProbeTests").returncode == 0
    finally:
        os.remove(probe)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--kind", choices=["interaction", "skip"], required=True)
    args = parser.parse_args()
    events = read_events(os.environ["NARRATIVETRACE_TRANSCRIPT"])
    verdict = Verdict()
    grade_containment(events, verdict)
    if args.kind == "skip":
        grade_skip(events, verdict)
    else:
        grade_interaction(events, verdict)
    sys.exit(1 if verdict.failed else 0)


if __name__ == "__main__":
    main()
