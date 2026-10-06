# Skill Evals (Tier B) — Scaffolding Only

This directory is the engine-neutral Tier B case layout for the two shipped
skills (`narrativetrace-doctor`, `add-narrative-tracing`) — fixtures,
trigger phrasings, and graders for a real sandboxed-agent trial through
Claude Code, Codex, or Gemini, per the frozen cross-runtime skill-harness
design.

The case layout, the quota ledger, and the promotion matrix below exist so a
run happens on the same terms every other runtime uses. `runner/` drives the
first real trials (D13 milestone 0); see `ledger/promotion.md` for current
per-skill, per-platform status and `ledger/runs.jsonl` for every trial row
recorded so far.

## Layout

```
skills-evals/
├── fixtures/
│   ├── empty-project/            cold-install start: nothing installed yet
│   └── existing-project/         a project that already exists, one real
│                                 service boundary, no NarrativeTrace
├── narrativetrace-doctor/
│   ├── trigger.yaml               positive/negative trigger phrasings
│   └── happy-path/
│       ├── prompt.md               the task given to the agent
│       └── graders/verify.sh       world-state assertion, not text equality
├── add-narrative-tracing/
│   ├── trigger.yaml               (includes the published init prompt itself)
│   ├── happy-path/
│   │   ├── prompt.md
│   │   └── graders/verify.sh
│   ├── init-prompt-empty-project/  THE PUBLISHED PROMPT, byte for byte
│   │   ├── case.json               → fixtures/empty-project
│   │   ├── prompt.md               pinned identical by InitPromptDriftTest
│   │   └── graders/verify.sh
│   └── init-prompt-existing-project/
│       ├── case.json               → fixtures/existing-project
│       ├── prompt.md               same published prompt, other branch
│       └── graders/verify.sh
└── ledger/
    ├── quota.md                   weekly Codex/Gemini allowance + spend log
    ├── promotion.md               skill × platform approval matrix
    └── runs.jsonl                 one row per trial (append-only)
```

`runner/` (`NarrativeTrace.SkillsEvalRunner`) drives a case end to end: it
scaffolds the case's fixture into a temp directory, runs the platform's
agent command with no shell, runs the case's `graders/verify.sh`, and
appends the resulting row to `ledger/runs.jsonl`.

The init prompt is the ladder's first rung — the exact text published on the
website's `#start` tab, in `README.md`'s "Start here", and at the top of
`documentation/guides/llms.txt`. A prompt we publish is a prompt we replay:
its `prompt.md` copies here are held byte-identical to every published copy by
`InitPromptDriftTest` (in `tests/NarrativeTrace.Skills.Catalogue.Tests`).

## Grader kinds, in order of authority

1. **verifier** — the skill's own `verify:` claim plus the case's
   post-conditions (gate). `graders/verify.sh` in each case implements this.
2. **trigger** — did the skill fire on a mined phrasing (`trigger.yaml`).
3. **trajectory** — tool-use sanity (diagnostic only).
4. **judge** — an LLM rubric, annotation only, never a gate.

Only (1) and (2) are scaffolded here; (3) and (4) are noted as future work,
same as the case set itself (`deviation-*`, `negative`, `ablation` cases are
not yet built — happy-path only).

## Running a trial

```
dotnet run --project runner -- --skill <name> --case <name> \
    --platform claude|codex|gemini --model <id> [--trials N]
```

The platform preset supplies the agent command unless `--agent-command`
overrides it. Codex and Gemini are sporadic (weekly-quota-guarded, see
`ledger/quota.md`); Claude is exempt and runs on the regular cadence.
