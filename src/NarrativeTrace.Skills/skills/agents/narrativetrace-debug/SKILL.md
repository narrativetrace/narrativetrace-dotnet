---
name: narrativetrace-debug
description: "Finds the cause of a wrong result in a .NET project by reading what the code did with the values, not by stepping through it. Use when a symptom is reported — a wrong amount, a wrong id, a call in the wrong order, a test that fails with a value nobody expected. Reproduces it with the smallest input and NarrativeTrace on, finds the first span where a value diverges and names it by its span id (the sequence diagram first when threads are involved), narrows to that span's sub-tree, fixes it there and checks the structural trace shows nothing else moved, pins the reproduction as a regression test and an approval baseline behind your yes, and reports the root cause by span id. Hands a defect in NarrativeTrace itself to narrativetrace-feedback. Say 'debug this with the trace', 'find where this value goes wrong', or 'why is this result wrong' to invoke it."
when_to_use: "Non-obvious triggers: a support ticket quoting a wrong amount or total; a rounding, currency or timezone difference between what was expected and what happened; a value that is right going into a service and wrong coming out; a flaky ordering between threads."
---

# narrativetrace-debug

## 1. Reproduce the symptom with tracing on

**when:** a test already drives the path with the input from the symptom, run that one; otherwise write the smallest one, as below — the reported input, through the real collaborators each wrapped with the fixture's context, asserting the value the symptom says should have come out

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

**verify:** dotnet test --filter "FullyQualifiedName~<the test that reproduces the symptom>"

**failure:** no .md for the test appears under bin/<configuration>/<tfm>/TestResults/narrativetrace/traces — the xUnit test neither runs its body through NarrativeFixture.Run nor calls WriteArtifacts, or the collaborators on the path are not wrapped with the fixture's context — fix: write the test as the listing does; run narrativetrace-doctor and apply its fix, then run the test again

## 2. Find the symptom in the values

**verify:** the reported value is in this run's the .md narrative under bin/<configuration>/<tfm>/TestResults/narrativetrace/traces, or the reproducing test fails on it — a symptom that does not reproduce is said so, and the loop stops here

## 3. Across threads, read the sequence diagram first

**when:** only when the path crosses threads — the .nt shows a fork, async or fire-and-forget marker — or the symptom is about order (a call that ran before or after another); otherwise go straight to localizing

**verify:** the reproduction's .mmd under bin/<configuration>/<tfm>/TestResults/narrativetrace/diagrams was read before any span's values, and the reply says which call ran before which across the threads, by the span id in each call's note — the span it points to is the one localized next

## 4. Localize by reading: name the first span where a value diverges

**verify:** the reproduction's spans in the .md narrative under bin/<configuration>/<tfm>/TestResults/narrativetrace/traces were read from the root down until the first one whose inputs are what the symptom implies but whose result, the value it passes on, or the branch it takes is not; the reply names that span by its id (#1.3) and the boundary — the collaborator, the parameter or return, the value that arrived and the value that left — before any code is changed: by reading, not by stepping through a debugger or adding prints

## 5. Bisect by span, not by file

**when:** only when the value went into the diverging span right and came out wrong, and what happens in between is more than that span's own few lines; otherwise the diverging span is the defect — go on to the fix

```bash
dotnet test --filter "FullyQualifiedName~<the test that reproduces the symptom>"
```

