# Tier B Quota Ledger

Budget = subscription, not API spend: Tier B runs through each platform's
CLI on its subscription plan, never the metered API. The Claude lane is
exempt (no weekly cap — it runs on the regular cadence); Codex, Antigravity and
Cursor accrue against the allowance below and run opportunistically when
quota permits. A lane whose allowance is 0 (its CLI is not installed or
signed in yet) skips with one line naming the tool and exits green.

| platform | plan tier | weekly allowance |
|---|---|---|
| codex | basic | 4 |
| antigravity | not installed | 0 |
| cursor | not installed | 0 |

No override flag — edit this table directly when a plan tier changes.

## Spend log

No runs yet.

| date | platform | skill | case | week |
|---|---|---|---|---|
