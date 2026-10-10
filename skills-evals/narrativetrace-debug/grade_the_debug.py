# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Grades a narrativetrace-debug trial: the transcript for ORDER, the scratch project for STATE.

Run by the case's graders/verify.sh from inside the scratch copy of `fixtures/checkout-currency`,
with NARRATIVETRACE_TRANSCRIPT pointing at the trial's transcript. The transcript reading is the verify
grader's own (`grade_the_verify.py`), imported rather than copied: both read the same stream-json with
the same rules (assistant-record text only, numbered reads allowed, evidence from what the agent SAW).
Ported from the Java reference's grader (Phase 7 milestone 2) with this port's layout: production
code in Billing/, tests in Billing.Tests/, artifacts under each test project's bin/.

The fixture's defect is a rounding in `RateTableConverter.Convert` — whole units before minor units —
so 45.99 EUR at 0.93 is charged 4300, not 4277. It is visible only as a value:
`ICurrencyConverter.Convert(euroCents: 4599, currency: "CHF") → 4300 #1.3`, with its child
`IExchangeRates.RateFor` returning the right 0.93. The structural trace is the same before and after.

Every check prints one line, PASS or FAIL with its reason; the exit code is 1 when any gating check
failed.
"""

import os
import re
import shlex
import shutil
import sys
import tempfile

sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "narrativetrace-verify"))
import grade_the_verify as gv  # noqa: E402 — the shared transcript reader

DIVERGING_CALL = "ICurrencyConverter.Convert"
CONVERTER = os.path.join("Billing", "RateTableConverter.cs")
PRODUCTION = "Billing"
FIXTURE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "fixtures", "checkout-currency")
MD_CALL = re.compile(r"\*\*([\w$`]+\.[\w$]+)\*\*\((.*)$")
# The call a trace LINE is about — never a call quoted inside one of its values. Structural:
# "#1.3 - Type.Method(", leading id. Narrative (Markdown or the indented tree): the first call on the
# line, with the line's own id trailing it.
STRUCTURAL_CALL = re.compile(r"^\s*(?:\d+[\t→])?\s*(#\d+(?:\.\d+)*) - ([\w$`]+\.[\w$]+)\(")
NARRATIVE_CALL = re.compile(
    r"^\s*(?:\d+[\t→])?[\s│]*(?:- \*\*([\w$`]+\.[\w$]+)\*\*\(|[├└]── ([\w$`]+\.[\w$]+)\(|↦ \*\*([\w$`]+\.[\w$]+)\*\*\(|↦ ([\w$`]+\.[\w$]+)\()"
)
TRAILING_ID = re.compile(r"(#\d+(?:\.\d+)*)\s*$")
# Parameter names: the first at the start, every later one right after a closing "`, " — so a value
# that itself contains "x: `" adds no name.
PARAMETER = re.compile(r"(?:^|`, )(\w+): `")
IN_PLACE = re.compile(r"^(-[a-zA-Z]*i[\w.]*|--in-place\S*)$")
REDIRECT = re.compile(r"(?<![<&])\d?>>?\s*([^\s|;&]+)")


def call_and_id(line):
    """(call, own id) of one trace line, or None when the line is not a call line."""
    structural = STRUCTURAL_CALL.match(line)
    if structural:
        return structural.group(2), structural.group(1)
    narrative = NARRATIVE_CALL.match(line)
    trailing = TRAILING_ID.search(line)
    if narrative and trailing:
        return next(group for group in narrative.groups() if group), trailing.group(1)
    return None


def diverging_ids(events):
    """The ids the agent was shown for the diverging call, in any flavour — the id of each trace line
    whose own call it is, never an id or a call quoted inside a value."""
    ids = set()
    for event in events:
        if event.kind != "result":
            continue
        for line in event.payload.splitlines():
            found = call_and_id(line)
            if found and found[0] == DIVERGING_CALL:
                ids.add(found[1])
    return ids


def in_production(path, cwd_in_production=False):
    """A path under Billing/ — after resolving '..' — or a relative path written from inside it.
    Billing.Tests/ is the tests, not production."""
    if not path or path.startswith("-") or path.startswith("&") or path == "/dev/null":
        return False
    norm = os.path.normpath(path.strip("'\"")).replace("\\", "/")
    return bool(re.search(r"(^|/)" + PRODUCTION + r"(/|$)", norm)) or (cwd_in_production and not norm.startswith("/"))


def tokens_of(segment):
    try:
        return shlex.split(segment)
    except ValueError:
        return segment.split()


def without_redirections(words):
    """The words of a command with its redirections set aside ('2>&1', '>', 'out.txt' after '>')."""
    kept, skip = [], False
    for word in words:
        if skip:
            skip = False
        elif re.match(r"^\d*(>>?|<)(&\d+)?$", word):
            skip = not word.endswith(("&1", "&2"))
        elif not re.match(r"^\d*(>>?|<)", word):
            kept.append(word)
    return kept


def segment_writes(segment, words, cwd_in_production):
    """One simple command writes production code: a redirect into it, an in-place sed/perl, tee, the
    destination of cp/mv, git checkout/restore of a path in it, or a script opening it for write."""
    if any(in_production(target, cwd_in_production) for target in REDIRECT.findall(segment)):
        return True
    while words and (words[0] == "sudo" or re.match(r"^\w+=", words[0])):
        words = words[1:]
    words = without_redirections(words)
    if not words:
        return False
    command, args = os.path.basename(words[0]), words[1:]
    operands = [a for a in args if not a.startswith("-")]
    if command in ("sed", "perl") and any(IN_PLACE.match(a) for a in args):
        scripted = "-e" in args or "-f" in args
        files = operands if scripted else operands[1:]
        return any(in_production(f, cwd_in_production) for f in files)
    if command == "tee":
        return any(in_production(f, cwd_in_production) for f in operands)
    if command in ("cp", "mv", "install"):
        return bool(operands) and in_production(operands[-1], cwd_in_production)
    if command == "git" and operands[:1] in (["checkout"], ["restore"]):
        return any(in_production(f, cwd_in_production) for f in operands[1:])
    if command.startswith("python"):
        return bool(re.search(r"open\([^)]*" + PRODUCTION + r"/[^)]*['\"][wa]", segment) or "write_text" in segment)
    return False


def shell_writes_production(command):
    """Walks a shell command's simple commands in order, tracking a `cd` into Billing/."""
    cwd_in_production = False
    for line in command.splitlines():
        for segment in re.split(r"\s*(?:&&|\|\||[|;])\s*", line):
            words = tokens_of(segment)
            if words[:1] == ["cd"]:
                cwd_in_production = len(words) > 1 and in_production(words[1])
                continue
            if segment_writes(segment, words, cwd_in_production):
                return True
    return False


