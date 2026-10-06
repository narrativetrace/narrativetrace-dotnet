# existing-project fixture

The init prompt's other branch: a project that **already exists**, with one
real service boundary and no NarrativeTrace anywhere. `add-narrative-tracing`'s
`init-prompt-existing-project` case starts here — the agent is told to work
inside the existing project and trace that one boundary, not to scaffold a
fresh console app.

Not `include`d in `NarrativeTrace.sln`; a real trial scaffolds a fresh copy
per run rather than building this in place.
