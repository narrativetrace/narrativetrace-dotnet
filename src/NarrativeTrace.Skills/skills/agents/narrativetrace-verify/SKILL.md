---
name: narrativetrace-verify
description: "Verifies a change in a .NET project by reading what the code actually did before saying it is done. Use after the tests are green and before reporting a change that crosses collaborators, branches, retries, runs async, carries state between calls, or touches code not written in this session — and skip it, saying why, for a pure function or a one-class edit. Writes the intent down first, runs the smallest real path with NarrativeTrace on, reads the value-free structural trace against the intent, opens values only on the span that looks wrong, fixes and re-reads, then pins the flow as an approval baseline behind your yes and reports what the trace showed, citing span ids. Say 'verify this change with the trace', 'check what the code actually did', 'did the flow do what I meant', or 'pin this flow as a baseline' to invoke it."
when_to_use: "Non-obvious triggers: the suite is green but the change touched more call sites than it added; a notification, payment or retry path changed; a .received.nt appeared after a test run; you are about to write 'tests pass' as the whole report."
---

# narrativetrace-verify

## 1. Decide whether to trace, and say so

**verify:** before anything runs, the reply says which it is: 'tracing: <the reason>' when the change crosses two or more collaborators over a boundary, branches, retries, runs async or concurrently, carries state between calls, touched more call sites than it added, or includes code not written in this session; or 'skipping narrativetrace-verify: <a pure function | a one-class edit with no collaborator | a flow one test already walks end to end>' — a skip ends the skill here, and that sentence is the report. A whole flow's .nt is dozens of lines: cheap where the path is not obvious, waste where it is

## 2. Write the intent down before running anything

**verify:** three to six lines in the reply, under the word Intent, written before the first traced run: which collaborators the change touches, in which order, under which branch, how many times — the oracle the trace is read against, never edited after the run

## 3. Run the smallest real path with tracing on

**when:** the project already has a test that drives the changed path through its real collaborators and writes its artifacts, run that one — again, if it already ran: a trace from a run made before the Intent was written does not count; otherwise write the smallest one, as below

<!-- snippet: tests/NarrativeTrace.Examples.SixtySeconds.Tests/PlaceOrderFlowTests.cs -->
```csharp
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Examples.SixtySeconds.Tests;

// The smallest test that drives the real path: the collaborators the change touched, wired as
// production wires them, each wrapped with the fixture's context. WriteArtifacts writes the shape to
// bin/<configuration>/<tfm>/TestResults/narrativetrace/structural/<TestClass>/<scenario>.nt and the
// values beside it under traces/; a red run writes the values and keeps the last green .nt.
public sealed class PlaceOrderFlowTests
{
    [Fact]
    public void Customer_places_an_order()
    {
        using var trace = new NarrativeFixture();
        var failed = true;
        try
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), trace.Context);

            Assert.Equal("confirmed:cust-1:book-123:2", orders.PlaceOrder("cust-1", "book-123", 2));
            failed = false;
        }
        finally
        {
            trace.WriteArtifacts(nameof(PlaceOrderFlowTests), nameof(Customer_places_an_order), failed);
        }
    }
}
```
<!-- /snippet -->

**verify:** dotnet test --filter "FullyQualifiedName~<the smallest test that drives the real path>"

**failure:** no .nt for the test appears under bin/<configuration>/<tfm>/TestResults/narrativetrace/structural — the xUnit test neither runs its body through NarrativeFixture.Run nor calls WriteArtifacts, the collaborators on the path are not wrapped with the fixture's context, or NARRATIVETRACE_OUTPUT=false turned the files off — fix: write the test as the listing does; run narrativetrace-doctor and apply its fix, then run the test again

## 4. Read the structural trace first, against the intent