def edits_production(event):
    """A tool call that changes production source: an edit or write under Billing/, or a shell command
    whose write TARGET is under it. A command that only names Billing/ — a grep, a cat — is a read."""
    if event.kind != "tool":
        return False
    payload = event.payload or {}
    tool_input = payload.get("input") or {}
    if payload.get("name") in ("Edit", "Write", "MultiEdit"):
        return in_production(str(tool_input.get("file_path", "")))
    return payload.get("name") == "Bash" and shell_writes_production(gv.command_of(event))


def prose(text):
    """The agent's own words: a line that IS a quoted trace line is the artifact, not a claim about it,
    so its id cites nothing. A sentence that merely mentions a call keeps its citation."""
    return "\n".join(line for line in text.splitlines() if call_and_id(line) is None)


def grade_order(events, verdict):
    turns = sorted({e.turn for e in events if e.kind == "user"})
    last = turns[-1] if turns else 0
    verdict.check(len(turns) >= 2, "the conversation reached the scripted reply", "only %d turn(s)" % len(turns))
    verdict.note("narrativetrace-debug loaded: %s" % (gv.first_index(events, lambda e: gv.is_skill_load(e, "narrativetrace-debug")) is not None))
    fix_at = gv.first_index(events, edits_production)
    ids = diverging_ids(events)
    # Named before the fix means seen before the fix: an id the agent had not been shown is a guess.
    seen_first = diverging_ids(events[:fix_at]) if fix_at is not None else ids
    values_at = gv.first_index(events, gv.narrative_seen)
    verdict.check(
        values_at is not None and (fix_at is None or values_at < fix_at),
        "the values of the reproduction were read before the fix",
        "no rendered narrative reached the agent" if values_at is None else "read at %s, fix at %s" % (values_at, fix_at),
    )
    named_at = gv.first_index(events, lambda e: e.kind == "text" and bool(seen_first & set(gv.SPAN_ID.findall(prose(e.payload)))))
    verdict.check(
        bool(seen_first) and named_at is not None and fix_at is not None and named_at < fix_at,
        "the diverging span is named by its id before the fix",
        "ids the agent saw for %s before the fix: %s; first named at %s; first production edit at %s"
        % (DIVERGING_CALL, sorted(seen_first), named_at, fix_at),
    )
    gv.grade_gate(events, verdict, last)
    after_fix = [e for i, e in enumerate(events) if e.kind == "text" and fix_at is not None and i > fix_at]
    cited = set(gv.SPAN_ID.findall("\n".join(prose(e.payload) for e in after_fix)))
    verdict.check(
        bool(cited & ids),
        "the root-cause report after the fix names the diverging span by its id",
        "ids cited after the fix %s, ids of %s %s" % (sorted(cited), DIVERGING_CALL, sorted(ids)),
    )
    closing = set(gv.SPAN_ID.findall("\n".join(prose(e.payload) for e in events if e.kind == "text" and e.turn == last)))
    verdict.note("the closing reply names the diverging span again: %s" % bool(closing & ids))


def is_skill_load(event):
    """The Skill tool naming exactly this skill, or the agent reading this skill's own page."""
    return gv.is_skill_load(event, "narrativetrace-debug")


def lines_of(path):
    with open(path, encoding="utf-8") as handle:
        return [line.rstrip() for line in handle.read().splitlines()]


