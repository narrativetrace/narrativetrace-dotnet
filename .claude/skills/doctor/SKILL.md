---
name: doctor
description: "Diagnoses a NarrativeTrace .NET install without changing anything: runs the doctor CLI and explains every failing check and its fix, proves redaction with a real test, opens the newest rendered trace before trusting any assertion against it, and — flagged unstudied — walks an approval-trace diff. Use when traces aren't appearing, output looks wrong, a value that should be redacted shows up in a trace, tests pass but the trace looks off, or someone asks \"why isn't NarrativeTrace working\", \"is my NarrativeTrace install broken\", \"run narrativetrace doctor\", \"check my NarrativeTrace setup\", or \"diagnose narrative trace\"."
when_to_use: "A NarrativeTrace install already exists in the project and something needs diagnosing — no traces are appearing, a check is failing, or a value that should be redacted appears in output."
allowed-tools: dotnet, git
---

# narrativetrace-doctor

## 1. Run the doctor CLI and read its report — every failing check names a fix and a doc link

```bash
dotnet tool run dotnet-narrativetrace doctor --json
```

**verify:** the JSON report's findings array has one entry per registered check, and every entry with "passed": false carries a non-empty "fix"

**failure:** command not found: dotnet-narrativetrace — NarrativeTrace.Cli is not installed as a local tool in this project yet — fix: run the add-narrative-tracing skill's install step, or `dotnet tool install NarrativeTrace.Cli`

## 2. Prove redaction in a test — render a call with a deny-listed parameter name and assert on the output, never just trust the deny-list

```bash
dotnet test
```

**verify:** re-running the doctor CLI, the finding with id "trap.redaction-proof" reports "passed": true

**failure:** trap.redaction-proof still fails after adding a test — the test asserts on a parameter name that isn't deny-listed, or the test project isn't discovered by `dotnet test` — fix: use a deny-listed name like "password" or "token", and assert a neighboring, non-sensitive value is still present so an over-broad redaction also fails

## 3. Read the newest rendered trace under the output directory before writing any assertion against it

```bash
dotnet test
```

## 4. Approval flow: diff the structural trace, not just values

```bash
dotnet tool run dotnet-narrativetrace doctor --json
```

**verify:** the finding with id "trap.approval-traces" reports "passed": true — no stale *.received.nt files remain

**failure:** a *.received.nt file sits beside its *.approved.nt baseline — a traced call's structure changed and approval mode caught it — fix: review the diff, promote it once it's reviewed, or delete the *.received.nt file if the change was wrong — never commit it

**Flagged:** unstudied — eval cell pending

## Always
- Run the doctor CLI before hand-diagnosing a NarrativeTrace problem (the checks are tested and stable-ided; guessing re-derives what a check already verified)
- Read the newest rendered file under the output directory before asserting against it (a stale or wrong-directory trace makes any assertion against it meaningless)

## Never
- Never commit a *.received.nt file (it is an unreviewed diff, not a baseline — committing it hides the question it was raised to ask)
- Never widen redaction until a neighboring, non-sensitive value is proven still present (an over-broad deny-list silently hides real evidence, the same failure mode as under-redaction)