**verify:** the .nt of the test just run — under bin/<configuration>/<tfm>/TestResults/narrativetrace/structural in the test project — was opened and read whole before any value was looked at, and the reply walks it against the intent — calls, order, branch, multiplicity — naming every match and every mismatch by its span id (#2.1); the shapes below are the checklist

## 5. Open values on the span that looks wrong, and only there

**when:** when the structural read named a span that does not match the intent, or the shape matches but a symptom points at data — an assertion failed on a value, the output is wrong — then open the one span the symptom points at; otherwise go on to the pin

**verify:** only the flagged span was read in the .md narrative under bin/<configuration>/<tfm>/TestResults/narrativetrace/traces, found by the id the .nt gave it — not the whole file; a [REDACTED] value stays redacted

## 6. Fix, re-run, read again

**when:** only when the structural read named a span that does not match the intent, or the value read in step 5 was wrong; otherwise go on to the pin

```bash
dotnet test --filter "FullyQualifiedName~<the smallest test that drives the real path>"
```

**verify:** the same test ran again after the fix and its new .nt was read whole: the span that was wrong now matches the intent, and a fix that changed the shape was read again from the structural read

## 7. Turn approval mode on

**when:** if approval mode is off — the test project references no run settings that set NARRATIVETRACE_APPROVAL, or the doctor's config.approval-mode finding fails; switching it on is part of the pin, not a change beyond the task, so do it now without asking first — the one question comes after the review copy is shown; when it is already on, go straight to the run

<!-- snippet: tests/NarrativeTrace.Examples.SixtySeconds.Tests/approval.runsettings -->
```xml
<?xml version="1.0" encoding="utf-8"?>
<!-- Approval mode for every test run of this project: a passing test whose traced structure
     differs from its committed narratives/<TestClass>/<scenario>.approved.nt fails with the diff. -->
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```
<!-- /snippet -->

**verify:** the test project's directory carries this file as approval.runsettings

## 8. Reference the run settings from the test project

**when:** if approval mode is off — the test project references no run settings that set NARRATIVETRACE_APPROVAL, or the doctor's config.approval-mode finding fails; switching it on is part of the pin, not a change beyond the task, so do it now without asking first — the one question comes after the review copy is shown; when it is already on, go straight to the run

<!-- snippet: tests/NarrativeTrace.Examples.SixtySeconds.Tests/NarrativeTrace.Examples.SixtySeconds.Tests.csproj region=approval-mode -->
```xml
<PropertyGroup>
  <RunSettingsFilePath>$(MSBuildProjectDirectory)/approval.runsettings</RunSettingsFilePath>
</PropertyGroup>
```
<!-- /snippet -->

**verify:** the test project's .csproj carries the PropertyGroup above, and .gitignore carries the line **/*.received.nt so a review copy is never committed

**failure:** the suite still passes and no .received.nt appears under narratives/ — the run settings are not referenced, or no xUnit test runs its body through NarrativeFixture.Run or calls WriteArtifacts, so nothing is compared — fix: check the doctor's config.approval-mode finding and apply its fix; write the flow test as the listing above does, then run the suite again

## 9. Run the suite in approval mode and show every .received.nt

```bash
dotnet test
```

**verify:** this runs in the same turn as the structural read, before the report — the pin does not wait for a yes, only the promotion does; approval mode compares every test that writes its artifacts, not only the one this skill ran, so the run that writes the review copies is the whole suite; the first run of a test with no baseline fails on purpose — that failure is what writes its review copy, as narratives/<TestClass>/<scenario>.received.nt beside the test project — and the whole text of each .received.nt that run wrote is in the reply, not a summary of it — it holds names and shape and no value, which is why it is safe to commit once approved; where a .approved.nt already existed, the reply also names what the delta changed, by span id, in the program's own words, and whether it was meant

## 10. Report what the trace showed, before the question

**verify:** two sentences on what the trace showed on the path this run took, every claim citing the span id it rests on — a claim without an id is not a claim, and only ids in the .nt that was read count — with the .nt attached or quoted; the report names the branches and inputs this run did not exercise, because one trace shows one path, not every execution; 'tests pass' alone is not the report. It is written in this reply, after the review copy and before the pin question

## 11. Ask once whether to pin it, then stop the turn

**verify:** the reply ends with the question and nothing after it — the answer is the user's next message, never something assumed in this one. Do not run the approve verb before the user says yes — promoting is the pinning. And everything else — the report, every caveat, what you will do on a yes — goes before the question; the question is the reply's last line, with no sentence after it: end the reply with exactly the line "Shall I pin this as the approved baseline?" (in the user's language).

## 12. Promote what was shown, and nothing else

```bash
dotnet tool restore
dotnet tool run dotnet-narrativetrace approve
```

**verify:** each .approved.nt now holds exactly the text that was shown and no .received.nt is left beside it — git status --short lists the new or changed .approved.nt files under narratives/ and nothing else of the pin's; those are what get committed — and dotnet test passes: the suite is green again after the promotion

## Which flavour answers which question

| flavour | where | carries | answers |
|---|---|---|---|
| structural `.nt` | `bin/<configuration>/<tfm>/TestResults/narrativetrace/structural/<TestClass>/<scenario>.nt`, under the test project | shape only: calls, order, nesting, parameter names, multiplicity, span ids — dozens of lines for a whole flow | did the flow do what I meant? |
| Markdown narrative | `bin/<configuration>/<tfm>/TestResults/narrativetrace/traces/<TestClass>/<scenario>.md` | values (redacted), outcomes, durations | what value crossed this boundary? — read one span, by id |
| indented text | `IndentedTextRenderer`, the console and a failing test's output | the same values as plain text | the same question, in a console or a failure message |
| sequence diagram | `bin/<configuration>/<tfm>/TestResults/narrativetrace/diagrams/<TestClass>/<scenario>.mmd` | who called whom, in order, across threads | ordering across components and threads |
| approval delta | the failing test's message: `.received.nt` against `narratives/<TestClass>/<scenario>.approved.nt`, beside the test project | what changed in the shape, citing both sides' ids | is this change intended? |
| prose | `ProseRenderer` | narration for a person | explaining the flow to the user — never read it to check the code |

Use the cheapest flavour that answers the question, and look at values only where the
shape says to look. A span id (`#1`, `#1.3`, `#1.3.2`) is the span's position in the tree
and the same in every flavour: find in the `.md` the span the `.nt` flagged by its id. A
test writes these files when it ends: an NUnit test deriving from `NarrativeTestBase` always,
an xUnit test when its body runs through `NarrativeFixture.Run` or it calls `WriteArtifacts`
(the listing above does); a red run writes the `.md` and the diagram and keeps the last green `.nt`. Redaction stays on — the
deny-list and `[REDACTED]` are never turned off to see more; a redacted value that matters
is reasoned about by its parameter name and the shape around it.

## Shapes that mean something went wrong

- a call made twice that the intent makes once
- a call before its precondition — a notification before the payment that it announces
- a branch never taken that the intent takes
- a retry that masks a failure
- a side effect inside a loop
- a swallowed exception: a thrown outcome `!!` under a call that returned normally
- a cleanup that never ran
- a value crossing a boundary that should have been redacted

## Always
- Cite a span id for every claim about the trace (an id points at one span in every flavour, so a reviewer can check the claim; a claim without one cannot be checked)
- Use the cheapest flavour that answers the question (the structural trace first and a value on one span only is what keeps the common case near zero tokens)
- Show the whole .received.nt before asking anything (the baseline becomes the contract every later change is held to, and a person can only approve what they have actually read)

## Never
- Never report a change as done on green tests alone once this skill decided to trace (the suite checks what someone thought to assert; the trace shows what the code did)
- Never read a trace against nothing (an intent written after the run bends to whatever happened — that is why it comes first)
- Never turn redaction off to see more (a redacted value that matters is reasoned about by its name and shape; turning redaction off puts the user's secrets in the transcript)
- Never promote a baseline in the turn that asked (approval is the user's next message — a yes assumed in the same turn is not one)
- Never edit the .received.nt after showing it (what was approved has to be what is promoted, so a changed run is rendered again and shown again)
- Never commit a .received.nt (it is the review copy; the committed contract is the .approved.nt)

