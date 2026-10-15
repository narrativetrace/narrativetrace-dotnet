// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using NarrativeTrace.Tooling.Platforms;
using Xunit;
using Carriers = NarrativeTrace.Tooling.Tests.Init.Carriers;

namespace NarrativeTrace.Tooling.Tests.Consumer;

/// <summary>
/// The platform pointers as an ENTRY POINT sees them: from another namespace, through the PUBLIC API
/// only (no <c>InternalsVisibleTo</c> member is named), into a real directory — a project that is on
/// Cursor and Replit at once, one that merely has a directory named like a marker, and one whose
/// pointer path is a link.
/// </summary>
public sealed class PlatformPointerFromAnotherPackageTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-pointer-consumer-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public PlatformPointerFromAnotherPackageTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "0.1.5");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string NewProject()
    {
        var project = Path.Combine(_root, "project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        return project;
    }

    private ExecutionReport Install(string project) => PlanExecutor.Execute(
        InitPlanner.Plan(ProjectStateReader.Read(project), _carrier, InitOptions.Defaults().WithWriteExisting(true)),
        project);

    private ExecutionReport Uninstall(string project) => PlanExecutor.Execute(
        UninstallPlanner.Plan(ProjectStateReader.Read(project), InitOptions.Defaults()), project);

    private static string RulePath(string project) => Path.Combine(project, ".cursor", "rules", "narrativetrace.mdc");

    [Fact]
    public void AProjectOnCursorAndReplitGetsBothPointersAndTheUninstallTakesBothAway()
    {
        var project = NewProject();
        Directory.CreateDirectory(Path.Combine(project, ".cursor"));
        File.WriteAllText(Path.Combine(project, "replit.md"), "# My Repl\n");

        var report = Install(project);

        Assert.Equal(0, report.ExitCode);
        var rule = (Pointer.Rule)PlatformTable.Row("cursor")!.Pointer;
        Assert.Equal(CursorRule.Render(rule.Text, "NarrativeTrace.Skills/0.1.5"), File.ReadAllText(RulePath(project)));
        Assert.Contains("narrativetrace:start NarrativeTrace.Skills/0.1.5", File.ReadAllText(Path.Combine(project, "replit.md")), StringComparison.Ordinal);

        Assert.Equal(0, Uninstall(project).ExitCode);
        Assert.False(File.Exists(RulePath(project)));
        Assert.Equal("# My Repl\n", File.ReadAllText(Path.Combine(project, "replit.md")));
    }

    [Theory]
    [InlineData(".cursorx")]
    [InlineData("cursor")]
    [InlineData(".replit-old")]
    public void ADirectoryNamedLikeAMarkerButNotOneWritesNoPointer(string name)
    {
        var project = NewProject();
        Directory.CreateDirectory(Path.Combine(project, name));

        Install(project);

        Assert.False(File.Exists(RulePath(project)));
        Assert.False(File.Exists(Path.Combine(project, "replit.md")));
        Assert.False(Directory.Exists(Path.Combine(project, ".cursor")));
    }

    [Fact]
    public void ARuleThePluginShippedIsAdoptedAndStampedWithoutForce()
    {
        var project = NewProject();
        var rule = (Pointer.Rule)PlatformTable.Row("cursor")!.Pointer;
        Directory.CreateDirectory(Path.GetDirectoryName(RulePath(project))!);
        File.WriteAllText(RulePath(project), CursorRule.Render(rule.Text, ""));

        var report = Install(project);

        Assert.Equal(0, report.ExitCode);
        Assert.Equal(CursorRule.Render(rule.Text, "NarrativeTrace.Skills/0.1.5"), File.ReadAllText(RulePath(project)));
    }

    [Fact]
    public void ACursorRulesDirectoryThatIsALinkIsRefusedAndTheTargetIsLeftEmpty()
    {
        var project = NewProject();
        var elsewhere = Path.Combine(_root, "elsewhere");
        Directory.CreateDirectory(elsewhere);
        Directory.CreateDirectory(Path.Combine(project, ".cursor"));
        Directory.CreateSymbolicLink(Path.Combine(project, ".cursor", "rules"), elsewhere);

        var report = Install(project);

        Assert.Equal(1, report.ExitCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
    }

    [Fact]
    public void ARenderedRuleWithABlankSentenceIsRefusedBeforeAnyFileExists()
    {
        Assert.Throws<ArgumentException>(() => CursorRule.Render(" ", ""));
        Assert.Throws<ArgumentException>(() => CursorRule.Render("text", null!));
    }
}
