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
    [InlineData("gemini", Platform.Gemini)]
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
    public void Only_codex_and_gemini_are_sporadic()
    {
        Assert.False(Platform.Claude.IsSporadic());
        Assert.True(Platform.Codex.IsSporadic());
        Assert.True(Platform.Gemini.IsSporadic());
    }

    [Fact]
    public void The_claude_preset_carries_the_init_prompts_allowed_tools()
    {
        var command = Platform.Claude.PresetAgentCommand("haiku", "add-narrative-tracing");

        Assert.Equal(
            "claude -p \"{prompt}\" --model haiku --allowed-tools \"Bash,Read,Edit,Write,WebFetch\"",
            command);
    }

    [Fact]
    public void A_read_only_skill_gets_the_least_privileged_codex_sandbox()
    {
        Assert.Contains("--sandbox read-only", Platform.Codex.PresetAgentCommand("m", "narrativetrace-doctor"), StringComparison.Ordinal);
        Assert.Contains("--sandbox workspace-write", Platform.Codex.PresetAgentCommand("m", "add-narrative-tracing"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_read_only_skill_gets_the_least_privileged_gemini_approval_mode()
    {
        Assert.Contains("--approval-mode plan", Platform.Gemini.PresetAgentCommand("m", "narrativetrace-doctor"), StringComparison.Ordinal);
        Assert.Contains("--approval-mode auto_edit", Platform.Gemini.PresetAgentCommand("m", "add-narrative-tracing"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_wire_name_is_the_lowercase_platform()
    {
        Assert.Equal("claude", Platform.Claude.WireName());
        Assert.Equal("gemini", Platform.Gemini.WireName());
    }
}
