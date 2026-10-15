// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Tooling.Init;
using NarrativeTrace.Tooling.Platforms;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The platform pointers against real files, applied for real: round trips that must come back byte
/// for byte, line endings and byte-order marks that must survive, and every path that must be refused
/// without a byte written. Method names mirror the Java <c>PlatformPointerAdversarialTest</c>.
/// </summary>
public sealed class PlatformPointerAdversarialTest : IDisposable
{
    private const string ReplitMd = "replit.md";
    private const string CursorRulePath = ".cursor/rules/narrativetrace.mdc";
    private const string Bom = "﻿";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-pointer-adv-" + Guid.NewGuid().ToString("N"));
    private readonly string _project;
    private readonly Carrier _carrier;

    public PlatformPointerAdversarialTest()
    {
        _project = Path.Combine(_root, "project");
        Directory.CreateDirectory(_project);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "1.2.3");
    }

    public void Dispose() => Projects.DeleteRecursively(_root);

    private string At(string relative) => Path.Combine(_project, relative.Replace('/', Path.DirectorySeparatorChar));

    private void Write(string relative, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(At(relative))!);
        File.WriteAllBytes(At(relative), new UTF8Encoding(false).GetBytes(content));
    }

    private string Read(string relative) => Encoding.UTF8.GetString(File.ReadAllBytes(At(relative)));

    private InitPlan PlanInstall(bool writeExisting = true) => InitPlanner.Plan(
        ProjectStateReader.Read(_project), _carrier, InitOptions.Defaults().WithWriteExisting(writeExisting));

    private InitPlan PlanUninstall() => UninstallPlanner.Plan(ProjectStateReader.Read(_project), InitOptions.Defaults());

    private ExecutionReport Apply(InitPlan plan) => PlanExecutor.Execute(plan, _project);

    private static string Crlf(string text) => text.Replace("\n", "\r\n", StringComparison.Ordinal);

    private static string CursorText() => ((Pointer.Rule)PlatformTable.Row("cursor")!.Pointer).Text;

    private string Section() => AgentsMdBlock.Render(_carrier, ProjectStateReader.Read(_project));

    // --- round trips: install then uninstall ----------------------------------------------------

    [Fact]
    public void InstallThenUninstallLeavesACrlfReplitMdByteIdentical()
    {
        Write(".replit", "run = \"x\"\n");
        Write(ReplitMd, "# Notes\r\n\r\nHello\r\n");
        var before = Projects.SnapshotOf(_project);

        Apply(PlanInstall());
        Apply(PlanUninstall());

        Assert.Equal(before, Projects.SnapshotOf(_project));
    }

    [Fact]
    public void InstallThenUninstallLeavesAByteOrderMarkedReplitMdByteIdentical()
    {
        Write(".replit", "run = \"x\"\n");
        Write(ReplitMd, Bom + "# Notes\n");
        var before = Projects.SnapshotOf(_project);

        Apply(PlanInstall());
        Apply(PlanUninstall());

        Assert.Equal(before, Projects.SnapshotOf(_project));
    }

    [Fact]
    public void AFileWithoutAFinalNewlineComesBackWithOneNewlineAndNothingElse()
    {
        Write(".replit", "x\n");
        Write(ReplitMd, "# Notes");

        Apply(PlanInstall());
        Apply(PlanUninstall());

        Assert.Equal("# Notes\n", Read(ReplitMd));
    }

    [Fact]
    public void InstallThenUninstallRestoresAWholeMixedProjectByteForByte()
    {
        Write(".replit", "x\n");
        Write("CLAUDE.md", "# Claude\n");
        Write(ReplitMd, Crlf("# Notes\n\nHello\n"));
        Directory.CreateDirectory(At(".cursor"));
        var before = Projects.SnapshotOf(_project);

        Apply(PlanInstall());
        Apply(PlanUninstall());

        Assert.Equal(before, Projects.SnapshotOf(_project));
    }

    [Fact]
    public void ASecondInstallOverAFullyInstalledMixedProjectPlansNothing()
    {
        Write(".replit", "x\n");
        Write("CLAUDE.md", "# Claude\n");
        Write(ReplitMd, Crlf("# Notes\n"));
        Directory.CreateDirectory(At(".cursor"));
        Apply(PlanInstall());

        Assert.Empty(PlanInstall().Actions);
    }

    [Fact]
    public void UninstallKeepsTheUsersNotesWrittenBelowACreatedReplitMd()
    {
        Write(".replit", "x\n");
        Apply(PlanInstall());
        File.AppendAllText(At(ReplitMd), "\nMy own notes.\n");

        Apply(PlanUninstall());

        Assert.Contains("My own notes.", Read(ReplitMd), StringComparison.Ordinal);
        Assert.DoesNotContain("narrativetrace:", Read(ReplitMd), StringComparison.Ordinal);
    }

    // --- the Cursor rule ------------------------------------------------------------------------

    [Fact]
    public void ACrlfCursorRuleThatIsAlreadyCurrentIsNotRewritten()
    {
        Directory.CreateDirectory(At(".cursor"));
        Apply(PlanInstall());
        Write(CursorRulePath, Crlf(Read(CursorRulePath)));

        Assert.DoesNotContain(PlanInstall().Actions, action => action.Path == CursorRulePath);
    }

    [Fact]
    public void UninstallDeletesACrlfCursorRuleTheInstallerMade()
    {
        Directory.CreateDirectory(At(".cursor"));
        Apply(PlanInstall());
        Write(CursorRulePath, Crlf(Read(CursorRulePath)));

        Apply(PlanUninstall());

        Assert.False(File.Exists(At(CursorRulePath)));
    }

    [Fact]
    public void UninstallKeepsTheUsersCrlfLinesBelowACursorRuleSection()
    {
        Directory.CreateDirectory(At(".cursor"));
        Apply(PlanInstall());
        Write(CursorRulePath, Crlf(Read(CursorRulePath) + "\nMy rule.\n"));

        Apply(PlanUninstall());

        Assert.Equal(Crlf("---\ndescription: Where the NarrativeTrace agent skills live in this project\nalwaysApply: true\n---\n\nMy rule.\n"), Read(CursorRulePath));
    }

    [Fact]
    public void ACursorRuleSavedWithAByteOrderMarkIsDeletedWhenTheInstallerMadeIt()
    {
        Directory.CreateDirectory(At(".cursor"));
        Apply(PlanInstall());
        Write(CursorRulePath, Bom + Read(CursorRulePath));

        Apply(PlanUninstall());

        Assert.False(File.Exists(At(CursorRulePath)));
    }

    [Fact]
    public void TwoSectionsInACursorRuleAreRefusedAndTheFileIsLeftAlone()
    {
        Directory.CreateDirectory(At(".cursor"));
        Apply(PlanInstall());
        var doubled = Read(CursorRulePath) + Read(CursorRulePath)[CursorRule.Frontmatter.Length..];
        Write(CursorRulePath, doubled);

        var install = Apply(PlanInstall());
        var uninstall = Apply(PlanUninstall());

        Assert.True(install.HasRefusals);
        Assert.True(uninstall.HasRefusals);
        Assert.Equal(doubled, Read(CursorRulePath));
    }

    // --- replit.md edits ------------------------------------------------------------------------

    [Fact]
    public void AStaleCrlfSectionInReplitMdIsRefreshedInCrlfAroundTheUsersText()
    {
        Write(".replit", "x\n");
        Write(ReplitMd, Crlf("# Notes\n\n" + Section() + "\nAfter.\n").Replace("1.2.3", "0.0.1", StringComparison.Ordinal));

        Apply(PlanInstall());

        var text = Read(ReplitMd);
        Assert.Contains("NarrativeTrace.Skills/1.2.3", text, StringComparison.Ordinal);
        Assert.StartsWith("# Notes\r\n\r\n", text, StringComparison.Ordinal);
        Assert.EndsWith("\r\nAfter.\r\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\r", text, StringComparison.Ordinal);
        Assert.DoesNotMatch("[^\r]\n", text);
    }

    [Fact]
    public void AByteOrderMarkBeforeTheSectionSurvivesARefreshOfThatSection()
    {
        Write(".replit", "x\n");
        Write(ReplitMd, Bom + Section().Replace("1.2.3", "0.0.1", StringComparison.Ordinal) + "\nNotes\n");

        Apply(PlanInstall());

        Assert.StartsWith(Bom + "<!-- narrativetrace:start NarrativeTrace.Skills/1.2.3", Read(ReplitMd), StringComparison.Ordinal);
        Assert.EndsWith("\nNotes\n", Read(ReplitMd), StringComparison.Ordinal);
    }

    [Fact]
    public void InstallRefusesAReplitMdWhoseSectionNeverClosesEvenWithWriteExisting()
    {
        Write(".replit", "x\n");
        const string unclosed = "# Notes\n<!-- narrativetrace:start x -->\nnever closed\n";
        Write(ReplitMd, unclosed);

        var report = Apply(PlanInstall());

        Assert.True(report.HasRefusals);
        Assert.Equal(unclosed, Read(ReplitMd));
    }

    [Fact]
    public void UninstallRefusesAReplitMdCarryingTwoSectionsAndLeavesItAlone()
    {
        Write(".replit", "x\n");
        var doubled = Section() + "\n" + Section();
        Write(ReplitMd, doubled);

        var report = Apply(PlanUninstall());

        Assert.True(report.HasRefusals);
        Assert.Equal(doubled, Read(ReplitMd));
    }

    // --- links and the wrong kind of thing at a path --------------------------------------------

    [Fact]
    public void AReplitMdThatIsALinkOutOfTheProjectIsRefusedAndTheTargetIsNotWrittenThrough()
    {
        Directory.CreateDirectory(Path.Combine(_root, "elsewhere"));
        File.WriteAllText(Path.Combine(_root, "elsewhere", "shared.md"), "shared\n");
        Write(".replit", "x\n");
        File.CreateSymbolicLink(At(ReplitMd), Path.Combine(_root, "elsewhere", "shared.md"));

        var report = Apply(PlanInstall());

        Assert.True(report.HasRefusals);
        Assert.Equal("shared\n", File.ReadAllText(Path.Combine(_root, "elsewhere", "shared.md")));
    }

    [Fact]
    public void ACursorDirectoryThatIsALinkIsRefusedAndNothingIsWrittenBehindIt()
    {
        Directory.CreateDirectory(Path.Combine(_root, "elsewhere"));
        Directory.CreateSymbolicLink(At(".cursor"), Path.Combine(_root, "elsewhere"));

        var report = Apply(PlanInstall());

        Assert.True(report.HasRefusals);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(_root, "elsewhere")));
    }

    [Fact]
    public void AReplitDirectoryMarksTheProjectExactlyLikeTheDotReplitFileDoes()
    {
        Directory.CreateDirectory(At(".replit"));

        Assert.Equal(["replit"], ProjectStateReader.Read(_project).DetectedPlatforms);
    }

    [Fact]
    public void AReplitMdThatIsADirectoryIsReportedRefusedByTheExecutorAndLeftIntact()
    {
        Directory.CreateDirectory(At(ReplitMd));

        var report = Apply(PlanInstall());

        Assert.True(report.HasRefusals);
        Assert.True(Directory.Exists(At(ReplitMd)));
    }

    [Fact]
    public void ACursorDirectoryThatIsAFileIsReportedRefusedByTheExecutorAndLeftIntact()
    {
        Write(".cursor", "not a directory\n");

        var report = Apply(PlanInstall());

        Assert.True(report.HasRefusals);
        Assert.Equal("not a directory\n", Read(".cursor"));
    }

    [Fact]
    public void ThePointerSentenceTheRuleCarriesIsTheTableTextByteForByte()
    {
        Directory.CreateDirectory(At(".cursor"));

        Apply(PlanInstall());

        Assert.Contains("\n" + CursorText() + "\n", Read(CursorRulePath), StringComparison.Ordinal);
    }
}
