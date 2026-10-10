// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Skills.Tests;

public sealed class CommandVocabularyTests
{
    [Fact]
    public void Claude_tool_pattern_wraps_a_command_as_a_bash_tool_rule_matching_it_and_its_arguments()
    {
        Assert.Equal("Bash(git *)", CommandVocabulary.ClaudeToolPattern("git"));
    }

    [Fact]
    public void Claude_tool_pattern_keeps_a_wrapper_commands_leading_dot_slash()
    {
        Assert.Equal("Bash(dotnet *)", CommandVocabulary.ClaudeToolPattern("dotnet"));
    }
}
