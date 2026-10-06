// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// Prompt safety (the whole reason the runner tokenizes the template, never the prompt): a prompt
/// carrying shell metacharacters reaches the agent as ONE opaque argv element, never reinterpreted.
/// </summary>
public sealed class AgentArgvTests
{
    [Fact]
    public void Substitutes_the_prompt_as_a_single_argv_element()
    {
        var argv = AgentArgv.Build("claude -p \"{prompt}\" --model haiku", "hello world");

        Assert.Equal(["claude", "-p", "hello world", "--model", "haiku"], argv);
    }

    [Theory]
    [InlineData("run `id` now")]
    [InlineData("$(rm -rf /) please")]
    [InlineData("a\nb\nc")]
    [InlineData("quotes \" and ' inside")]
    [InlineData("semicolons; and | pipes && chains")]
    public void Passes_shell_metacharacters_in_the_prompt_through_verbatim(string hostile)
    {
        var argv = AgentArgv.Build("claude -p \"{prompt}\"", hostile);

        Assert.Equal(3, argv.Count);
        Assert.Equal(hostile, argv[2]);
    }

    [Fact]
    public void Respects_single_and_double_quoted_template_segments()
    {
        var argv = AgentArgv.Build("codex exec --sandbox 'workspace-write' \"{prompt}\"", "p");

        Assert.Equal(["codex", "exec", "--sandbox", "workspace-write", "p"], argv);
    }

    [Fact]
    public void A_template_without_the_placeholder_carries_no_prompt()
    {
        var argv = AgentArgv.Build("claude --version", "ignored");

        Assert.Equal(["claude", "--version"], argv);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_template_is_rejected(string template)
    {
        Assert.Throws<ArgumentException>(() => AgentArgv.Build(template, "p"));
    }

    [Fact]
    public void An_unterminated_quote_in_the_template_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => AgentArgv.Build("claude -p \"{prompt}", "p"));
    }
}
