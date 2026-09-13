# empty-project fixture

A cold-install fixture: a bare console project with no NarrativeTrace
package reference yet. `add-narrative-tracing`'s happy-path case starts
here; `narrativetrace-doctor`'s case starts from a project one step further
along (a proxy already wired up, per its own prompt).

Not runnable directly — a real trial scaffolds a fresh `dotnet new console`
project rather than committing generated build output here.
