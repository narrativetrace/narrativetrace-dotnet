# Case: add-narrative-tracing / happy-path

**Fixture**: `fixtures/empty-project` — a bare console project, no
NarrativeTrace reference yet.

**Task prompt** (given to the agent verbatim):

> This is a brand new .NET console project. I want to add NarrativeTrace and
> see a trace from a real method call within the next few minutes.

**Expected trajectory**: the agent installs with a frozen restore (never a
bare unlocked `dotnet add package`), wraps the service with
`NarrativeTraceProxy.Create<T>`, renders the captured tree to the console,
runs the project once to prove a trace appears, and finishes by running the
doctor CLI.

**Grading** (the no-tests premise — the page never adds a test runner or a test to a project that has
none, so nothing is written to disk and nothing about files is asserted):

- **Gates** (every model, `graders/verify.sh`, world state — the grader runs the program itself):
  - `dotnet build` succeeds after the agent's changes, in the project that declares NarrativeTrace.
  - `dotnet run`, run by the grader, exits 0 and its output carries a `trace: <name> (<id>)` line and
    a traced call line (`<Type>.<Method>(...) → <result>`).
  - The doctor's `toolchain.*` findings hold.
- **Report-only / gates at mid-model+**:
  - The service is wrapped once, at composition time — not re-wrapped on
    every call site.
