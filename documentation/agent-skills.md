# Agent Skills

Six packaged agent playbooks ship with this repository for Claude Code and
Codex CLI: `narrativetrace-doctor` (read-only diagnosis),
`add-narrative-tracing` (setup, end to end), `narrativetrace-feedback`
(report a defect in NarrativeTrace itself, with your approval),
`add-narrativetrace-clarity` (a first naming report, and the renames that clear the gate),
`narrativetrace-verify` (read what a change did before reporting it, and pin it behind your yes) and
`narrativetrace-debug` (find where a value goes wrong by reading the trace, fix it there, pin the regression). All are generated — never
hand-edited — from a typed catalogue in
[`NarrativeTrace.Skills.Catalogue`](../src/NarrativeTrace.Skills.Catalogue), the same way
`glossary.md` is generated from `glossary.json`.

## Where they live

- **Source of truth**: `src/NarrativeTrace.Skills.Catalogue/Catalogue/*.cs` — typed
  records (`Skill`, `SkillStep`, `ProListing`), not YAML or hand-written
  Markdown.
- **Rendered pages**: `.claude/skills/narrativetrace-doctor/SKILL.md`,
  `.claude/skills/add-narrative-tracing/SKILL.md`,
  `.claude/skills/narrativetrace-feedback/SKILL.md`,
  `.claude/skills/add-narrativetrace-clarity/SKILL.md`,
  `.claude/skills/narrativetrace-verify/SKILL.md` and
  `.claude/skills/narrativetrace-debug/SKILL.md` — the exact files Claude Code discovers. The
  same catalogue also renders Codex CLI's repository-level layout at
  `.agents/skills/narrativetrace-doctor/SKILL.md`,
  `.agents/skills/add-narrative-tracing/SKILL.md`,
  `.agents/skills/narrativetrace-feedback/SKILL.md`,
  `.agents/skills/add-narrativetrace-clarity/SKILL.md`,
  `.agents/skills/narrativetrace-verify/SKILL.md` and
  `.agents/skills/narrativetrace-debug/SKILL.md` — Codex scans
  `.agents/skills` from the working directory up to the repository root
  (developers.openai.com/codex/skills, redirects to learn.chatgpt.com/docs/build-skills; fetched
  2026-09-13). Every rendered page's directory name — on every platform — is the skill's own
  `CanonicalName`: skill names must be globally self-identifying, since Codex and Gemini have flat
  namespaces with no qualified fallback and a repo-level `.claude/skills/` directory is a flat
  namespace too, not a plugin (skills-design ruling, 2026-09-04, reaffirmed 2026-09-13). Committed
  build output; see [What to Commit](what-to-commit.md).
- **Regenerate**: `dotnet run --project skills-devtool -- render`.
- **Lint**: `dotnet run --project skills-devtool -- lint` — part
  of `./build.sh Verify`, every commit. Fails if a rendered page drifts from
  a fresh render, a step's command uses a tool outside this port's `dotnet`/
  `git` vocabulary, a description exceeds its budget, a skill whose steps can make something
  public pre-approves the command that does it (see `narrativetrace-feedback` below), a skill
  that promotes an approval baseline pre-approves any tool (see `narrativetrace-verify` below), or a
  Pro-tier listing disagrees with [`feature-guide.md`](feature-guide.md).

## Installing them in your own project

The published `NarrativeTrace.Cli` tool carries the same four skills this repository renders for
itself, plus a versionless `catalogue.json` — its `init` verb copies them into `.agents/skills/`
(and `.claude/skills/` too, once it detects `.claude/` or `CLAUDE.md`) and writes one marked
section into `AGENTS.md`. Zero network: the carrier travels inside the package already installed.

```bash
dotnet new tool-manifest
dotnet tool install NarrativeTrace.Cli
dotnet tool run dotnet-narrativetrace init --dry-run
```

Read the diff, then run the same command again without `--dry-run` to apply it. Nothing is ever
written by the `--dry-run` form. Against a fresh project the preview looks like this:

<!-- snippet: artifacts/agent-skills/init-preview.json -->
```json
{
  "carrier": "NarrativeTrace.Skills/0.3.0",
  "actions": [
    {"kind": "create", "path": ".agents/skills/narrativetrace-doctor/SKILL.md", "status": "planned"},
    {"kind": "create", "path": ".agents/skills/add-narrative-tracing/SKILL.md", "status": "planned"},
    {"kind": "create", "path": ".agents/skills/narrativetrace-feedback/SKILL.md", "status": "planned"},
    {"kind": "create", "path": ".agents/skills/add-narrativetrace-clarity/SKILL.md", "status": "planned"},
    {"kind": "create", "path": ".agents/skills/narrativetrace-verify/SKILL.md", "status": "planned"},
    {"kind": "create", "path": ".agents/skills/narrativetrace-debug/SKILL.md", "status": "planned"},
    {"kind": "create", "path": "AGENTS.md", "status": "planned"}
  ],
  "exitCode": 0
}
```
<!-- /snippet -->

