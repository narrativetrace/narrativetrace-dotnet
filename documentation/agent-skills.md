# Agent Skills

Two packaged agent playbooks ship with this repository for Claude Code and
Codex CLI: `narrativetrace-doctor` (read-only diagnosis) and
`add-narrative-tracing` (setup, end to end). Both are generated — never
hand-edited — from a typed catalogue in
[`NarrativeTrace.Skills.Catalogue`](../src/NarrativeTrace.Skills.Catalogue), the same way
`glossary.md` is generated from `glossary.json`.

## Where they live

- **Source of truth**: `src/NarrativeTrace.Skills.Catalogue/Catalogue/*.cs` — typed
  records (`Skill`, `SkillStep`, `ProListing`), not YAML or hand-written
  Markdown.
- **Rendered pages**: `.claude/skills/narrativetrace-doctor/SKILL.md` and
  `.claude/skills/add-narrative-tracing/SKILL.md` — the exact files Claude Code discovers. The
  same catalogue also renders Codex CLI's repository-level layout at
  `.agents/skills/narrativetrace-doctor/SKILL.md` and
  `.agents/skills/add-narrative-tracing/SKILL.md` — Codex scans
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
  `git` vocabulary, a description exceeds its budget, or a Pro-tier listing
  disagrees with [`feature-guide.md`](feature-guide.md).

## Installing them in your own project

The published `NarrativeTrace.Cli` tool carries the same two skills this repository renders for
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
  "carrier": "NarrativeTrace.Skills/0.2.0",
  "actions": [
    {"kind": "create", "path": ".agents/skills/narrativetrace-doctor/SKILL.md", "status": "planned"},
    {"kind": "create", "path": ".agents/skills/add-narrative-tracing/SKILL.md", "status": "planned"},
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
`AGENTS.md` managed section alongside the two free skills. Never a price,
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
- [What to Commit](what-to-commit.md) — why the rendered pages are committed.
- [Feature Guide](feature-guide.md) — the Pro-tier status table these
  listings must agree with.
