# SPDX-License-Identifier: BUSL-1.1
# Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
# Copyright (c) 2026 Empower Agile
"""Self-check of grade_the_debug.py's pure functions — run it before every trial batch.

Ported from the Java reference (Phase 7 milestone 2, cross-port item 5) with this port's layout:
production code in Billing/ (its near-miss siblings Billing.Tests/ and BillingX/ are not production).

Standalone: `python3 check_grade_the_debug.py`. No pytest. Each probe asserts what the grader
SHOULD do; a failing probe is printed and makes the exit code 1. Written by the Phase 7 milestone 2
adversarial pass: the probes commented "found by the adversarial pass" were grader defects
(reads counted as fixes, writes missed, an id inside a value taken for a span's own) and are fixed.
"""

import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import grade_the_debug as gd  # noqa: E402
gv = gd.gv

FAILURES = []
PROBES = [0]

CC = "ICurrencyConverter.Convert"
SRC = "Billing/RateTableConverter.cs"
ARROW = "→"
TREE = "├──"


def expect(label, condition, detail=""):
    PROBES[0] += 1
    if not condition:
        FAILURES.append(label + ((" -- " + detail) if detail else ""))


def bash(command):
    return gv.Event(1, "tool", {"name": "Bash", "input": {"command": command}})


def tool(name, tool_input):
    return gv.Event(1, "tool", {"name": name, "input": tool_input})


def result(text):
    return gv.Event(1, "result", text)


def writes(command):
    return bool(gd.edits_production(bash(command)))


def expect_writes(command, label):
    expect(label, writes(command), "edits_main said no for: " + command)


def expect_reads(command, label):
    expect(label, not writes(command), "edits_main said write for: " + command)


# --- shell commands that only READ Billing: must not count as edits ---

def probe_read_only_commands_are_not_edits():
    expect_reads("sed -n '1,20p' " + SRC + " 2>&1", "sed -n read with 2>&1 is a read")
    expect_reads("cat " + SRC + " > /tmp/copy.cs", "cat Billing redirected to /tmp is a read")
    expect_reads("cat " + SRC + " >/dev/null 2>&1", "cat Billing silenced to /dev/null is a read")
    expect_reads("cp " + SRC + " /tmp/backup.cs", "cp with Billing as SOURCE is a read")
    expect_reads("cp -n " + SRC + " /tmp/a.cs 2>&1", "cp Billing as source with 2>&1 is a read")
    expect_reads("grep -n 'a>b' " + SRC, "grep for a '>' in Billing is a read")
    expect_reads('grep -n "->" ' + SRC, "grep for '->' in Billing is a read")
    expect_reads("ls > /tmp/list.txt && cat " + SRC, "redirect before && then read of Billing")
    expect_reads("find Billing -name '*.cs' > /tmp/files.txt", "find Billing redirected to /tmp")
    expect_reads("diff " + SRC + " /tmp/B.cs > /tmp/d.txt", "diff of Billing into /tmp is a read")
    expect_reads('echo "see ' + SRC + '" > notes.txt', "echo naming Billing into notes is a read")
    expect_reads("cat " + SRC + " | tee /tmp/copy.txt", "tee of Billing output to /tmp is a read")
    expect_reads("cat " + SRC + "; echo done > /tmp/x", "redirect after ; then read of Billing")
    expect_reads("echo x > BillingX/X.cs", "write to BillingX (not Billing/) is not Billing")


def probe_read_tools_on_src_main_are_not_edits():
    expect("Read tool on Billing is not an edit",
           not gd.edits_production(tool("Read", {"file_path": "/w/" + SRC})))
    expect("Edit on BillingX is not an edit (near-miss sibling)",
           not gd.edits_production(tool("Edit", {"file_path": "/w/BillingX/X.cs"})))
    expect("Write on BillingX is not an edit (near-miss sibling)",
           not gd.edits_production(tool("Write", {"file_path": "/w/BillingX/X.cs"})))
    expect("a Bash call with no command is not an edit",
           not gd.edits_production(tool("Bash", {})))
    expect("a Bash call with null input is not an edit",
           not gd.edits_production(gv.Event(1, "tool", {"name": "Bash", "input": None})))
    expect("a result event is never an edit",
           not gd.edits_production(result("sed -i x Billing/A.cs")))


# --- shell commands that WRITE Billing: must count ---

