// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

public sealed class PlatformTests
{
    [Theory]
    [InlineData("claude", Platform.Claude)]
    [InlineData("CLAUDE", Platform.Claude)]
    [InlineData("codex", Platform.Codex)]
    [InlineData("antigravity", Platform.Antigravity)]
    [InlineData("cursor", Platform.Cursor)]
    [InlineData("Cursor", Platform.Cursor)]
    public void Parses_a_known_platform_case_insensitively(string value, Platform expected)
    {
        Assert.Equal(expected, Platforms.Parse(value));
    }

    [Theory]
    [InlineData("bard")]
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_an_unknown_or_missing_platform(string? value)
    {
        Assert.Null(Platforms.Parse(value));
    }

    [Fact]
    public void Only_codex_antigravity_and_cursor_are_sporadic()
    {
        Assert.False(Platform.Claude.IsSporadic());
        Assert.True(Platform.Codex.IsSporadic());
        Assert.True(Platform.Antigravity.IsSporadic());
        Assert.True(Platform.Cursor.IsSporadic());
    }

    /// <summary>
    /// Cursor's headless CLI is <c>agent</c>; <c>--force</c> lets it run commands and apply edits
    /// without a confirmation nobody is there to give, for EVERY skill — even the read-only doctor,
    /// which runs its diagnosis through a command, so a preset narrower than the prompt would
    /// measure the sandbox and not the skill. No <c>--model</c>: the runner's model argument is a
    /// ledger label, and a label that is not a Cursor model name would fail the whole trial.
    /// </summary>
    [Theory]
    [InlineData("narrativetrace-doctor")]
    [InlineData("add-narrative-tracing")]
    public void The_cursor_preset_is_the_headless_agent_with_force_for_every_skill(string skill)
    {
        Assert.Equal(
            "agent -p \"{prompt}\" --output-format text --force",
            Platform.Cursor.PresetAgentCommand("auto", skill));
    }

    [Fact]
    public void Cursor_and_antigravity_name_their_tools_for_the_skip_line()
    {
        Assert.Equal("agent", Platform.Cursor.CliTool());
        Assert.Equal("claude", Platform.Claude.CliTool());
        Assert.Equal("codex", Platform.Codex.CliTool());
        Assert.Equal("agy", Platform.Antigravity.CliTool());
    }

    [Fact]
    public void The_claude_preset_carries_the_init_prompts_allowed_tools()
    {
        var command = Platform.Claude.PresetAgentCommand("haiku", "add-narrative-tracing");

        Assert.Equal(
            "claude -p \"{prompt}\" --model haiku --allowed-tools \"Bash,Read,Edit,Write,WebFetch,Skill\""
            + " --output-format stream-json --verbose",
            command);
    }

    /// <summary>
    /// A grader has to read the agent's TOOL CALLS, not only its closing text: a URL a command
    /// printed and a URL typed into a reply are the same event to a gate that must see neither
    /// before the approval turn. <c>--verbose</c> is load-bearing — without it this CLI exits 1
    /// having written nothing at all.
    /// </summary>
    [Fact]
    public void The_claude_preset_streams_machine_readable_events_with_verbose()
    {
        var command = Platform.Claude.PresetAgentCommand("claude-haiku-5-5", "narrativetrace-feedback");

        Assert.EndsWith("--output-format stream-json --verbose", command, StringComparison.Ordinal);
    }

    /// <summary>The same sandbox, tools and format as one turn — differing only in being one conversation.</summary>
    [Fact]
    public void Claude_opens_a_conversation_with_a_session_id_and_resumes_the_same_one()
    {
        var single = Platform.Claude.PresetAgentCommand("m", "narrativetrace-feedback");

        Assert.Equal(single + " --session-id {session}", Platform.Claude.PresetFirstTurnCommand("m", "narrativetrace-feedback"));
        Assert.Equal(single + " --resume {session}", Platform.Claude.PresetResumedTurnCommand("m", "narrativetrace-feedback"));
    }

    /// <summary>
    /// Empty for BOTH halves: a platform that could open a conversation but not resume it would run
    /// its "second turn" as a fresh session with no memory of the draft, where "nothing was filed"
    /// is true for a reason that has nothing to do with the gate under test.
    /// </summary>
    [Theory]
    [InlineData(Platform.Codex)]
    [InlineData(Platform.Antigravity)]
    [InlineData(Platform.Cursor)]
    public void A_platform_whose_resume_flags_were_never_verified_offers_neither_half(Platform platform)
    {
        Assert.Null(platform.PresetFirstTurnCommand("m", "narrativetrace-feedback"));
        Assert.Null(platform.PresetResumedTurnCommand("m", "narrativetrace-feedback"));
    }

    [Fact]
    public void A_read_only_skill_gets_the_least_privileged_codex_sandbox()
    {
        Assert.Contains("--sandbox read-only", Platform.Codex.PresetAgentCommand("m", "narrativetrace-doctor"), StringComparison.Ordinal);
        Assert.Contains("--sandbox workspace-write", Platform.Codex.PresetAgentCommand("m", "add-narrative-tracing"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_antigravity_preset_skips_permissions_for_every_skill_and_names_the_model()
    {
        // agy 1.3.3 (verified 2026-10-10): --dangerously-skip-permissions is its only headless
        // auto-approval — no --yolo, no approval mode — so it rides every skill, like Cursor's --force.
        const string expected = "agy -p \"{prompt}\" --model gemini-3.8-flash-low --output-format text --dangerously-skip-permissions";

        Assert.Equal(expected, Platform.Antigravity.PresetAgentCommand("gemini-3.8-flash-low", "narrativetrace-doctor"));
        Assert.Equal(expected, Platform.Antigravity.PresetAgentCommand("gemini-3.8-flash-low", "add-narrative-tracing"));
    }

    [Fact]
    public void The_wire_name_is_the_lowercase_platform()
    {
        Assert.Equal("claude", Platform.Claude.WireName());
        Assert.Equal("antigravity", Platform.Antigravity.WireName());
    }
}
