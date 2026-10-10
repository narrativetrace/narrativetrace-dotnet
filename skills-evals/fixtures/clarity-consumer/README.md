# clarity-consumer fixture

A small console project whose one service boundary is named the way a first draft is: the method is
the generic verb `Handle` and its parameter is `data`. The clarity gate flags both on each of the two
public types (the interface and its implementation) as HIGH issues, so
`dotnet-narrativetrace clarity-check --max-high-issues 0` exits 1 on the project as it stands, and
exits 0 once the names say what the member does in this project's own words.

The flagged names are not hidden from the agent and there is no seeded lie: the task is to run the
gate, read the report, rename, and update every caller. `Program.cs` calls the boundary and prints the
result, so a rename that leaves a caller behind does not build and a deletion changes the output.

The CLI is a local tool (`.config/dotnet-tools.json`) resolved from the local feed `./build.sh
SkillsEvalFeed` packs from HEAD — `nuget.config` names it as `%NARRATIVETRACE_TEST_FEED%`, which the
eval runner sets together with a package cache and a CLI home the trial owns. The pinned version is
the current `VersionPrefix`, held there by `ClarityFixtureTests`. The project itself references no
NarrativeTrace package: the scan reads the built assembly by reflection.

Not `include`d in `NarrativeTrace.sln`; a real trial scaffolds a fresh copy per run rather than
building this in place.
