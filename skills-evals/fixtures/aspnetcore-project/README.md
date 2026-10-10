# aspnetcore-project fixture

The init prompt's "existing project" branch on the runtime's main web framework: an ASP.NET Core
minimal API that **already exists**, with one service boundary (`IOrderService.PlaceOrder`, registered
in DI and called from `GET /orders/{customerId}`) and no NarrativeTrace anywhere.
`add-narrative-tracing`'s `init-prompt-aspnetcore-project` case starts here (Phase 6 D4).

Nothing in the prompt or this project names ASP.NET Core's integration. The point of the case is that
the agent learns it from the installed doctor: `config.registration-pair` detects the Web SDK, reports
`NarrativeTrace.AspNetCore` missing, and prints the compiled wiring lines (`AddNarrativeTrace`,
`UseNarrativeTrace`, a service wrapped against the request's own context). The grader passes a trial
whose doctor reports that check green on a project still detected as ASP.NET Core, whose app — run and
requested by the grader itself — writes a trace naming the boundary in response to that request, and
whose redaction test passes.

`NarrativeTrace.*` restores from the local feed `./build.sh SkillsEvalFeed` packs from HEAD:
`nuget.config` names it as `%NARRATIVETRACE_TEST_FEED%`, which the eval runner sets together with a
package cache and a CLI home the trial owns — so the doctor the agent installs is the one with the
framework table. Everything else restores from nuget.org.

Not `include`d in `NarrativeTrace.sln`; a real trial scaffolds a fresh copy per run rather than
building this in place.