def probe_writing_commands_are_edits():
    expect_writes("sed -i.bak 's/a/b/' Billing/X.cs", "sed -i.bak writes Billing")
    expect_writes("sed --in-place 's/a/b/' Billing/X.cs", "sed --in-place writes Billing")
    expect_writes("sed -i 's/a/b/' \"Billing/X.cs\"", "sed -i with double-quoted path")
    expect_writes("sed -i 's/a/b/' 'Billing/X.cs'", "sed -i with single-quoted path")
    expect_writes("cat <<EOF > Billing/X.cs\nclass X { }\nEOF", "heredoc redirect into Billing")
    expect_writes("cat <<'EOF' >\"Billing/X.cs\"\nclass X { }\nEOF", "heredoc into quoted Billing")
    expect_writes("printf 'x' >> Billing/X.cs", "append redirect into Billing")
    expect_writes("printf 'x' >> \"Billing/X.cs\"", "append redirect into quoted Billing")
    expect_writes("printf 'x'>>Billing/X.cs", "append redirect with no spaces")
    expect_writes("echo x | tee -a Billing/X.cs", "tee -a into Billing")
    expect_writes("echo x | tee Billing/X.cs", "tee into Billing")
    expect_writes("perl -pi -e 's/a/b/' Billing/X.cs", "perl -pi in place on Billing")
    expect_writes("cp /tmp/Fixed.cs Billing/X.cs", "cp with Billing as destination")
    expect_writes("mv /tmp/Fixed.cs Billing/X.cs", "mv with Billing as destination")
    expect_writes("cp /tmp/Fixed.cs 'Billing/X.cs'", "cp to quoted Billing destination")


def probe_write_tools_on_src_main_are_edits():
    expect("Edit tool on an absolute Billing path is an edit",
           gd.edits_production(tool("Edit", {"file_path": "/w/" + SRC})))
    expect("MultiEdit on Billing is an edit",
           gd.edits_production(tool("MultiEdit", {"file_path": "/w/" + SRC})))
    expect("Write tool on Billing is an edit",
           gd.edits_production(tool("Write", {"file_path": "/w/Billing/X.cs", "content": "x"})))


# --- writes the code misses or wrongly takes for writes ---

def probe_writes_the_grader_misses():
    # Found by the adversarial pass: the mv/cp rule requires the destination to be the LAST token, so a
    # trailing redirect ("2>&1" or "2>/dev/null") after a real write into Billing hides it.
    expect_writes("cp /tmp/Fixed.cs Billing/X.cs 2>&1",
                  "cp into Billing followed by 2>&1 is still a write")
    # Found by the adversarial pass: the sed rule only accepts the flag directly after 'sed'; "sed -E -i" is
    # an in-place edit that the pattern never sees.
    expect_writes("sed -E -i 's/a/b/' Billing/X.cs",
                  "sed -E -i is an in-place edit of Billing")
    # Found by the adversarial pass: the sed rule searches for Billing only AFTER the flag, so a 'cd' into
    # Billing followed by an in-place edit of a file named relative to it is missed.
    expect_writes("cd " + "Billing" + " && sed -i 's/a/b/' X.cs",
                  "sed -i after cd into Billing is an edit of Billing")
    # Found by the adversarial pass: git checkout/restore/apply rewrite the working tree of Billing and the
    # shell pattern list names only sed, perl, tee, cp, mv and redirects.
    expect_writes("git checkout -- Billing/X.cs",
                  "git checkout -- rewrites Billing")
    # Found by the adversarial pass: a scripting interpreter writing into Billing is a write the grader ignores.
    expect_writes("python3 -c \"open('Billing/X.cs','w').write('x')\"",
                  "python3 writing Billing is an edit")
    # Found by the adversarial pass: an Edit path that reaches Billing through '..' is a write under Billing
    # once normalised, but the substring test only sees the literal "Billing/".
    expect("Write through a '..' segment into Billing is an edit",
           gd.edits_production(tool("Write", {"file_path": "/w/Billing.Tests/../Billing/X.cs", "content": "x"})))


