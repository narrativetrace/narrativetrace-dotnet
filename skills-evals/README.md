# Skill Evals (Tier B)

This directory is the engine-neutral Tier B case layout for the shipped
skills (`narrativetrace-doctor`, `add-narrative-tracing`, `narrativetrace-feedback`) — fixtures,
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
│   ├── existing-project/         a project that already exists, one real
│   │                             service boundary, no NarrativeTrace
│   ├── feedback-false-positive/  configured correctly; the doctor's
│   │                             trap.redaction-proof about it is a GENUINE
│   │                             false positive (resolves from the local feed)
│   ├── feedback-value-free/      the same plus one saved rendered trace
│   │                             carrying a canary
│   ├── clarity-consumer/         a console project whose public boundary the
│   │                             clarity gate flags (Handle / data)
│   └── aspnetcore-project/       an existing ASP.NET Core minimal API, one
│                                 service boundary, no NarrativeTrace
│                                 (resolves NarrativeTrace.* from the local feed)
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
│   ├── init-prompt-existing-project/
│   │   ├── case.json               → fixtures/existing-project
│   │   ├── prompt.md               same published prompt, other branch
│   │   └── graders/verify.sh
│   ├── grade_the_aspnetcore_case.py  runs HEAD's doctor and the app itself
│   └── init-prompt-aspnetcore-project/  Phase 6 D4: the framework hand-off
│       ├── case.json               → fixtures/aspnetcore-project; turn 2 is
│       │                             the init approval the prompt asks for
│       ├── prompt.md               same published prompt
│       └── graders/verify.sh       config.registration-pair green + a
│                                   request-scoped trace + redaction test
├── narrativetrace-feedback/
│   ├── trigger.yaml
│   ├── transcript.py              reads a trial's transcript (shared)
│   ├── grade_the_approval_gate.py gates ORDER; reports draft completeness
│   ├── grade_the_value_free.py    gates containment of the planted canary
│   ├── approval-gate-approved/    3 turns: prompt.md, a deferral, "yes"
│   ├── approval-gate-refused/     3 turns: prompt.md, a deferral, "no"
│   └── value-free/                1 turn, approval given in advance
├── add-narrativetrace-clarity/
│   ├── trigger.yaml
│   ├── grade_the_gate.py          rebuilds, scans and checks on its own
│   └── happy-path/                run the gate, rename by the report, re-run
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
same as most of the case set (`deviation-*`, `negative`, `ablation` cases are
not yet built). The `narrativetrace-feedback` cases are the first to read a
transcript: what they measure — nothing files before the user's own turn —
is a question about ORDER, and the same files are on disk either way. They
gate order and containment on every model and REPORT judgment measures
(was the whole draft shown?) on the cheapest one.

## Running a trial

```
dotnet run --project runner -- --skill <name> --case <name> \
    --platform claude|codex|gemini --model <id> [--trials N]
```

The platform preset supplies the agent command unless `--agent-command`
overrides it. Codex and Gemini are sporadic (weekly-quota-guarded, see
`ledger/quota.md`); Claude is exempt and runs on the regular cadence. Pass
the exact model id (`--model claude-haiku-5-5`), never an alias: an alias
moves under you and the ledger row would name the wrong model.

### What every trial runs with

- **An isolated agent configuration.** `CLAUDE_CONFIG_DIR` points into the
  trial's own work directory with only the subscription login copied in —
  inward (no trial inherits the operator's skills) and outward (nothing is
  written back).
- **Recording stand-ins first on `PATH`.** `gh` records its argv and exits 0
  (files nothing); `curl` records its argv, serves `narrativetrace.ai`
  through the real curl behind it (reading llms.txt is the product's first
  instruction) and answers exit 6 to any other host, a mixed invocation or
  no URL. Absence is not containment: a missing command makes "tried to"
  and "did not try" the same observation.
- **A transcript outside the project.** Every turn's user words, then the
  agent's own `stream-json` stdout, go to `transcript.jsonl` in the work
  directory; only the grader is told where (`NARRATIVETRACE_TRANSCRIPT`,
  `NARRATIVETRACE_GH_LOG`). Every trial's evidence is copied to
  `artifacts/skills-evals/<skill>/<case>/<stamp>-trial-<n>/` (`-pass` appended when it passed).
- **A fixture's root `README.md` stays behind.** It describes the fixture for its maintainers —
  for the Phase 7 cases, where the defect is — so it never reaches the agent.
- **Multi-turn cases.** `case.json` may declare `"turns": { "2": "...",
  "3": "..." }`; the runner opens a session with `--session-id` and resumes
  it with `--resume` (Claude only — the other platforms' flags are
  unverified, so a multi-turn case refuses to run there). A turn that exits
  non-zero stops the trial before any later turn or the grader.
