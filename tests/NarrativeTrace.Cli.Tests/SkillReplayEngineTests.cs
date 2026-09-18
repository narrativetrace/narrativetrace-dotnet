// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Unit-level tests for the generic replay engine (skill-harness design §4.2), mirroring the canonical
/// TypeScript source's own "replaySkill (generic replayer, unit-level)" describe block — fakes for
/// <c>runCommand</c>/<c>tryVerify</c>, no real process, no real fixture. <see cref="SkillReplayRegistry"/>
/// (the concrete, process-spawning registry) is exercised for real by
/// <see cref="SkillsReplayCommandTests"/> instead.
/// </summary>
public sealed class SkillReplayEngineTests
{
    private static readonly Skill Base = new(
        CanonicalName: "fake-skill",
        SkillClass: SkillClass.Mechanical,
        Description: "d",
        WhenToUse: null,
        Fixture: "examples/fake",
        Steps: [],
        Always: [],
        Never: []);

    [Fact]
    public void A_step_with_neither_commands_nor_a_known_verify_is_not_ran_but_ok()
    {
        var step = new SkillStep("narrative only", new CommandStep([]));

        var result = SkillReplayEngine.ReplayStep(step, "/cwd", (_, _) => false, (_, _) => null);

        Assert.False(result.Ran);
        Assert.True(result.Ok);
    }

    [Fact]
    public void Runs_every_command_and_the_verify_for_a_step_that_has_both()
    {
        var seenCommands = new List<string>();
        var step = new SkillStep(
            "install", new CommandStep(["dotnet restore"]), Verify: "restore succeeded");

        var result = SkillReplayEngine.ReplayStep(
            step,
            "/cwd",
            (command, cwd) => { seenCommands.Add($"{command}@{cwd}"); return true; },
            (verify, _) => verify == "restore succeeded" ? true : null);

        Assert.Equal("install", result.Title);
        Assert.True(result.Ran);
        Assert.True(result.Ok);
        Assert.Equal(["dotnet restore@/cwd"], seenCommands);
    }

    [Fact]
    public void A_failing_command_is_reported_ran_but_not_ok()
    {
        var step = new SkillStep("flaky", new CommandStep(["dotnet test"]));

        var result = SkillReplayEngine.ReplayStep(step, "/cwd", (_, _) => false, (_, _) => null);

        Assert.True(result.Ran);
        Assert.False(result.Ok);
    }

    [Fact]
    public void A_snippet_step_with_a_known_verify_still_runs_the_verify()
    {
        var step = new SkillStep(
            "show it", new SnippetStep("x.cs", "csharp"), Verify: "the output looks right");

        var result = SkillReplayEngine.ReplayStep(
            step, "/cwd", (_, _) => throw new InvalidOperationException("must not run a command"),
            (verify, _) => verify == "the output looks right" ? true : null);

        Assert.True(result.Ran);
        Assert.True(result.Ok);
    }

    [Fact]
    public void A_snippet_step_with_an_unknown_prose_verify_is_not_ran()
    {
        var step = new SkillStep(
            "show it", new SnippetStep("x.cs", "csharp"), Verify: "a human reads the output and judges it");

        var result = SkillReplayEngine.ReplayStep(step, "/cwd", (_, _) => true, (_, _) => null);

        Assert.False(result.Ran);
        Assert.True(result.Ok);
    }

    [Fact]
    public void Replay_walks_every_step_of_a_skill_in_order()
    {
        var skill = Base with
        {
            Steps =
            [
                new SkillStep("one", new CommandStep(["dotnet a"])),
                new SkillStep("two", new CommandStep(["dotnet b"])),
            ],
        };

        var results = SkillReplayEngine.Replay(skill, "/cwd", (_, _) => true, (_, _) => null);

        Assert.Equal(["one", "two"], results.Select(r => r.Title));
        Assert.All(results, r => Assert.True(r is { Ran: true, Ok: true }));
    }
}