`uninstall` removes exactly what `init` wrote — the provenance-stamped pages, the marked section,
and nothing else — and never touches anything it didn't create. Full verb, flag and exit-code
reference: [MSBuild & CLI Guide § init and uninstall](guides/msbuild-cli.md#init-and-uninstall).

**No CLI, or offline?** Copy `.claude/skills/*` and `.agents/skills/*` from this repository by hand
into the same paths in your own project — the rendered pages are exactly what `init` would write,
minus the marked `AGENTS.md` section and the provenance stamp.

## From a registry

A project can carry these skills without anyone here ever running the installer, in one of three
states:

1. **Installed by `init`** — committed, the team's. The only state `config.skills-installed`
   passes: the pages carry the provenance line and match the release this project resolves.
2. **A personal install from a registry** (a Claude Code plugin cache) — yours only. Invisible to
   the doctor by design: it diagnoses the project, and a personal install reaches no teammate and
   no other agent.
3. **A registry install into the project** (`npx skills add`) — this repository's own rendered
   pages, landed by a registry rather than by `init`, so they carry no provenance line yet.

Trying the skills yourself, without touching the project:

```text
/plugin marketplace add narrativetrace/narrativetrace-dotnet
/plugin install narrativetrace-dotnet@narrativetrace-dotnet
```

then run `dotnet tool run dotnet-narrativetrace init --dry-run`, read the diff, and run it without
the flag so `AGENTS.md` points at them.

Installing into the project from the open-standard registry:

```text
npx skills add narrativetrace/narrativetrace-dotnet
```

then run `dotnet tool run dotnet-narrativetrace init --dry-run`, read the diff, and run it without
the flag so `AGENTS.md` points at them.

A page a registry left behind is never refused just for being there. `init` compares it, byte for
byte but for line endings and the provenance line, against what it would have rendered itself. One
identical to this release's own page is **adopted** — the plan says so, rather than "replaced",
because a person reading it has to know that nothing of theirs was overwritten. This is the plan's
own text, quoted, never retyped here:

<!-- snippet: src/NarrativeTrace.Tooling/Init/PlanRenderer.cs region=adoptedNote -->
```csharp
private const string Adopted =
    "adopted: identical to this carrier's page, so only the provenance line is added";
```
<!-- /snippet -->

A page that differs — another release, or hand-edited — keeps the ordinary refusal `--force` is
for. `npx skills add` also leaves `.claude/skills/<name>` a symbolic link to the open-standard
page; `init` never writes through a link like that one. A link whose target it would adopt or
already owns is replaced with a real directory holding the right flavour; every other link is
refused, because `--force` covers content, never a link.

And this is the doctor's own fix, quoted the same way, for a project where the pages are there but
carry none of this:

<!-- snippet: src/NarrativeTrace.Tooling/Doctor/Checks/SkillsInstalledCheck.cs region=registryMessages -->
```csharp
private const string InitCommand = "dotnet tool run dotnet-narrativetrace init --dry-run";

/// <summary>What a skill directory that is present but not ours is called in a message.</summary>
private const string NotOurs = " (there, but not ours)";

/// <summary>
/// What a page with no provenance line most often IS: a registry install (D5 state 3) — <c>npx
/// skills add</c>, or a plugin or workspace install — of this repository's own rendered pages.
/// Naming the case matters because the obvious reading of "not ours" is "somebody else's work",
/// which invites a <c>--force</c> nobody needs: <c>init</c> ADOPTS a page identical to this
/// release's.
/// </summary>
private const string FromARegistry =
    " Pages that are there without our line usually came from a registry (npx skills add, a" +
    " plugin or workspace install). Run " + InitCommand +
    ", read the diff, then run it without the flag — a page identical to this release's is" +
    " adopted, and no --force is needed.";
```
<!-- /snippet -->

## `narrativetrace-doctor`

Read-only: runs `dotnet-narrativetrace doctor`, interprets the report, and
points at the fix. It never mutates anything. Beyond the CLI invocation, it
covers three things a stable check id can't fully replace:

1. Proving redaction actually holds, in a test — not just trusting the
   always-on deny-list.
2. Reading the newest rendered trace before writing any assertion against
   it — a judgmental step: there is nothing mechanical to verify here beyond
   "did the file get opened", so no `verify:` line accompanies it.
3. The approval-flow diff — reviewing a `*.received.nt` file against its
   `*.approved.nt` baseline. This step ships **flagged "unstudied — eval
   cell pending"**: it is evidenced only by field reports (see the
   `llms.txt` quotes), not by a trial run through the harness described
   below. That flag is load-bearing, not decoration — resolve it before
   treating the step as proven.