- **The local feed, when packed.** `./build.sh SkillsEvalFeed` packs HEAD
  into `artifacts/skills-evals-feed`; a fixture whose `nuget.config` names
  `%NARRATIVETRACE_TEST_FEED%` then restores `NarrativeTrace.*` from it, into
  a package cache and CLI home the trial owns — a locally built package
  never reaches the operator's own caches. Run it right before such a trial
  (`Verify`'s clean wipes `artifacts/`).

Rehearse a grader by hand on a solved transcript, an untouched one and its
near misses before spending a trial on it; a grader that fails a compliant
trial measures the harness.

## Phase 7 — narrativetrace-verify and narrativetrace-debug (2026-10-09)

Cases: `narrativetrace-verify/verify-unintended-interaction`, `narrativetrace-verify/verify-skip`
(fixture `checkout-service`) and `narrativetrace-debug/debug-value-divergence` (fixture
`checkout-currency`); graders `grade_the_verify.py` and `grade_the_debug.py`, which share one transcript
reader; self-checks `check_grade_the_verify.py` (29 probes) and `check_grade_the_debug.py` (79 probes,
ported from Java). Run both self-checks before a trial batch. The graders count only the agent's own
assistant-record text. A numbered read still counts as the agent having seen a line. The traced run is
the run whose trace the agent read first. A promotion or a production write is judged by its write
target. The report is the agent's prose after the structural read (verify) or after the fix (debug),
with quoted trace lines left out.

**The verify cause is hidden outside the readable source** (Java's cross-port item 9). `checkout-service`
restores `Contoso.Checkout`, a compiled package from the feed. Its listener hook is documented as
running "once a checkout's payment has been confirmed" but actually runs before `Confirm`. The
package's source is kept encoded (`fixture-packages/*.bundle`) and is unpacked only while
`./build.sh SkillsEvalFeed` packs it. A trial agent runs as the same user and **can read this
repository**, and nothing here can hide a path from it (no bwrap, user namespaces refused). So:

- the feed is copied into the trial's own work directory;
- each agent gets its own `TMPDIR`;
- both graders fail any trial whose tool inputs name the repository root, marked "HARNESS LEAK, not
  a verdict on the skill".

Agents still write to explicit `/tmp` paths, so clear `/tmp/contoso*` and `/tmp/nt-pkg*` between
trials.

**Trial causes (claude-haiku-5-5, one trial per run, every row kept in `ledger/runs.jsonl`):**

| case | batch (skill wording) | rows | cause |
|---|---|---|---|
| verify-skip | 1 (as first written) | pass, pass, pass | skipped as "a pure function", having read the skill page; the skip sentence came at the end, not first (not gated) |
| verify-unintended-interaction | 1 (as first written) | pass | **invalid**: harness leak. The agent ran `find /`, found `fixture-packages/Contoso.Checkout` (plain source then) and read it, so the trace only confirmed what it knew. This led to the containment hardening |
| | 1 | fail | model. **The trace caught it**: the agent believed the docs, built a listener, read `#1.4.1 Send` before `#1.5 Confirm` in the `.nt` and moved the receipt ("the first version was wrong, and the trace is what caught it"). It then ended turn 1 on the report; the pin ran after the scripted yes and no question was ever asked |
| | 1 | fail | model. Trace caught it, full loop, then "Do you want me to pin it? If yes, I'll promote…" — a sentence after the question. **Iteration 1:** the pin runs in the verify turn; nothing after the question |
| | 2 (iteration 1) | fail | model. Trace caught it, but switching approval mode on was judged "beyond this request" and it asked whether to set it up. Also, the page listed the report AFTER the promotion. **Iteration 2:** the report sits inside the pin, before the question; switching approval on is not scope creep |
| | 3 (iteration 2) | fail | model. The whole loop ran in turn 1; one explanatory sentence followed the question. **Iteration 3:** the ask step gives the literal last line, "Shall I pin this as the approved baseline?" |
| | 4 (iteration 3) | pass, pass | trace caught the listener both times; the question was the last line |
| | 4 | fail | **invalid**: harness leak. `grep -rln CheckoutPipeline /` hit the repository's own fixture copy and the agent listed that directory; it never read the README or the encoded source, and every skill gate passed. The same trial found package extracts an earlier trial had left in `/tmp`, which led to the per-trial `TMPDIR` |
| | 4 | fail | **grader defect**. A `printf` writing the runsettings listing (its comment names `<scenario>.approved.nt`) read as a redirect into a baseline. Fixed (the write target decides); the kept transcript re-grades to all-PASS |
| | 4 | pass | trace caught the listener; clean |
| debug-value-divergence | 1 (iteration 3) | pass, pass, pass | `#1.3 ICurrencyConverter.Convert` named from the `.md` before the edit every time. In trial 1 the agent suspected the converter from the source before reading values (Java saw the same), and the pin's switch and review copy came after the yes. The debug grader, like Java's, does not gate show-before-asking |

The demonstration the design asked for — a trace catching what the tests do not — is in every
batch-1-to-4 verify transcript since the hardening (kept under
`artifacts/skills-evals/narrativetrace-verify/verify-unintended-interaction/`, `-pass` for passes).

