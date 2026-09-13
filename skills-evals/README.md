# Skill Evals (Tier B) — Scaffolding Only

This directory is the engine-neutral Tier B case layout for the two shipped
skills (`narrativetrace-doctor`, `add-narrative-tracing`) — fixtures,
trigger phrasings, and graders for a real sandboxed-agent trial through
Claude Code, Codex, or Gemini, per the frozen cross-runtime skill-harness
design.

**No real trial has been run yet.** This is scaffolding only, matching the
design's explicit instruction not to fabricate one — the case layout, the
quota ledger, and the promotion matrix below exist so a run can happen on
the same terms every other runtime uses, not so one can be claimed.

## Layout

```
skills-evals/
├── fixtures/
│   └── empty-project/            pinned tiny project cases start from
├── narrativetrace-doctor/
│   ├── trigger.yaml               positive/negative trigger phrasings
│   └── happy-path/
│       ├── prompt.md               the task given to the agent
│       └── graders/verify.sh       world-state assertion, not text equality
├── add-narrative-tracing/
│   ├── trigger.yaml
│   └── happy-path/
│       ├── prompt.md
│       └── graders/verify.sh
└── ledger/
    ├── quota.md                   weekly Codex/Gemini allowance + spend log
    └── promotion.md               skill × platform approval matrix
```

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

Not implemented for this port yet. `evals/run.ts`'s platform-preset shape
(`--platform claude|codex|gemini`) is the reference to follow when it is.