## `add-narrative-tracing`

Setup, end to end: install with the project's own frozen restore, wrap a
service and render its first trace, send that trace to a logger with one
call, then hand off to `narrativetrace-doctor` to confirm the install holds.
Right after the install, one step hands framework wiring to the doctor: run
it, apply every framework fix it prints (`config.registration-pair` and each
`config.<framework>-*`), in order, and leave alone a framework it reports as
having no integration shipped. The page names no framework on purpose — which
frameworks NarrativeTrace integrates with, and the compiled wiring lines for
each, ship inside the doctor, so the answer always matches the version the
project installed.
The first two code steps embed the real, tested
`examples/NarrativeTrace.Examples.SixtySeconds` project verbatim via this
repo's `<!-- snippet: PATH -->` convention — the same one
`documentation/sixty-seconds.md` and `llms.txt` use for the identical
file, so `SnippetCheck`'s drift coverage extends to `.claude/skills/**/SKILL.md`
and `.agents/skills/**/SKILL.md` too, never a second, hand-copied literal — and including its fixed demo
`traceparent` (`00-a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4-a1b2c3d4a1b2c3d4-01`),
which exists only so this page's output is reproducible; a real run adopts
an inbound header or generates its own. The marker comment (never rendered
by a Markdown viewer, and not part of what a reader runs) is the one place
a rendered page names this repo's own path; the bash commands beside it —
what an adopter actually runs — never do (agent-skills review, 2026-09-13).

## `narrativetrace-feedback`

Reports a defect in NarrativeTrace itself: a doctor check that is wrong or whose fix does not
work, a skill step that cannot be followed, wording in the install prompt that led somewhere
wrong, or the library misbehaving on a correctly configured project. The tested verb behind it —
`dotnet tool run dotnet-narrativetrace feedback draft|url|gh` — drafts the report from the
project (the install coordinates, the doctor's own JSON report from `doctor --report`, and at
most one structural trace), and **refuses to write a report that carries a value from your
traces**, naming the rule that refused it, so there is something specific to fix rather than a
warning to ignore. The skill then shows the whole draft and asks once whether to file it
publicly. It sends nothing anywhere and files nothing without an answer given in a turn of its
own: `url` prints the pre-filled issue form for `narrativetrace/narrativetrace-dotnet`, which you
open and submit under your own account, and `gh` prints the `gh issue create` line only when `gh`
is signed in.

`narrativetrace-feedback` is where a path ends when the problem turns out to be ours rather than
the project's — the doctor's own closing rule points at it.

A skill whose steps can make something public — today, this one — must declare no
`allowed-tools`. That field pre-approves its listed tools for the turn that loads the skill, and
a reporting skill that pre-approved its own reporting command would stop the harness asking
exactly where asking is the point. A lint keeps it that way: the rendered page carries no
`allowed-tools` line at all, and the build fails if one ever returns.

## `add-narrativetrace-clarity`

A first naming report, and the renames that clear it. The skill restores the local tool, builds the
project, runs `dotnet tool run dotnet-narrativetrace clarity-scan` over the built assembly (reflection
only, nothing is executed), reads both reports, explains the scores against the weights in the
[Clarity Guide](guides/clarity.md), then renames each flagged member by the report's suggestion,
updates every caller, and runs `clarity-check --max-high-issues 0` again until it exits `0`.

The gate is the CLI rather than the MSBuild targets on purpose: the `ClarityScan` and `ClarityCheck`
targets shell out to a bare `dotnet-narrativetrace`, which a local tool manifest never puts on `PATH`.
The targets are the skill's last step, taken only when a build gate is asked for. The skill never
deletes, comments out or exempts a flagged member to make the gate pass, never lowers a threshold the
project already set, and asks before renaming a published API. It pre-approves the same `dotnet` and
`git` commands as the setup skill, because nothing it runs is public or permanent.

## `narrativetrace-verify`

Reads what a change actually did before the agent says it is done. It runs after the tests are green
and before the report, and its first step is the cost rule: it says `tracing: <the reason>` when the
change crosses collaborators, branches, retries, runs async, carries state between calls or touches
code not written in this session — and `skipping narrativetrace-verify: <why>` for a pure function or
a one-class edit, which ends it there. Then: the intent, written down in three to six lines BEFORE the
first traced run (a trace read against nothing confirms whatever happened); the smallest real path,
run with a compiled listing that writes the artifacts; the value-free structural `.nt` read whole
against the intent, every match and mismatch named by its span id (`#2.1`); values opened on the one
span that looks wrong, and only then; fix and re-read; the pin; a two-sentence report that cites the
span ids it rests on.

