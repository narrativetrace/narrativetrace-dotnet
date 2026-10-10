// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Carriers = NarrativeTrace.Tooling.Tests.Init.Carriers;

namespace NarrativeTrace.Tooling.Tests.Consumer;

/// <summary>
/// The installer as an ENTRY POINT sees it: from another namespace, through the PUBLIC API only, into a
/// real directory. The only end-to-end cases — open a carrier, install into an empty project, install
/// again, uninstall, and check the tree at each step. Mirrors the Java
/// <c>consumer.InstallerFromAnotherPackageTest</c>; it uses a directory carrier (the real published
/// carrier is Phase 3 milestone 1, not yet landed) and this port's <c>NarrativeTrace.Skills/&lt;version&gt;</c>
/// coordinate.
/// </summary>
public sealed class InstallerFromAnotherPackageTest : IDisposable
{
    private const string Version = "0.1.5";
    private const string Coordinate = "NarrativeTrace.Skills/0.1.5";

    /// <summary>What `npx skills add` writes at the project root, and nothing of ours may touch.</summary>
    private const string LockFile = "{\"skills\": []}\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-consumer-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public InstallerFromAnotherPackageTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), Version);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string NewProject()
    {
        var project = Path.Combine(_root, "project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        return project;
    }

    private ExecutionReport Install(string project, InitOptions options) =>
        PlanExecutor.Execute(InitPlanner.Plan(ProjectStateReader.Read(project), _carrier, options), project);

    private static List<string> FilesUnder(string project) =>
        Directory.EnumerateFiles(project, "*", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(project, file).Replace(Path.DirectorySeparatorChar, '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    private static string ReadText(string file) =>
        System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(file));

    [Fact]
    public void InstallsTheCarriersSkillsAndTheManagedSectionIntoAnEmptyProject()
    {
        var project = NewProject();

        var report = Install(project, InitOptions.Defaults());

        Assert.Equal(0, report.ExitCode);
        Assert.Equal(Coordinate, report.Carrier);
        foreach (var skill in _carrier.Skills)
        {
            var page = Path.Combine(project, ".agents", "skills", skill.Name, "SKILL.md");
            Assert.True(File.Exists(page));
            var text = ReadText(page);
            Assert.Contains("installed by narrativetrace init from " + Coordinate, text, StringComparison.Ordinal);
            Assert.Contains(skill.Name, text, StringComparison.Ordinal);
        }

        var agents = ReadText(Path.Combine(project, "AGENTS.md"));
        Assert.StartsWith("<!-- narrativetrace:created -->", agents, StringComparison.Ordinal);
        Assert.Contains("<!-- narrativetrace:start " + Coordinate + " -->", agents, StringComparison.Ordinal);
        Assert.Contains("<!-- narrativetrace:end -->", agents, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallsTheVendorFlavourOnlyWhereItIsWanted()
    {
        var project = NewProject();
        Directory.CreateDirectory(Path.Combine(project, ".claude"));

        Install(project, InitOptions.Defaults());

        var files = FilesUnder(project);
        Assert.Contains(files, file => file.StartsWith(".claude/skills/", StringComparison.Ordinal));
        Assert.Contains(files, file => file.StartsWith(".agents/skills/", StringComparison.Ordinal));
    }

    [Fact]
    public void ASecondInstallOfTheSameCarrierHasNothingToDo()
    {
        var project = NewProject();
        Install(project, InitOptions.Defaults());

        var second = InitPlanner.Plan(ProjectStateReader.Read(project), _carrier, InitOptions.Defaults());

        Assert.True(second.IsEmpty);
        Assert.Contains("nothing to do", PlanRenderer.RenderText(second), StringComparison.Ordinal);
        Assert.Equal("", PlanRenderer.RenderDiff(second));
    }

    [Fact]
    public void ADryRunShowsTheDiffAndWritesNothing()
    {
        var project = NewProject();

        var plan = InitPlanner.Plan(
            ProjectStateReader.Read(project), _carrier, InitOptions.Defaults().WithDryRun(true));

        var diff = PlanRenderer.RenderDiff(plan);
        Assert.Contains("--- /dev/null", diff, StringComparison.Ordinal);
        Assert.Contains("+++ b/AGENTS.md", diff, StringComparison.Ordinal);
        Assert.Contains("+## NarrativeTrace", diff, StringComparison.Ordinal);
        Assert.Empty(FilesUnder(project));
        Assert.Equal(0, plan.ExitCode);
    }

    [Fact]
    public void UninstallingTakesBackEverythingTheInstallWrote()
    {
        var project = NewProject();
        Install(project, InitOptions.Defaults());

        var report = PlanExecutor.Execute(
            UninstallPlanner.Plan(ProjectStateReader.Read(project), InitOptions.Defaults()), project);

        Assert.Equal(0, report.ExitCode);
        Assert.Empty(FilesUnder(project));
    }

    [Fact]
    public void RefusesAnExistingContextFileAndSaysWhatToPass()
    {
        var project = NewProject();
        File.WriteAllText(Path.Combine(project, "AGENTS.md"), "# My own notes\n");

        var report = Install(project, InitOptions.Defaults());

        Assert.Equal(1, report.ExitCode);
        Assert.Contains("--write-existing", PlanRenderer.RenderText(report), StringComparison.Ordinal);
        Assert.Equal("# My own notes\n", ReadText(Path.Combine(project, "AGENTS.md")));
        Assert.Contains(FilesUnder(project), file => file.StartsWith(".agents/skills/", StringComparison.Ordinal));
    }

    [Fact]
    public void TheJsonEnvelopeNamesTheCarrierEveryActionAndTheExitCode()
    {
        var project = NewProject();
        var report = Install(project, InitOptions.Defaults());

        var json = PlanRenderer.RenderJson(report);

        Assert.StartsWith("{\n  \"carrier\": \"" + Coordinate + "\"", json, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"create\", \"path\": \"AGENTS.md\", \"status\": \"applied\"", json, StringComparison.Ordinal);
        Assert.EndsWith("\"exitCode\": 0\n}\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void InstallsOnlyTheHalfItIsAskedFor()
    {
        var project = NewProject();
        Install(project, InitOptions.Defaults().WithScope(Scope.Skills));

        var files = FilesUnder(project);
        Assert.NotEmpty(files);
        Assert.DoesNotContain("AGENTS.md", files);
    }

    /// <summary>
    /// The registry case, end to end: a project that got these very pages from a registry — the
    /// repository's own rendered files, no provenance line — is adopted rather than refused, with no
    /// flag at all, and the page it already had is left in place with one line added.
    /// </summary>
    [Fact]
    public void AdoptsThePagesARegistryInstalledWithoutAnyFlag()
    {
        var project = NewProject();
        var first = _carrier.Skills[0];
        var page = Path.Combine(project, ".agents", "skills", first.Name, "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, _carrier.Body(first, SkillFlavour.Agents));

        var report = Install(project, InitOptions.Defaults());

        Assert.Equal(0, report.ExitCode);
        var text = PlanRenderer.RenderText(report);
        Assert.Contains("adopt", text, StringComparison.Ordinal);
        Assert.Contains("adopted:", text, StringComparison.Ordinal);
        var stamped = ReadText(page);
        Assert.Contains("installed by narrativetrace init from " + Coordinate, stamped, StringComparison.Ordinal);
        Assert.Equal(
            Provenance.Stamp(_carrier.Body(first, SkillFlavour.Agents), Coordinate),
            stamped);
    }

    /// <summary>
    /// The whole registry tree, end to end: what `npx skills add` really leaves — the open-standard
    /// pages, a LINK from each vendor path to them, and a lock file of its own. `init` adopts the
    /// pages and replaces the links; it never writes the vendor flavour through one, which would have
    /// overwritten the page it had just adopted.
    /// </summary>
    [Fact]
    public void InstallsIntoATreeARegistryLeftBehindWithoutWritingThroughItsLinks()
    {
        var project = NewProject();
        SimulateRegistryInstall(project);

        var report = Install(project, InitOptions.Defaults());

        Assert.Equal(0, report.ExitCode);
        foreach (var skill in _carrier.Skills)
        {
            var agents = Path.Combine(project, ".agents", "skills", skill.Name, "SKILL.md");
            var vendor = Path.Combine(project, ".claude", "skills", skill.Name);
            Assert.Equal(Stamped(_carrier.Body(skill, SkillFlavour.Agents)), ReadText(agents));
            Assert.Contains(Coordinate, ReadText(agents), StringComparison.Ordinal);
            Assert.False(new FileInfo(vendor).LinkTarget is not null);
            Assert.Equal(Stamped(_carrier.Body(skill, SkillFlavour.Claude)), ReadText(Path.Combine(vendor, "SKILL.md")));
        }

        Assert.Equal(LockFile, ReadText(Path.Combine(project, "skills-lock.json")));
    }

    /// <summary>Uninstalling that tree takes back the pages, now ours, and leaves the registry's own file.</summary>
    [Fact]
    public void UninstallingAfterAdoptingARegistryTreeLeavesTheRegistrysOwnFiles()
    {
        var project = NewProject();
        SimulateRegistryInstall(project);
        Install(project, InitOptions.Defaults());

        var report = PlanExecutor.Execute(
            UninstallPlanner.Plan(ProjectStateReader.Read(project), InitOptions.Defaults()), project);

        Assert.Equal(0, report.ExitCode);
        Assert.Equal(["skills-lock.json"], FilesUnder(project));
    }

    /// <summary>`npx skills add`: the agents pages, a link per vendor path, and a lock file at the root.</summary>
    private void SimulateRegistryInstall(string project)
    {
        Directory.CreateDirectory(Path.Combine(project, ".claude", "skills"));
        foreach (var skill in _carrier.Skills)
        {
            var directory = Path.Combine(project, ".agents", "skills", skill.Name);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "SKILL.md"), _carrier.Body(skill, SkillFlavour.Agents));
            Directory.CreateSymbolicLink(
                Path.Combine(project, ".claude", "skills", skill.Name),
                Path.Combine("..", "..", ".agents", "skills", skill.Name));
        }

        File.WriteAllText(Path.Combine(project, "skills-lock.json"), LockFile);
    }

    private static string Stamped(string rendered) => Provenance.Stamp(rendered, Coordinate);

    [Fact]
    public void ReadsBackWhatItWroteAsAProjectState()
    {
        var project = NewProject();
        Install(project, InitOptions.Defaults());

        var state = ProjectStateReader.Read(project);

        Assert.NotNull(state.AgentsMd);
        Assert.NotEmpty(state.InstalledSkills);
        Assert.All(state.InstalledSkills, skill => Assert.Equal(Coordinate, skill.Coordinate));
    }
}
