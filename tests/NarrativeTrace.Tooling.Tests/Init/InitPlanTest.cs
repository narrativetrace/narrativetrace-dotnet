// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Action = NarrativeTrace.Tooling.Init.Action;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// A plan is the whole of what an install would do, computed before anything is written. These cases
/// pin what an action promises the executor — a complete before and after, never a path outside the
/// project — and what a plan promises a reader: one action per path, and a refusal that says why.
/// Method names mirror the Java <c>InitPlanTest</c>. The shared subject's invariant is checked at
/// setup and teardown (the C# shape of Java's InvariantCheckExtension).
/// </summary>
public sealed class InitPlanTest : IDisposable
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private readonly InitPlan _subject;

    public InitPlanTest()
    {
        _subject = Plan(new CreateFile("AGENTS.md", "block\n"));
        Assert.True(_subject.Invariant());
    }

    public void Dispose() => Assert.True(_subject.Invariant());

    private static InitPlan Plan(params Action[] actions) => new(Coordinate, false, actions);

    // --- what each action promises --------------------------------------------------------------

    [Fact]
    public void ACreatedFileHasNoBefore()
    {
        var action = new CreateFile("AGENTS.md", "block\n");

        Assert.Equal("", action.Before);
        Assert.Equal("block\n", action.After);
        Assert.Equal("create", action.Kind);
    }

    [Fact]
    public void AReplacementCarriesBothWholeFileTexts()
    {
        var action = new ReplaceBlock("AGENTS.md", "old\n", "new\n");

        Assert.Equal("old\n", action.Before);
        Assert.Equal("new\n", action.After);
        Assert.Equal("replace", action.Kind);
    }

    [Fact]
    public void AnAppendComputesItsAfterFromWhatWasThere()
    {
        var action = new AppendBlock("AGENTS.md", "# Title\n", "block\n");

        Assert.Equal("# Title\n\nblock\n", action.After);
        Assert.Equal("append", action.Kind);
    }

    [Fact]
    public void AnAppendedLineEndsWithTheFilesOwnLineEnding()
    {
        var unix = new AppendLine("CLAUDE.md", "# C\n", "@AGENTS.md");
        var windows = new AppendLine("CLAUDE.md", "# C\r\n", "@AGENTS.md");

        Assert.Equal("# C\n\n@AGENTS.md\n", unix.After);
        Assert.Equal("# C\r\n\r\n@AGENTS.md\r\n", windows.After);
        Assert.Equal("append-line", unix.Kind);
    }

    [Fact]
    public void ADeletionEndsWithNothing()
    {
        var file = new DeleteFile("AGENTS.md", "gone\n");
        var directory = new DeleteDirectory(".agents/skills/doctor");

        Assert.Equal("", file.After);
        Assert.Equal("gone\n", file.Before);
        Assert.Equal("delete", file.Kind);
        Assert.Equal("delete-directory", directory.Kind);
    }

    [Fact]
    public void ARefusalCarriesItsReason()
    {
        var refusal = new Refuse("AGENTS.md", "two blocks");

        Assert.Equal("two blocks", refusal.Reason);
        Assert.Equal("refuse", refusal.Kind);
    }

    [Fact]
    public void RefusesAnActionOnAPathOutsideTheProject()
    {
        Assert.Contains("project-relative",
            Assert.Throws<ArgumentException>(() => new CreateFile("/etc/passwd", "x")).Message,
            StringComparison.Ordinal);
        Assert.Contains("project-relative",
            Assert.Throws<ArgumentException>(() => new CreateFile("../outside.md", "x")).Message,
            StringComparison.Ordinal);
        Assert.Contains("project-relative",
            Assert.Throws<ArgumentException>(() => new DeleteDirectory(".agents/../../x")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesARefusalWithoutAReason()
    {
        Assert.Contains("reason",
            Assert.Throws<ArgumentException>(() => new Refuse("AGENTS.md", " ")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAnActionWithoutAPathOrContent()
    {
        Assert.Contains("path",
            Assert.Throws<ArgumentException>(() => new CreateFile(null!, "x")).Message,
            StringComparison.Ordinal);
        Assert.Contains("text",
            Assert.Throws<ArgumentException>(() => new CreateFile("AGENTS.md", null!)).Message,
            StringComparison.Ordinal);
    }

    // --- what a plan promises ------------------------------------------------------------------

    [Fact]
    public void AnEmptyPlanIsWhatANothingToDoRunLooksLike()
    {
        var empty = Plan();

        Assert.True(empty.IsEmpty);
        Assert.False(empty.HasRefusals);
        Assert.Equal(0, empty.ExitCode);
        Assert.True(empty.Invariant());
    }

    [Fact]
    public void ARefusalMakesTheRunExitOne()
    {
        var refused = Plan(new Refuse("AGENTS.md", "two blocks"));

        Assert.True(refused.HasRefusals);
        Assert.Single(refused.Refusals());
        Assert.Equal(1, refused.ExitCode);
    }

    [Fact]
    public void ADryRunAlwaysExitsZeroEvenWithARefusal()
    {
        var dry = new InitPlan(Coordinate, true, [new Refuse("AGENTS.md", "no")]);

        Assert.True(dry.DryRun);
        Assert.True(dry.HasRefusals);
        Assert.Equal(0, dry.ExitCode);
    }

    [Fact]
    public void RefusesTwoActionsOnOnePath()
    {
        Assert.Throws<ArgumentException>(() =>
            Plan(
                new CreateFile("AGENTS.md", "a"),
                new ReplaceBlock("AGENTS.md", "a", "b")));
    }

    [Fact]
    public void RefusesAPlanWithoutACarrier()
    {
        Assert.Contains("carrier",
            Assert.Throws<ArgumentException>(() => new InitPlan(" ", false, [])).Message,
            StringComparison.Ordinal);
        Assert.Contains("actions",
            Assert.Throws<ArgumentException>(() => new InitPlan(Coordinate, false, null!)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsItsActionsImmutable()
    {
        var onlyOne = Plan(new CreateFile("AGENTS.md", "a"));

        Assert.Throws<NotSupportedException>(() => ((IList<Action>)onlyOne.Actions).Clear());
    }

    // --- options -------------------------------------------------------------------------------

    [Fact]
    public void TheDefaultOptionsWriteNothingItWasNotAskedTo()
    {
        var options = InitOptions.Defaults();

        Assert.False(options.DryRun);
        Assert.False(options.WriteExisting);
        Assert.False(options.Force);
        Assert.Equal(Scope.Both, options.Scope);
        Assert.Equal(Vendor.Auto, options.VendorClaude);
    }

    [Fact]
    public void EachScopeKnowsWhatItCovers()
    {
        Assert.True(Scope.Both.IncludesSkills());
        Assert.True(Scope.Both.IncludesAgentsMd());
        Assert.True(Scope.Skills.IncludesSkills());
        Assert.False(Scope.Skills.IncludesAgentsMd());
        Assert.False(Scope.AgentsMd.IncludesSkills());
        Assert.True(Scope.AgentsMd.IncludesAgentsMd());
    }

    [Fact]
    public void ChangesOneOptionAtATime()
    {
        var options = InitOptions.Defaults()
            .WithDryRun(true)
            .WithWriteExisting(true)
            .WithForce(true)
            .WithScope(Scope.Skills)
            .WithVendorClaude(Vendor.Off);

        Assert.True(options.DryRun);
        Assert.True(options.WriteExisting);
        Assert.True(options.Force);
        Assert.Equal(Scope.Skills, options.Scope);
        Assert.Equal(Vendor.Off, options.VendorClaude);
    }

    // C# enums cannot be null (Java's null test); an undefined value is the equivalent invalid input.
    [Fact]
    public void RefusesOptionsWithoutAScopeOrAVendorRule()
    {
        Assert.Contains("scope",
            Assert.Throws<ArgumentException>(() => InitOptions.Defaults().WithScope((Scope)999)).Message,
            StringComparison.Ordinal);
        Assert.Contains("vendor",
            Assert.Throws<ArgumentException>(() => InitOptions.Defaults().WithVendorClaude((Vendor)999)).Message,
            StringComparison.Ordinal);
    }
}