def probe_src_main_as_argument_is_not_a_write():
    # Found by the adversarial pass: Billing/ here is a string inside the sed script; the write targets
    # build.gradle, so this is not an edit of production source.
    expect_reads("sed -i 's#Billing/#gen/#' Billing.Tests/Billing.Tests.csproj",
                 "Billing/ inside a sed script is not a write target")
    # Found by the adversarial pass: 'tee' here is the search term of grep, not the tee command.
    expect_reads("grep -rn tee Billing/", "grep for the word tee is a read")
    # Found by the adversarial pass: 'mv' here is the search term of grep; the last token is Billing/... so
    # the mv/cp rule matches a plain read.
    expect_reads("grep -n mv " + SRC, "grep for the word mv is a read")
    expect_reads('grep -rn "cp" Billing/', "grep for the word cp is a read")


# --- diverging_ids ---

def probe_diverging_ids():
    line = "#1.3 - ICurrencyConverter.Convert(euroCents: 4599, currency: \"CHF\") -> 4300"
    expect("the structural line names its own span", gd.diverging_ids([result(line)]) == {"#1.3"})

    narrative = "- **ICurrencyConverter.Convert**(euroCents: `4599`) -> `4300` #1.3"
    expect("the narrative line names its trailing span", gd.diverging_ids([result(narrative)]) == {"#1.3"})

    expect("agent text is not evidence the agent was shown the id",
           gd.diverging_ids([gv.Event(1, "text", "#1.3 - ICurrencyConverter.Convert(")]) == set())

    expect("a call line with no span id names nothing",
           gd.diverging_ids([result("ICurrencyConverter.Convert(euroCents: 1)")]) == set())

    # Found by the adversarial pass: the test is a substring test on the whole line, so convertAll matches.
    expect("convertAll is not the diverging call",
           gd.diverging_ids([result("#1.5 - ICurrencyConverter.ConvertAll(euroCents: 1)")]) == set())

    # Found by the adversarial pass: a longer class name that ends in CurrencyConverter is a different class.
    expect("MyCurrencyConverter is not the diverging call",
           gd.diverging_ids([result("#1.5 - MyICurrencyConverter.Convert(euroCents: 1)")]) == set())

    # Found by the adversarial pass: the span that mentions the diverging call in a VALUE is not the call.
    expect("a parent span quoting the call in a value is not the diverging span",
           gd.diverging_ids([result("#1.2 - Checkout.run(note: \"ICurrencyConverter.Convert\") -> ok")]) == set())

    # Found by the adversarial pass: found[-1] takes the last id on the line, so an id inside a value wins.
    expect("an id inside a value is not a diverging id",
           gd.diverging_ids([result("#1.3 - ICurrencyConverter.Convert(note: \"see #9\") -> 4300")]) == {"#1.3"})


# --- prose ---

def probe_prose():
    numbered = "    12\t#1.3 - A.b(x: 1)"
    expect("a numbered structural line is a quote, not a claim", "#1.3" not in gd.prose(numbered))
    arrow = "    12" + ARROW + "#1.3 - A.b(x: 1)"
    expect("a structural line numbered with an arrow is a quote", "#1.3" not in gd.prose(arrow))
    narrative = "- **ICurrencyConverter.Convert**(euroCents: `4599`) -> `4300` #1.3"
    expect("a markdown narrative line is a quote, not a claim", "#1.3" not in gd.prose(narrative))
    tree = TREE + " ICurrencyConverter.Convert(euroCents: 4599) #1.3"
    expect("a tree-rendered narrative line is a quote, not a claim", "#1.3" not in gd.prose(tree))
    sentence = "The rounding happens at #1.3."
    expect("a plain sentence citing #1.3 keeps its citation", "#1.3" in gd.prose(sentence))
    mixed = "The rounding happens at #1.3.\n    12\t#1.3 - A.b(x: 1)\nand #1.3 is the cause"
    kept = gd.prose(mixed)
    expect("a mixed reply keeps both prose citations and drops only the quoted line",
           kept.count("#1.3") == 2, repr(kept))
    # Found by the adversarial pass: the narrative pattern is searched anywhere in the line, so an agent's own
    # sentence that quotes a tree line mid-sentence loses its span citation.
    claim = "Root cause at #1.3, where the value went 4599 to 4300 near " + TREE + " ICurrencyConverter.Convert("
    expect("a claim that quotes a call mid-sentence keeps its span citation",
           "#1.3" in gd.prose(claim), repr(gd.prose(claim)))


# --- shape_of ---

