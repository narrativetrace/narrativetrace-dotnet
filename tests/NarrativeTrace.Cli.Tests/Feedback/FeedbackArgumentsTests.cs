// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Feedback;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Feedback;

public sealed class FeedbackArgumentsTests
{
    private static readonly string[] Complete =
    [
        "draft", "--category", "doctor", "--step", "trap.redaction-proof",
        "--did", "ran it", "--happened", "it broke", "--expected", "it works",
    ];

    [Fact]
    public void A_complete_command_line_has_no_error()
    {
        Assert.Null(FeedbackArguments.Parse(Complete).Error);
    }

    [Fact]
    public void A_missing_channel_is_named()
    {
        var parsed = FeedbackArguments.Parse(["--category", "doctor"]);

        Assert.Contains("feedback needs a channel", parsed.Error);
    }

    [Fact]
    public void An_unknown_channel_is_named()
    {
        var parsed = FeedbackArguments.Parse(["send"]);

        Assert.Contains("unknown feedback channel", parsed.Error);
    }

    [Fact]
    public void A_missing_mandatory_field_names_the_flag()
    {
        var parsed = FeedbackArguments.Parse(["draft", "--category", "doctor", "--step", "s", "--did", "d", "--happened", "h"]);

        Assert.Contains("--expected", parsed.Error);
    }

    [Fact]
    public void An_unknown_category_is_rejected()
    {
        var parsed = FeedbackArguments.Parse(["draft", "--category", "nonsense", "--step", "s", "--did", "d", "--happened", "h", "--expected", "e"]);

        Assert.Contains("unknown category", parsed.Error);
    }

    [Fact]
    public void An_unknown_option_is_named()
    {
        var parsed = FeedbackArguments.Parse([.. Complete, "--nope", "x"]);

        Assert.Contains("unknown option", parsed.Error);
    }

    [Fact]
    public void The_equals_form_of_a_value_flag_is_read()
    {
        var parsed = FeedbackArguments.Parse(
            ["draft", "--category=doctor", "--step=s", "--did=d", "--happened=h", "--expected=e", "--language=es"]);

        Assert.Null(parsed.Error);
        Assert.Equal("es", parsed.Language);
    }

    [Fact]
    public void Help_short_circuits_validation()
    {
        var parsed = FeedbackArguments.Parse(["--help"]);

        Assert.True(parsed.Help);
        Assert.Null(parsed.Error);
    }

    [Fact]
    public void The_language_defaults_to_english()
    {
        Assert.Equal("en", FeedbackArguments.Parse(Complete).Language);
    }

    [Fact]
    public void The_agent_and_trace_flags_are_read()
    {
        var parsed = FeedbackArguments.Parse(
            [.. Complete, "--agent-product", "example-agent", "--agent-model", "example-model", "--trace", "order.nt"]);

        Assert.Null(parsed.Error);
        Assert.Equal("example-agent", parsed.AgentProduct);
        Assert.Equal("example-model", parsed.AgentModel);
        Assert.Equal("order.nt", parsed.Trace);
    }

    [Fact]
    public void A_value_flag_with_no_value_is_an_error()
    {
        var parsed = FeedbackArguments.Parse(["draft", "--category", "doctor", "--step"]);

        Assert.Contains("--step needs a value", parsed.Error);
    }
}
