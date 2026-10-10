// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;
using Status = NarrativeTrace.Tooling.Init.ExecutionReport.Status;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The only class in the installer that writes. Everything here happens on a real temp directory.
/// Method names mirror the Java <c>PlanExecutorTest</c>.
/// </summary>
public sealed class PlanExecutorTest : IDisposable
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nt-exec-" + Guid.NewGuid().ToString("N"));

    public PlanExecutorTest() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static InitPlan Plan(params Action[] actions) => new(Coordinate, false, actions);

    private string Resolve(string relative) => Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));

    private static string Read(string file) => Encoding.UTF8.GetString(File.ReadAllBytes(file));

    [Fact]
    public void CreatesAFileAndEveryDirectoryAboveIt()
    {
        var report = PlanExecutor.Execute(
            Plan(new CreateFile(".agents/skills/doctor/SKILL.md", "page\n")), _dir);

        Assert.Equal("page\n", Read(Resolve(".agents/skills/doctor/SKILL.md")));
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(Status.Applied, Assert.Single(report.Results).ActionStatus);
    }

    [Fact]
    public void WritesExactlyTheBytesThePlanCarries()
    {
        var windows = "﻿# Agents\r\n\r\nblock\r\n";

        PlanExecutor.Execute(Plan(new CreateFile("AGENTS.md", windows)), _dir);

        Assert.Equal(windows, Read(Resolve("AGENTS.md")));
    }

    [Fact]
    public void ReplacesAnExistingFile()
    {
        File.WriteAllText(Resolve("AGENTS.md"), "old\n");

        PlanExecutor.Execute(Plan(new ReplaceBlock("AGENTS.md", "old\n", "new\n")), _dir);

        Assert.Equal("new\n", Read(Resolve("AGENTS.md")));
    }

    [Fact]
    public void AppliesWhatTheActionComputedNotWhatIsOnDisk()
    {
        File.WriteAllText(Resolve("AGENTS.md"), "# Agents\n");

        PlanExecutor.Execute(Plan(new AppendBlock("AGENTS.md", "# Agents\n", "block\n")), _dir);

        Assert.Equal("# Agents\n\nblock\n", Read(Resolve("AGENTS.md")));
    }

    [Fact]
    public void DeletesAFileAndThenItsDirectory()
    {
        var page = Resolve(".agents/skills/doctor/SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(page)!);
        File.WriteAllText(page, "page\n");

        var report = PlanExecutor.Execute(
            Plan(
                new DeleteFile(".agents/skills/doctor/SKILL.md", "page\n"),
                new DeleteDirectory(".agents/skills/doctor")),
            _dir);

        Assert.False(File.Exists(page));
        Assert.False(Directory.Exists(Path.GetDirectoryName(page)!));
        Assert.Equal(0, report.ExitCode);
    }

    [Fact]
    public void DeletingSomethingAlreadyGoneIsStillApplied()
    {
        var report = PlanExecutor.Execute(
            Plan(
                new DeleteFile("AGENTS.md", "gone\n"),
                new DeleteDirectory(".agents/skills/doctor")),
            _dir);

        Assert.Equal(0, report.ExitCode);
        Assert.All(report.Results, result => Assert.Equal(Status.Applied, result.ActionStatus));
    }

    [Fact]
    public void LeavesADirectoryThatStillHoldsSomebodyElsesFileAndSaysSo()
    {
        var directory = Resolve(".agents/skills/doctor");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "NOTES.md"), "mine\n");

        var report = PlanExecutor.Execute(Plan(new DeleteDirectory(".agents/skills/doctor")), _dir);

        Assert.True(Directory.Exists(directory));
        Assert.True(File.Exists(Path.Combine(directory, "NOTES.md")));
        Assert.Equal(1, report.ExitCode);
        Assert.Contains("not empty", report.Results[0].Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsARefusalAndWritesNothingForIt()
    {
        var report = PlanExecutor.Execute(Plan(new Refuse("AGENTS.md", "two sections")), _dir);

        Assert.False(File.Exists(Resolve("AGENTS.md")));
        Assert.Equal(Status.Refused, report.Results[0].ActionStatus);
        Assert.Equal("two sections", report.Results[0].Detail);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void AFailedWriteLeavesWhatWasThereIntactAndTheRestOfThePlanRuns()
    {
        Directory.CreateDirectory(Resolve("AGENTS.md"));
        File.WriteAllText(Resolve("AGENTS.md/inside.txt"), "untouched\n");

        var report = PlanExecutor.Execute(
            Plan(
                new ReplaceBlock("AGENTS.md", "old\n", "new\n"),
                new CreateFile("NOTES.md", "written\n")),
            _dir);

        Assert.Equal("untouched\n", Read(Resolve("AGENTS.md/inside.txt")));
        Assert.Equal("written\n", Read(Resolve("NOTES.md")));
        Assert.Equal(Status.Refused, report.Results[0].ActionStatus);
        Assert.Equal(Status.Applied, report.Results[1].ActionStatus);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void LeavesNoTemporaryFileBehind()
    {
        PlanExecutor.Execute(
            Plan(new CreateFile("AGENTS.md", "a\n"), new CreateFile("CLAUDE.md", "b\n")), _dir);

        var names = Directory.EnumerateFileSystemEntries(_dir).Select(Path.GetFileName).ToList();
        Assert.Equal(["AGENTS.md", "CLAUDE.md"], names.OrderBy(name => name, StringComparer.Ordinal));
    }

    [Fact]
    public void ReportsTheCarrierAndEveryActionInPlanOrder()
    {
        var report = PlanExecutor.Execute(
            Plan(new CreateFile("AGENTS.md", "a\n"), new Refuse("CLAUDE.md", "no flag")), _dir);

        Assert.Equal(Coordinate, report.Carrier);
        Assert.Equal(["AGENTS.md", "CLAUDE.md"], report.Results.Select(applied => applied.Action.Path));
        Assert.True(report.HasRefusals);
    }

    // --- links ----------------------------------------------------------------------------------

    private static bool IsSymlink(string path) => new FileInfo(path).LinkTarget is not null;

    /// <summary>
    /// Replacing a link is the one write that starts by deleting something: the link goes, a real
    /// directory takes its place, and what the link pointed at is left exactly as it was — which is
    /// the whole point, because after `npx skills add` it points at the page this install just adopted.
    /// </summary>
    [Fact]
    public void ReplacesALinkWithARealDirectoryAndLeavesItsTargetAlone()
    {
        var reached = Resolve(".agents/skills/doctor/SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(reached)!);
        File.WriteAllText(reached, "the agents page\n");
        Directory.CreateDirectory(Resolve(".claude/skills"));
        Directory.CreateSymbolicLink(
            Resolve(".claude/skills/doctor"), Path.Combine("..", "..", ".agents", "skills", "doctor"));

        var report = PlanExecutor.Execute(
            Plan(new ReplaceLink(
                ".claude/skills/doctor", ".claude/skills/doctor/SKILL.md",
                "../../.agents/skills/doctor", "the vendor page\n")),
            _dir);

        Assert.Equal(0, report.ExitCode);
        Assert.False(IsSymlink(Resolve(".claude/skills/doctor")));
        Assert.Equal("the vendor page\n", Read(Resolve(".claude/skills/doctor/SKILL.md")));
        Assert.Equal("the agents page\n", Read(reached));
    }

    [Fact]
    public void ReplacesALinkedPageWithARealFile()
    {
        Directory.CreateDirectory(Resolve(".agents/skills/doctor"));
        File.WriteAllText(Resolve("elsewhere.md"), "somebody's page\n");
        const string page = ".agents/skills/doctor/SKILL.md";
        File.CreateSymbolicLink(Resolve(page), Path.Combine("..", "..", "..", "elsewhere.md"));

        PlanExecutor.Execute(Plan(new ReplaceLink(page, page, "../../../elsewhere.md", "ours\n")), _dir);

        Assert.False(IsSymlink(Resolve(page)));
        Assert.Equal("ours\n", Read(Resolve(page)));
        Assert.Equal("somebody's page\n", Read(Resolve("elsewhere.md")));
    }

    /// <summary>
    /// The guarantee for a link no planner saw — one created between the read and the write, or one
    /// further up the path than the planners look. The write is refused, not followed, and the rest of
    /// the plan still runs.
    /// </summary>
    [Fact]
    public void RefusesToWriteThroughALinkNoPlanKnewAbout()
    {
        var outside = Path.Combine(Path.GetTempPath(), "nt-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            Directory.CreateDirectory(Resolve(".claude"));
            Directory.CreateSymbolicLink(Resolve(".claude/skills"), outside);

            var report = PlanExecutor.Execute(
                Plan(
                    new CreateFile(".claude/skills/doctor/SKILL.md", "page\n"),
                    new CreateFile("AGENTS.md", "section\n")),
                _dir);

            Assert.Equal(1, report.ExitCode);
            Assert.Contains("symbolic link", report.Results[0].Detail, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(outside, "doctor")));
            Assert.Equal("section\n", Read(Resolve("AGENTS.md")));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    /// <summary>An uninstall never deletes through a link either: the page at the other end is not ours.</summary>
    [Fact]
    public void RefusesToDeleteThroughALink()
    {
        var reached = Resolve(".agents/skills/doctor/SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(reached)!);
        File.WriteAllText(reached, "the agents page\n");
        Directory.CreateDirectory(Resolve(".claude/skills"));
        Directory.CreateSymbolicLink(
            Resolve(".claude/skills/doctor"), Path.Combine("..", "..", ".agents", "skills", "doctor"));

        var report = PlanExecutor.Execute(
            Plan(new DeleteFile(".claude/skills/doctor/SKILL.md", "the agents page\n")), _dir);

        Assert.Equal(1, report.ExitCode);
        Assert.Equal("the agents page\n", Read(reached));
    }

    [Fact]
    public void AnEmptyPlanAppliesCleanly()
    {
        var report = PlanExecutor.Execute(Plan(), _dir);

        Assert.Empty(report.Results);
        Assert.Equal(0, report.ExitCode);
        Assert.False(report.HasRefusals);
    }

    [Fact]
    public void RefusesToApplyADryRunPlan()
    {
        var dry = new InitPlan(Coordinate, true, []);

        Assert.Contains("dry run",
            Assert.Throws<ArgumentException>(() => PlanExecutor.Execute(dry, _dir)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesToApplyToSomethingThatIsNotADirectory()
    {
        var file = Resolve("a-file");
        File.WriteAllText(file, "x");

        Assert.Contains("not a directory",
            Assert.Throws<ArgumentException>(() => PlanExecutor.Execute(Plan(), file)).Message,
            StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => PlanExecutor.Execute(null!, _dir));
        Assert.Throws<ArgumentException>(() => PlanExecutor.Execute(Plan(), null!));
    }
}
