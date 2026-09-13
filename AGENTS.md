

<!-- narrativetrace:skills:start -->
## NarrativeTrace agent skills

- `narrativetrace-doctor` — Diagnoses a NarrativeTrace .NET install without changing anything: runs the doctor CLI and explains every failing check and its fix, proves redaction with a real test, opens the newest rendered trace before trusting any assertion against it, and — flagged unstudied — walks an approval-trace diff. Use when traces aren't appearing, output looks wrong, a value that should be redacted shows up in a trace, tests pass but the trace looks off, or someone asks "why isn't NarrativeTrace working", "is my NarrativeTrace install broken", "run narrativetrace doctor", "check my NarrativeTrace setup", or "diagnose narrative trace".
- `add-narrative-tracing` — Adds NarrativeTrace to a .NET project end to end: installs the real toolchain with a frozen restore, wraps a service and renders its first trace, sends the trace to a logger with one call, and finishes by running the doctor CLI to confirm the install. Use when asked to "add narrative tracing", "set up NarrativeTrace", "wire up tracing for this service", "instrument this .NET app with NarrativeTrace", or "get a trace out of this code".
- `narrativetrace-pro-aggregate` (Pro, shipped) — flow summaries and dependency-graph diagrams over aggregated event streams
- `narrativetrace-mcp` (Pro, planned) — MCP tool handlers over rendered traces
See documentation/agent-skills.md for the full doc index.
<!-- narrativetrace:skills:end -->
