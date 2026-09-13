# Case: add-narrative-tracing / happy-path

**Fixture**: `fixtures/empty-project` — a bare console project, no
NarrativeTrace reference yet.

**Task prompt** (given to the agent verbatim):

> This project has one service behind an interface. Add NarrativeTrace so
> I can see a trace of what it does when I run it.

**Expected trajectory**: the agent installs with a frozen restore (never a
bare unlocked `dotnet add package`), wraps the service with
`NarrativeTraceProxy.Create<T>`, renders the captured tree to the console,
runs the project once to prove a trace appears, and finishes by running the
doctor CLI.

**Grading**:

- **Gates** (every model, `graders/verify.sh`):
  - `dotnet build` succeeds after the agent's changes.
  - Running the project prints a `trace: <name> (<id>)` line.
  - The doctor CLI's final run exits 0.
- **Report-only / gates at mid-model+**:
  - The service is wrapped once, at composition time — not re-wrapped on
    every call site.