**verify:** only the sub-tree under the diverging id was read on each re-run — the spans whose id begins with it (#1.3, #1.3.1, #1.3.2) — and where the work inside that span is not traced, the collaborator it calls was wrapped with NarrativeTraceProxy.Create in the reproducing test, as the listing above wraps its service, and the run repeated, until the divergence sits in the smallest span that has it; never [NotTraced] to narrow — in .NET it redacts a value, it does not scope a trace

## 6. Hand a defect in NarrativeTrace itself to narrativetrace-feedback

**when:** only when the trace and the code disagree — a call the code makes has no span, a span shows a value the code did not pass, one span has two ids in two flavours — or the diverging span is inside NarrativeTrace; otherwise go on to the fix

**verify:** narrativetrace-feedback was started with the span id and the value-free .nt — never a value from the trace — and the project's code was not changed to work around it; the loop ends with that hand-off

## 7. Fix it in the diverging span, re-run the same input, read the same span

```bash
dotnet test --filter "FullyQualifiedName~<the test that reproduces the symptom>"
```

**verify:** the change is in the code of that span — the method the diverging id names, or what it calls — and the reproducing test passes; the same span, by the same id, now carries the value the symptom implied; a change anywhere else that makes the test pass silences the symptom and leaves the defect, so it is undone

## 8. Check that nothing else moved

**verify:** the fixed run's .nt under bin/<configuration>/<tfm>/TestResults/narrativetrace/structural was read whole and compared, line by line, with the call lines of the reproduction's .md — a red run writes no .nt (the .nt on disk is the last green one), so the shape before the fix is the .md's calls and ids without their values: the same calls in the same order under the same ids; a value fix moves no line of a value-free trace, and every line that did move is named by its id in the reply and either explained by the fix or undone

## 9. Keep the reproduction as the regression test

**verify:** the reproducing test stays in the suite with the input from the symptom and asserts the value the fixed span now carries — not only that nothing throws — so it fails when the fix is undone; its structural trace is what the pin below makes the baseline

## 10. Turn approval mode on

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

## 11. Reference the run settings from the test project

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

## 12. Run the suite in approval mode and show every .received.nt

```bash
dotnet test
```

**verify:** this runs in the same turn as the structural read, before the report — the pin does not wait for a yes, only the promotion does; approval mode compares every test that writes its artifacts, not only the one this skill ran, so the run that writes the review copies is the whole suite; the first run of a test with no baseline fails on purpose — that failure is what writes its review copy, as narratives/<TestClass>/<scenario>.received.nt beside the test project — and the whole text of each .received.nt that run wrote is in the reply, not a summary of it — it holds names and shape and no value, which is why it is safe to commit once approved; where a .approved.nt already existed, the reply also names what the delta changed, by span id, in the program's own words, and whether it was meant

## 13. Report the root cause as the trace showed it, before the question

**verify:** the root cause in the user's own terms — the span id where it diverged, the value that arrived and the value that left, the branch it took — and what the fix changed in that span; every claim cites the span id it rests on, from the .nt or .md read in this session: a claim without an id is not a claim. The report is written in full before the pin question, where the gate puts it, and the closing reply after the promotion names the span id again in its one-line summary of the cause

## 14. Ask once whether to pin it, then stop the turn

**verify:** the reply ends with the question and nothing after it — the answer is the user's next message, never something assumed in this one. Do not run the approve verb before the user says yes — promoting is the pinning. And everything else — the report, every caveat, what you will do on a yes — goes before the question; the question is the reply's last line, with no sentence after it: end the reply with exactly the line "Shall I pin this as the approved baseline?" (in the user's language).

## 15. Promote what was shown, and nothing else

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
- Narrow to one span before reading its values (debugging is where values pay for themselves, but only on the span that diverged — a whole trace of values buries the one that matters)
- Show the whole .received.nt before asking anything (the baseline becomes the contract every later change is held to, and a person can only approve what they have actually read)

## Never
- Never change code before the diverging span is named (a fix made before the trace says where the value went wrong is a guess, and a guess that turns the test green hides the defect it missed)
- Never make the symptom go away somewhere other than the diverging span (a correction downstream, a caught exception or a changed expectation silences the symptom and leaves the defect for the next caller of that span)
- Never turn redaction off to see more (a redacted value that matters is reasoned about by its name and shape; turning redaction off puts the user's secrets in the transcript)
- Never promote a baseline in the turn that asked (approval is the user's next message — a yes assumed in the same turn is not one)
- Never edit the .received.nt after showing it (what was approved has to be what is promoted, so a changed run is rendered again and shown again)
- Never commit a .received.nt (it is the review copy; the committed contract is the .approved.nt)

