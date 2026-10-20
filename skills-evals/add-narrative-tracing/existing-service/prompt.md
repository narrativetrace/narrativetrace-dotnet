# Case: add-narrative-tracing / existing-service

**Fixture**: `fixtures/existing-project-xunit` — a project that already exists WITH a test suite (one
xUnit test of its one service) and no NarrativeTrace anywhere.

**Task prompt** (given to the agent verbatim):

> This service already has a test suite. Add NarrativeTrace and show me the trace of one of my
> existing tests — I want to find the file afterwards, not just see it scroll by.

**Expected trajectory**: the project has tests, so the first trace is one existing test run through the
xUnit integration (`NarrativeTrace.Testing.Xunit`) with output on — no demo program, and no second test
runner. The agent prints the rendered file as well, so the trace is on the console too, and finishes by
running the doctor.

**Grading**:

- **Gates** (every model, `graders/verify.sh`, world state only):
  - `dotnet test`, run by the grader itself, exits 0 and the fixture's own test still passes.
  - At least one `.md` and one `.nt` exist under the integration's output directory
    (`TestResults/narrativetrace/traces/` and `TestResults/narrativetrace/structural/`) after that run.
  - The doctor's `toolchain.*` findings hold.
