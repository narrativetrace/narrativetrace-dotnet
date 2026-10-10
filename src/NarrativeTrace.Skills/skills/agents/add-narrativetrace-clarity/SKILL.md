---
name: add-narrativetrace-clarity
description: "Adds or verifies NarrativeTrace Clarity in a .NET project. Use when you want a first static naming report, per-element explanations, or an explicit clarity quality gate. Runs the real clarity-scan and clarity-check verbs, checks fresh nonempty JSON and Markdown, applies the report's renames and re-runs until the gate is clean, and preserves existing thresholds. Say \"add a clarity report\", \"run the clarity scan\", \"explain our clarity scores\", or \"set up a clarity quality gate\" to invoke it; route tracing itself being broken to narrativetrace-doctor."
when_to_use: "A project has NarrativeTrace or needs a first clarity report, score explanation, or optional build enforcement."
allowed-tools: Bash(dotnet *), Bash(git *)
---

# add-narrativetrace-clarity

## 1. Inspect the existing project setup

**verify:** Read the target project file's target framework, NarrativeTrace references, tool manifest, glossary, and any ClarityMinScore, ClarityMaxHighIssues or ClarityWarnOnly property; preserve them. Run commands in the project's own directory.

Choose the path that matches the request:
- First naming report: scan, read both reports, and explain them.
- Names the gate flags: rename by the report's suggestions and re-run until it is clean.
- Build enforcement: only when the user asks for it.

## 2. Make the clarity gate available to this project

```bash
dotnet tool restore
```

**verify:** the restore succeeded and the tool manifest lists NarrativeTrace.Cli

**failure:** the restore finds no tool manifest — this project has never installed NarrativeTrace.Cli as a local tool — fix: run `dotnet new tool-manifest` and `dotnet tool install NarrativeTrace.Cli`, or run the add-narrative-tracing skill's install step first

## 3. Build, then run a static scan for a first naming report

```bash
dotnet build --configuration Release
dotnet tool run dotnet-narrativetrace clarity-scan --assembly bin/Release/<target framework>/<project>.dll --output-dir narrativetrace
```

**verify:** the scan reported a nonzero type count and wrote fresh reports from the built assembly, which it read by reflection without running anything

**failure:** the scan exits 1 naming an assembly that was not found — the assembly path does not match this project's target framework or configuration — fix: substitute the real path under the project's Release output and run the scan again

## 4. Check both report artifacts and read their contents

**verify:** narrativetrace/clarity-scan-results.json and the Markdown report beside it in narrativetrace/ are fresh, nonempty, and were read; the JSON has a nonempty scenario array, and the Markdown ranks the issues with their suggestions. These scan reports are distinct from the clarity results a test run aggregates

## 5. Explain scores, notes, and coverage

**verify:** Base the explanation on the report you read:

- Present a short table of type, observed score, and the report's exact issue. Label your own interpretation or rename advice separately as suggestions.
- overall (0–1) weights method names 30%, parameter names 25%, class names 20%, structural quality 15%, and cohesion 10%.
- A static scan reads depth-1 reflection: it assesses names and parameter counts but cannot establish runtime call depth. A captured trace answers that.
- An empty issues list means no scoring rule flagged an issue; it does not prove every name is unambiguous.

## 6. Apply the report's renames, then run the gate again until it is clean

```bash
dotnet build --configuration Release
dotnet tool run dotnet-narrativetrace clarity-scan --assembly bin/Release/<target framework>/<project>.dll --output-dir narrativetrace
dotnet tool run dotnet-narrativetrace clarity-check --results narrativetrace/clarity-scan-results.json --max-high-issues 0
```

**verify:** the gate exits 0 on a tree where each flagged name was renamed to the report's suggested shape and every caller, test and reference was updated to match; if a rename would change a published API, stop and ask first

**failure:** the gate still exits 1 after a rename — the new name is another generic verb or placeholder, or a flagged parameter was left behind — fix: read the report again, name the member for what it does in this project's own domain words, and re-run the three commands

## 7. Add build enforcement only when requested

```bash
dotnet build --target ClarityCheck --property ClarityMaxHighIssues=0
```

**verify:** the build runs the check and fails on a HIGH issue; the target shells out to dotnet-narrativetrace on PATH, so it needs the tool installed globally, not only in a local manifest. Existing thresholds stay as they were

**failure:** the build cannot start dotnet-narrativetrace — the tool is installed in a local manifest only, which never reaches PATH — fix: keep the gate as the CLI command in CI, or install the tool globally on the build agent

## 8. Hand missing tracing or output setup to the doctor

**verify:** if the reports stay missing or empty, run narrativetrace-doctor for diagnosis. When the user asked for setup or repair, apply the identified fix and repeat the verification; do not stop after merely naming the doctor

## Always
- Start from fresh output and inspect both artifacts (a successful command can still leave a stale or empty report)
- Rename by the report's suggestion and update every caller in the same change (a rename that leaves a caller behind trades a naming issue for a broken build)

## Never
- Do not delete, comment out or exempt a flagged member to make the gate pass (removing the evidence is not clean code; the gate is clean only when the names changed)
- Do not lower, remove or replace an existing threshold to hide a failure (enforcement is an explicit project choice and a failure is useful evidence)
- Do not rename a published API without asking first (a public member's name is a promise to other projects, and the gate cannot see them)
- Report observed scores without promising a score for an untested rename (a rename's score must be measured by another run)
- Do not harvest a glossary merely to read existing vocabulary (clarity reads a committed glossary; harvesting is a separate opt-in write)
- Do not call a missing report successful (the gate skips when the results file is absent, so artifact freshness must be checked)

