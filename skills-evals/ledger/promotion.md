# Promotion Matrix

A cell turns `approved` once that platform has one green `trigger` case and
one green `happy-path` case for the skill, at its current wording and
fixture. A wording or fixture change clears that skill's row.

| Skill | Claude | Codex | Antigravity | Cursor |
|---|---|---|---|---|
| narrativetrace-doctor | not yet run | not yet run | not yet run | not yet run |
| add-narrative-tracing | green (claude-haiku-5-5, 2026-10-10) | not yet run | not yet run | not yet run |
| narrativetrace-feedback | green (claude-haiku-5-5, 2026-10-08) | not yet run | not yet run | not yet run |
| add-narrativetrace-clarity | green (claude-haiku-5-5, 2026-10-09) | not yet run | not yet run | not yet run |
| narrativetrace-verify | green (claude-haiku-5-5, 2026-10-09) | not yet run | not yet run | not yet run |
| narrativetrace-debug | green (claude-haiku-5-5, 2026-10-09) | not yet run | not yet run | not yet run |
| narrativetrace-explain | green (claude-haiku-5-5, 2026-10-15) | not yet run | not yet run | not yet run |

Every skill is expected to eventually be approved by every platform; an
empty cell does not block the nightly build, but it blocks the skill's
promotion to fully-approved status in the catalogue.