The pin turns approval mode on with a `.runsettings` the test project references through
`<RunSettingsFilePath>` (the doctor's `config.approval-mode` finding checks it), runs the whole
suite — approval mode compares every test that writes its artifacts — shows every `.received.nt`
whole, asks once and stops the turn, and only on your next message promotes exactly what was shown
with `dotnet tool run dotnet-narrativetrace approve`. The baselines live in `narratives/` beside the
test project and are committed; the review copies are gitignored. The gate is the feedback skill's,
in the same words, and like it the skill declares no `allowed-tools`: a lint fails the build if a
skill that promotes a baseline ever pre-approves a tool.

## `narrativetrace-debug`

Starts from a symptom — a wrong amount, a wrong id, a call in the wrong order — rather than a change.
It reproduces the symptom with the smallest input and the same compiled listing verify uses, finds the
reported value in the run's Markdown narrative, reads the sequence diagram first when threads or
ordering are involved (each call carries its span id as a note), and names the FIRST span whose
inputs are right and whose output is wrong, by its id, before any code changes — by reading, not by
stepping through a debugger. When that span is large it narrows by span: it reads only the sub-tree
under the id and wraps one more collaborator with `NarrativeTraceProxy.Create` — never `[NotTraced]`,
which in this runtime redacts a value rather than scoping a trace. A trace that disagrees with the
code is handed to `narrativetrace-feedback` instead of worked around.

The fix goes into the diverging span, and the same input is re-run and the same span read again.
"Nothing else moved" is checked against the structure: a red run writes the narrative but keeps the
last green `.nt`, so the shape before the fix is the narrative's calls and ids without their values,
and a value fix moves no line of the value-free trace. The reproduction stays as the regression test,
and its shape is pinned with the same steps and the same gate as verify's pin. The report names the
root cause by span id, before the pin question and again after the promotion. It renders the same
"how to read a trace" sections as verify and declares no `allowed-tools`.

## Why a CLI verb, not just prose

Per the cross-runtime skill design's thin-LLM principle: push whatever can be
a tested program into one, and keep the model's job to invoking it and
interpreting the result in context. `narrativetrace-doctor`'s report is a
tested library subcommand (`NarrativeTrace.Cli`'s `doctor` verb — see the
[Installation Guide](guides/installation.md#validate-installation)), not
twelve paragraphs the model re-derives from scratch every time.

## Pro-tier mentions

The catalogue also carries two Pro-tier listings —
`narrativetrace-pro-aggregate` and `narrativetrace-mcp` — rendered into the
`AGENTS.md` managed section alongside the four free skills. Never a price,
never "paid": the mark is "Pro" plus a status (`shipped`, `in development`,
`planned`) that must agree verbatim with [`feature-guide.md`](feature-guide.md).

## Evaluation (Tier A / A2 / B)

- **Tier A** (lint, seconds, every commit): the `lint` verb, described above.
- **Tier A2** (deterministic replay, seconds, every commit): the `replay` verb
  — mechanically executes each step's real `commands` (and the handful of
  `verify:` claims that reduce to a mechanical check) against
  `examples/NarrativeTrace.Examples.SixtySeconds`, no LLM. Mirrors the
  canonical TypeScript source's `packages/skills/__tests__/replay.test.ts`,
  ported through a closed registry of safe, in-repo executors — see
  `SkillReplayRegistry` for what each catalogue command actually replays as
  and why. Rides `./build.sh Verify` beside `lint`.
- **Tier B** (real agent trials, sandboxed): case layout under
  `skills-evals/` — fixtures, trigger phrasings, and graders — driven by the
  standalone runner under `skills-evals/runner/` (`dotnet run --project
  skills-evals/runner -- eval …`), never wired into `./build.sh Verify`
  (Tier B drives subscription CLIs, by hand or from a scheduled job, never
  the metered API). Real trials have run on the Claude/Haiku lane; see
  `skills-evals/ledger/runs.jsonl` for every row and
  `skills-evals/ledger/promotion.md` for the current status per case.

## See also

- [Installation Guide](guides/installation.md) — the `doctor` verb, Option F.
- [Complete Reference](guides/llms.txt) — "Report a problem", the same three channels in one place.
- [What to Commit](what-to-commit.md) — why the rendered pages are committed.
- [Feature Guide](feature-guide.md) — the Pro-tier status table these
  listings must agree with.
