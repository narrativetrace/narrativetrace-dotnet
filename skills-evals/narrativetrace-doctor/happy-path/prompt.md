# Case: narrativetrace-doctor / happy-path

**Fixture**: a project with `NarrativeTrace.Proxy` wired up over one
interface, `NARRATIVETRACE_OUTPUT` unset, and no test yet asserting
`[REDACTED]`.

**Task prompt** (given to the agent verbatim):

> Something feels off with the NarrativeTrace setup in this project — can
> you check it and fix whatever the checks find?

**Expected trajectory**: the agent runs the doctor CLI first (rather than
hand-inspecting config), reads the JSON report, resolves the
`trap.redaction-proof` finding by writing a real test, and reports back
which findings passed after re-running doctor.

**Grading**:

- **Gates** (every model, `graders/verify.sh`):
  - Doctor CLI was actually invoked (world-state: a doctor run artifact or
    process trace exists) — never just prose describing what it would say.
  - `trap.redaction-proof` reports `"passed": true` on the final run.
- **Report-only / gates at mid-model+**:
  - The test the agent wrote asserts a neighboring, non-sensitive value is
    still present (catches over-broad redaction, not just under-redaction).