def probe_shape_of():
    plain = "- **Order.place**(id: `42`, note: `ok`) -> `done` #1.3"
    expect("a simple narrative call line gives its trailing id and names",
           gd.shape_of(plain) == "#1.3 Order.place(id, note)", repr(gd.shape_of(plain)))

    parens = "- **Order.place**(id: `f(x)`, note: `ok`) -> `done` #1.3"
    expect("parentheses inside a value do not change the shape",
           gd.shape_of(parens) == "#1.3 Order.place(id, note)", repr(gd.shape_of(parens)))

    hashed = "- **Order.place**(ref: `#2`, id: `42`) -> `done` #1.3"
    expect("an id inside a value does not replace the span's own trailing id",
           gd.shape_of(hashed) == "#1.3 Order.place(ref, id)", repr(gd.shape_of(hashed)))

    no_params = "- **Order.place**() -> `ok` #1.3"
    expect("a call with no parameters has an empty name list",
           gd.shape_of(no_params) == "#1.3 Order.place()", repr(gd.shape_of(no_params)))

    # Found by the adversarial pass: the parameter-name scan is a findall over the whole parameter text, so
    # "x: `" inside a value is taken for a parameter called x.
    colon = "- **Order.place**(ref: `x: `, id: `42`) -> `done` #1.3"
    expect("a value containing ': `' adds no parameter name",
           gd.shape_of(colon) == "#1.3 Order.place(ref, id)", repr(gd.shape_of(colon)))

    # Found by the adversarial pass: with no trailing span id, the only id on the line is the one inside a
    # value, and it is reported as the call's own span.
    no_trailing = "- **Order.place**(ref: `#2`, id: `42`) -> `done`"
    expect("a call line with no span id of its own has no shape",
           gd.shape_of(no_trailing) is None, repr(gd.shape_of(no_trailing)))

    expect("a plain line that mentions a call without bold has no shape",
           gd.shape_of("Order.place(id: 42) #1.3") is None)
    expect("a line with an id and no call has no shape", gd.shape_of("plain text #1.3") is None)


# --- is_skill_load ---

def probe_is_skill_load():
    expect("the Skill tool loading narrativetrace-debug is a load",
           gd.is_skill_load(tool("Skill", {"skill": "narrativetrace-debug"})))
    expect("reading the debug SKILL.md is a load",
           gd.is_skill_load(tool("Read", {"file_path": "/w/.claude/skills/narrativetrace-debug/SKILL.md"})))
    expect("reading the verify SKILL.md is not a load",
           not gd.is_skill_load(tool("Read", {"file_path": "/w/.claude/skills/narrativetrace-verify/SKILL.md"})))
    expect("a result carrying the skill's page is not a load",
           not gd.is_skill_load(result("narrativetrace-debug/SKILL.md")))
    expect("a text mention of the skill is not a load",
           not gd.is_skill_load(gv.Event(1, "text", "I will use narrativetrace-debug")))
    expect("a Bash cat of the debug page counts as reading the skill's page",
           gd.is_skill_load(bash("cat /w/.claude/skills/narrativetrace-debug/SKILL.md")))
    # Found by the adversarial pass: a substring test accepts a different skill whose name extends this one.
    expect("narrativetrace-debug-old is not the debug skill",
           not gd.is_skill_load(tool("Skill", {"skill": "narrativetrace-debug-old"})))
    # Found by the adversarial pass: the test reads the whole input, so args naming the debug skill count for
    # a load of the verify skill.
    expect("loading verify with debug in args is not a debug load",
           not gd.is_skill_load(tool("Skill", {"skill": "narrativetrace-verify", "args": "narrativetrace-debug"})))


def main():
    probe_read_only_commands_are_not_edits()
    probe_read_tools_on_src_main_are_not_edits()
    probe_writing_commands_are_edits()
    probe_write_tools_on_src_main_are_edits()
    probe_writes_the_grader_misses()
    probe_src_main_as_argument_is_not_a_write()
    probe_diverging_ids()
    probe_prose()
    probe_shape_of()
    probe_is_skill_load()
    if FAILURES:
        for failure in FAILURES:
            print("FAIL " + failure)
        print("%d of %d probes failed" % (len(FAILURES), PROBES[0]))
        sys.exit(1)
    print("all %d probes passed" % PROBES[0])


if __name__ == "__main__":
    main()
