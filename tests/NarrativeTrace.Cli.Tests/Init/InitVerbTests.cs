// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using NarrativeTrace.Cli.Init;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>
/// The <c>init</c> and <c>uninstall</c> verbs end to end, mirroring the Java <c>CliTest</c> installer
/// cases so the two lists diff. Each runs the real dispatch (<see cref="CliRunner"/>) over a directory
/// carrier named with <c>--from</c> and a temp project named with <c>--dir</c>; the two locator-seam
/// cases use the injectable overload directly.
/// </summary>
public sealed class InitVerbTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-verb-" + Guid.NewGuid().ToString("N"));
    private readonly string _carrier;
    private readonly string _project;
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public InitVerbTests()
    {
        Directory.CreateDirectory(_root);
        _carrier = TestCarriers.Write(Path.Combine(_root, "carrier"), "1.2.3");
        _project = Path.Combine(_root, "project");
        Directory.CreateDirectory(_project);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private int Init(params string[] extra) =>
        CliRunner.Run(["init", "--from", _carrier, "--dir", _project, .. extra], _out, _err);

    private int Uninstall(params string[] extra) =>
        CliRunner.Run(["uninstall", "--dir", _project, .. extra], _out, _err);

    private string Page(string flavour, string name) =>
        Path.Combine(_project, "." + flavour, "skills", name, "SKILL.md");

    private string AgentsMd => Path.Combine(_project, "AGENTS.md");

    [Fact]
    public void InitOnAFreshProjectWritesThePageAndTheSection()
    {
        var exit = Init();

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Page("agents", TestCarriers.Doctor)));
        Assert.Contains("narrativetrace:start", File.ReadAllText(AgentsMd), StringComparison.Ordinal);
    }

    [Fact]
    public void InitStampsEveryPageWithTheCarriersCoordinate()
    {
        Init();

        Assert.Contains(
            "installed by narrativetrace init from NarrativeTrace.Skills/1.2.3",
            File.ReadAllText(Page("agents", TestCarriers.Doctor)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void InitDryRunWritesNothingAndPrintsTheDiff()
    {
        var exit = Init("--dry-run");

        Assert.Equal(0, exit);
        Assert.False(File.Exists(AgentsMd));
        Assert.False(Directory.Exists(Path.Combine(_project, ".agents")));
        Assert.Contains("+++ b/AGENTS.md", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("+++ b/.agents/skills/narrativetrace-doctor/SKILL.md", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ASecondInitIsANoOp()
    {
        Init();
        var second = Init();

        Assert.Equal(0, second);
        Assert.Contains("0 applied", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InitRefusesAnExistingAgentsMdAndNamesTheFlag()
    {
        File.WriteAllText(AgentsMd, "# my own agents file\n");

        var exit = Init();

        Assert.Equal(1, exit);
        Assert.Contains("--write-existing", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InitWithWriteExistingAppendsTheSection()
    {
        File.WriteAllText(AgentsMd, "# my own agents file\n");

        var exit = Init("--write-existing");

        Assert.Equal(0, exit);
        Assert.Contains("narrativetrace:start", File.ReadAllText(AgentsMd), StringComparison.Ordinal);
        Assert.Contains("# my own agents file", File.ReadAllText(AgentsMd), StringComparison.Ordinal);
    }

    [Fact]
    public void InitOnlySkillsLeavesTheSectionAlone()
    {
        var exit = Init("--only", "skills");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Page("agents", TestCarriers.Doctor)));
        Assert.False(File.Exists(AgentsMd));
    }

    [Fact]
    public void InitOnlyAgentsMdLeavesTheSkillsAlone()
    {
        var exit = Init("--only", "agents-md");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(AgentsMd));
        Assert.False(Directory.Exists(Path.Combine(_project, ".agents")));
    }

    [Fact]
    public void InitVendorClaudeWritesTheVendorCopyIntoAProjectThatShowsNoSignOfIt()
    {
        var exit = Init("--vendor", "claude");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Page("claude", TestCarriers.Doctor)));
    }

    [Fact]
    public void InitVendorNoneSkipsTheVendorCopyEvenWhereItIsDetected()
    {
        Directory.CreateDirectory(Path.Combine(_project, ".claude"));

        var exit = Init("--vendor", "none");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Page("agents", TestCarriers.Doctor)));
        Assert.False(File.Exists(Page("claude", TestCarriers.Doctor)));
    }

    [Fact]
    public void InitForceOverwritesASkillDirectorySomebodyElseOwns()
    {
        var foreign = Page("agents", TestCarriers.Doctor);
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        File.WriteAllText(foreign, "---\nname: x\n---\nsomebody else's\n");

        Assert.Equal(1, Init());
        Assert.Equal(0, Init("--force"));
        Assert.Contains("installed by narrativetrace init", File.ReadAllText(foreign), StringComparison.Ordinal);
    }

    [Fact]
    public void InitJsonPrintsTheEnvelopeWithTheCarrierAndActions()
    {
        var exit = Init("--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"carrier\"", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"actions\"", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 0", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InitDryRunJsonReportsPlannedActionsAndExitsZeroDespiteARefusal()
    {
        File.WriteAllText(AgentsMd, "# my own agents file\n");

        var exit = Init("--dry-run", "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"status\": \"refused\"", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 0", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void InitHelpExits0AndNamesEveryFlag()
    {
        var exit = CliRunner.Run(["init", "--help"], _out, _err);

        Assert.Equal(0, exit);
        foreach (var flag in new[] { "--dry-run", "--write-existing", "--force", "--only", "--vendor", "--from", "--dir", "--json" })
        {
            Assert.Contains(flag, _out.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UninstallHelpExits0()
    {
        Assert.Equal(0, CliRunner.Run(["uninstall", "--help"], _out, _err));
        Assert.Contains("Removes exactly what init wrote", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownInstallerOptionExits2AndPrintsTheVerbsUsage()
    {
        var exit = CliRunner.Run(["init", "--nope"], _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("--nope", _err.ToString(), StringComparison.Ordinal);
        Assert.Contains("dotnet-narrativetrace init", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ABadOptionValueExits2()
    {
        var exit = CliRunner.Run(["init", "--only", "both"], _out, _err);

        Assert.Equal(2, exit);
        Assert.Contains("--only takes skills or agents-md", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ACarrierThatCannotBeOpenedExits1AndPrintsTheFetchSnippet()
    {
        var exit = CliRunner.Run(
            ["init", "--from", Path.Combine(_root, "nope"), "--dir", _project], _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("no carrier at", _err.ToString(), StringComparison.Ordinal);
        Assert.Contains("~/.nuget/packages", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void FromIsHandedToTheLocatorVerbatim()
    {
        string? seen = null;
        InitCommand.Run(
            true, ["--from", "whatever-i-typed", "--dry-run"], _project,
            from => { seen = from; return CarrierLocator.Open(_carrier); }, _out, _err);

        Assert.Equal("whatever-i-typed", seen);
    }

    [Fact]
    public void AProjectDirectoryThatIsNotOneExits1()
    {
        var exit = CliRunner.Run(
            ["init", "--from", _carrier, "--dir", Path.Combine(_root, "does-not-exist")], _out, _err);

        Assert.Equal(1, exit);
        Assert.Contains("not a directory", _err.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void UninstallNeverOpensACarrier()
    {
        var opened = false;
        var exit = InitCommand.Run(
            false, ["--dir", _project], _project,
            _ => { opened = true; return CarrierLocator.Open(_carrier); }, _out, _err);

        Assert.Equal(0, exit);
        Assert.False(opened);
    }

    [Fact]
    public void UninstallAfterInitLeavesTheTreeByteIdentical()
    {
        File.WriteAllText(Path.Combine(_project, "CLAUDE.md"), "# Claude\n");
        File.WriteAllText(Path.Combine(_project, "README.md"), "# Read me\n");
        var before = Files();

        Init("--write-existing");
        var exit = Uninstall();

        Assert.Equal(0, exit);
        Assert.Equal(before, Files());
    }

    /// <summary>The project's files as path → content, the same file-set snapshot Java's byte-identity test uses.</summary>
    private Dictionary<string, string> Files() =>
        Directory.EnumerateFiles(_project, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(_project, path).Replace('\\', '/'),
                File.ReadAllText,
                StringComparer.Ordinal);

    [Fact]
    public void UninstallDryRunRemovesNothing()
    {
        Init();
        var exit = Uninstall("--dry-run");

        Assert.Equal(0, exit);
        Assert.True(File.Exists(Page("agents", TestCarriers.Doctor)));
    }

    [Fact]
    public void UninstallOnAProjectThatNeverRanInitDoesNothing()
    {
        var exit = Uninstall();

        Assert.Equal(0, exit);
        Assert.Contains("0 applied", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshNeverCreatesOnAProjectThatNeverRanInit()
    {
        var exit = CliRunner.Run(["init", "--refresh", "--from", _carrier, "--dir", _project], _out, _err);

        Assert.Equal(0, exit);
        Assert.False(Directory.Exists(Path.Combine(_project, ".agents")));
        Assert.False(File.Exists(AgentsMd));
    }

    [Fact]
    public void RefreshRewritesAStaleInstall()
    {
        Init();
        var newer = TestCarriers.Write(Path.Combine(_root, "carrier-2"), "2.0.0");

        var exit = CliRunner.Run(["init", "--refresh", "--from", newer, "--dir", _project], _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains(
            "installed by narrativetrace init from NarrativeTrace.Skills/2.0.0",
            File.ReadAllText(Page("agents", TestCarriers.Doctor)),
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshWithDryRunWritesNothingEvenWhenStale()
    {
        Init();
        var newer = TestCarriers.Write(Path.Combine(_root, "carrier-3"), "3.0.0");

        var exit = CliRunner.Run(
            ["init", "--refresh", "--dry-run", "--from", newer, "--dir", _project], _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains(
            "installed by narrativetrace init from NarrativeTrace.Skills/1.2.3",
            File.ReadAllText(Page("agents", TestCarriers.Doctor)),
            StringComparison.Ordinal);
    }
}