PROBE = """using Billing;
using Xunit;

namespace Billing.Tests;

public sealed class ConverterGraderProbeTests
{
    [Fact]
    public void Ticket()
    {
        var converter = new RateTableConverter(new DailyRates());
        Assert.Equal(4277, converter.Convert(4599, "CHF"));
        Assert.Equal(9300, converter.Convert(10000, "CHF"));
        Assert.Equal(4599, converter.Convert(4599, "EUR"));
        Assert.Equal(3955, converter.Convert(4599, "GBP"));
    }
}
"""


def converter_value_is_right():
    """The value AT the diverging span, in the final code: today's converter, the ticket's input."""
    probe = os.path.join("Billing.Tests", "ConverterGraderProbeTests.cs")
    with open(probe, "w", encoding="utf-8") as handle:
        handle.write(PROBE)
    try:
        return gv.dotnet("test", "--filter", "FullyQualifiedName~ConverterGraderProbeTests").returncode == 0
    finally:
        os.remove(probe)


def shapes(root):
    """Each scenario's call shape from its .md narrative: '#id Type.Method(names)' per call line. The
    .md is written on a red run too, where no .nt is (the .nt is the last green one)."""
    found = {}
    for directory, _dirs, files in os.walk(root):
        parts = directory.replace("\\", "/").split("/")
        if "bin" not in parts or "traces" not in parts:
            continue
        base = directory[: directory.replace("\\", "/").rfind("/traces") + len("/traces")]
        for name in files:
            if name.endswith(".md"):
                path = os.path.join(directory, name)
                found[os.path.relpath(path, base)] = [s for s in map(shape_of, lines_of(path)) if s]
    return found


def shape_of(line):
    """'#id Type.Method(names)' of a narrative call line — its own trailing id, its parameter names
    scanned in order — or None for any other line."""
    found = call_and_id(line)
    match = MD_CALL.search(line)
    if not found or not match:
        return None
    return "%s %s(%s)" % (found[1], found[0], ", ".join(PARAMETER.findall(match.group(2))))


def pre_fix_copy():
    """The final project with every fixture production file put back as it was: the agent's tests, its
    approval switch and baselines, the fixture's code. New production files stay."""
    copy = tempfile.mkdtemp(prefix="debug-grader-prefix-")
    shutil.copytree(".", copy, dirs_exist_ok=True, ignore=shutil.ignore_patterns("bin", "obj", "TestResults"))
    production = os.path.join(FIXTURE, PRODUCTION)
    for directory, _dirs, files in os.walk(production):
        for name in files:
            source = os.path.join(directory, name)
            target = os.path.join(copy, PRODUCTION, os.path.relpath(source, production))
            os.makedirs(os.path.dirname(target), exist_ok=True)
            shutil.copyfile(source, target)
    return copy


def grade_state(verdict):
    green, output = gv.run_suite()
    verdict.check(green, "the suite passes in the final state", output[-600:])
    changed = os.path.exists(CONVERTER) and lines_of(CONVERTER) != lines_of(os.path.join(FIXTURE, CONVERTER))
    verdict.check(changed, "the fix touches the diverging span's code", CONVERTER + " is as the fixture shipped it")
    verdict.check(
        converter_value_is_right(),
        "the diverging span now carries the right value (4599 EUR at 0.93 is 4277)",
        "RateTableConverter still converts the ticket's input wrongly — the symptom was silenced elsewhere",
    )
    after = shapes(".")
    copy = pre_fix_copy()
    try:
        reverted = gv.dotnet("test", cwd=copy)
        verdict.check(reverted.returncode != 0, "a regression test fails when the fix is undone",
                      "with the fixture's production code back, every test still passes")
        before = shapes(copy)
    finally:
        shutil.rmtree(copy, ignore_errors=True)
    grade_delta(verdict, before, after)
    grade_baseline(verdict)


def grade_delta(verdict, before, after):
    common = sorted(set(before) & set(after))
    reaching = [s for s in common if any(DIVERGING_CALL + "(" in line for line in before[s])]
    moved = ["%s:\n  before %s\n  after  %s" % (s, before[s], after[s]) for s in common if before[s] != after[s]]
    verdict.check(
        bool(reaching) and not moved,
        "the structural delta against the pre-fix run shows nothing else moved",
        "no scenario reached %s in both runs" % DIVERGING_CALL if not reaching else "\n".join(moved),
    )


def grade_baseline(verdict):
    files = gv.baselines()
    received = [p for p in files if p.endswith(".received.nt")]
    pinned = [p for p, text in files.items() if p.endswith(".approved.nt") and DIVERGING_CALL + "(" in text]
    verdict.check(bool(pinned), "a baseline pinning the reproduced flow exists", "baselines: %s" % sorted(files))
    verdict.check(not received, "no .received.nt is left behind", "left: %s" % received)


def main():
    events = gv.read_events(os.environ["NARRATIVETRACE_TRANSCRIPT"])
    verdict = gv.Verdict()
    gv.grade_containment(events, verdict)
    grade_order(events, verdict)
    grade_state(verdict)
    sys.exit(1 if verdict.failed else 0)


if __name__ == "__main__":
    main()
