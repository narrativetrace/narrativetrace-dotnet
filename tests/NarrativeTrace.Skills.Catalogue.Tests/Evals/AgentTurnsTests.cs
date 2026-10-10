// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// Everything that drives a trial's agent: one template per turn, one prompt per turn, and the
/// session id that makes several turns ONE conversation. Every way of being wrong is REFUSED rather
/// than degraded — a multi-turn case run as independent single turns would reach its approval
/// question with an agent that had forgotten the draft, and pass for the wrong reason.
/// </summary>
public sealed class AgentTurnsTests
{
    private const string Session = "6bd1f7fb-2d90-441a-be7e-ac5a42b60a40";
    private const string Skill = "narrativetrace-feedback";

    private static AgentTurns Of(Platform platform, string? overrideCommand, IReadOnlyList<string> replies, string? session = Session) =>
        AgentTurns.Of(platform, "claude-haiku-5-5", Skill, overrideCommand, "report it", replies, session);

    [Fact]
    public void A_case_with_no_scripted_replies_is_one_turn_driven_by_the_single_turn_preset()
    {
        var turns = Of(Platform.Claude, null, []);

        Assert.Equal(1, turns.TurnCount);
        Assert.True(turns.DrivesAnAgent);
        Assert.Equal(Platform.Claude.PresetAgentCommand("claude-haiku-5-5", Skill), turns.CommandForTurn(1));
        Assert.Equal("report it", turns.PromptForTurn(1));
    }

    [Fact]
    public void An_explicit_agent_command_overrides_the_single_turn_preset()
    {
        var turns = Of(Platform.Claude, "my-agent \"{prompt}\"", []);

        Assert.Equal("my-agent \"{prompt}\"", turns.CommandForTurn(1));
    }

    [Fact]
    public void A_scripted_reply_becomes_a_later_turn_with_the_resumed_template()
    {
        var turns = Of(Platform.Claude, null, ["yes, file it"]);

        Assert.Equal(2, turns.TurnCount);
        Assert.Equal("yes, file it", turns.PromptForTurn(2));
        Assert.EndsWith("--session-id " + Session, turns.CommandForTurn(1), StringComparison.Ordinal);
        Assert.EndsWith("--resume " + Session, turns.CommandForTurn(2), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_turn_after_the_first_resumes_the_same_conversation()
    {
        var turns = Of(Platform.Claude, null, ["first", "second", "third"]);

        Assert.Equal(4, turns.TurnCount);
        Assert.All(
            Enumerable.Range(2, 3),
            turn => Assert.EndsWith("--resume " + Session, turns.CommandForTurn(turn), StringComparison.Ordinal));
        Assert.DoesNotContain(
            Enumerable.Range(1, 4),
            turn => turns.CommandForTurn(turn).Contains(Platforms.SessionPlaceholder, StringComparison.Ordinal));
    }

    /// <summary>
    /// One override string cannot be two templates, and the one it would be is the FIRST turn's — so
    /// an accepted override would run every later turn as a fresh conversation.
    /// </summary>
    [Fact]
    public void Refuses_an_override_for_a_multi_turn_case_rather_than_using_it_for_the_first_turn_only()
    {
        var thrown = Assert.Throws<ArgumentException>(() => Of(Platform.Claude, "my-agent \"{prompt}\"", ["yes"]));

        Assert.Contains("--agent-command", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Platform.Codex)]
    [InlineData(Platform.Gemini)]
    public void Refuses_a_multi_turn_case_on_a_platform_that_cannot_resume_a_session(Platform platform)
    {
        var thrown = Assert.Throws<ArgumentException>(() => Of(platform, null, ["yes"]));

        Assert.Contains("cannot drive a multi-turn case", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Platform.Codex)]
    [InlineData(Platform.Gemini)]
    public void A_single_turn_case_still_runs_on_a_platform_that_cannot_resume_a_session(Platform platform)
    {
        var turns = Of(platform, null, []);

        Assert.Equal(platform.PresetAgentCommand("claude-haiku-5-5", Skill), turns.CommandForTurn(1));
    }

    /// <summary>An id carrying whitespace or a quote would split into two argv elements and change the command.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-uuid")]
    [InlineData("6bd1f7fb-2d90-441a-be7e-ac5a42b60a40 --dangerously-skip-permissions")]
    [InlineData("6bd1f7fb-2d90-441a-be7e-ac5a42b60a4\"")]
    [InlineData("x6bd1f7fb-2d90-441a-be7e-ac5a42b60a40")]
    [InlineData("6bd1f7fb-2d90-441a-be7e-ac5a42b60a40\n")]
    public void Refuses_a_session_id_that_is_not_one_opaque_token(string? session)
    {
        var thrown = Assert.Throws<ArgumentException>(() => Of(Platform.Claude, null, ["yes"], session));

        Assert.Contains("session id", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_single_turn_case_needs_no_session_id()
    {
        Assert.Equal(1, Of(Platform.Claude, null, [], session: null).TurnCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Refuses_a_turn_number_outside_the_conversation(int turn)
    {
        var turns = Of(Platform.Claude, null, ["yes"]);

        Assert.Throws<ArgumentOutOfRangeException>(() => turns.PromptForTurn(turn));
        Assert.Throws<ArgumentOutOfRangeException>(() => turns.CommandForTurn(turn));
    }

    [Fact]
    public void A_trial_that_drives_no_agent_grades_the_fixture_as_it_stands()
    {
        var turns = AgentTurns.Graded("report it");

        Assert.False(turns.DrivesAnAgent);
        Assert.Equal(1, turns.TurnCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Refuses_a_blank_prompt_at_any_turn(string blank)
    {
        Assert.Throws<ArgumentException>(() => AgentTurns.Of(Platform.Claude, "m", Skill, null, blank, [], Session));
        Assert.Throws<ArgumentException>(() => AgentTurns.Of(Platform.Claude, "m", Skill, null, "report it", [blank], Session));
    }

    [Fact]
    public void Refuses_several_turns_with_no_template_that_resumes_the_session()
    {
        Assert.Throws<ArgumentException>(() => new AgentTurns("claude -p \"{prompt}\"", null, ["one", "two"]));
    }

    [Fact]
    public void Refuses_a_trial_with_no_turn_at_all()
    {
        Assert.Throws<ArgumentException>(() => new AgentTurns("claude -p \"{prompt}\"", null, []));
    }

    [Fact]
    public void Keeps_its_own_copy_of_the_turns_it_was_given()
    {
        var prompts = new List<string> { "one" };
        var turns = new AgentTurns("claude -p \"{prompt}\"", null, prompts);

        prompts.Add("two");

        Assert.Equal(1, turns.TurnCount);
    }
}
