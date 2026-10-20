# existing-project-xunit

`add-narrative-tracing`'s `existing-service` fixture (B-92): a project that **already exists** WITH a
test suite — one real service boundary (`OrderService`), one xUnit test of it — and no NarrativeTrace
anywhere. The first trace of such a project is one of its own tests run through the xUnit
integration, which leaves the `.md` and `.nt` files; the page never adds a test runner here, it already
has one. `existing-project` is the same service without tests (the no-tests premise).

Not `include`d in `NarrativeTrace.sln`; a real trial scaffolds a fresh copy per run. `nuget.config`
resolves `NarrativeTrace.*` from the local eval feed.
