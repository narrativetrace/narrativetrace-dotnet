// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Adversarial probes of <see cref="ControlEscape"/> and <see cref="YamlEscape"/> around U+2028 and
/// U+2029, surrogate pairs and line feeds.
/// </summary>
public sealed class AdversarialPhase7EscapeTests
{
    [Fact]
    public void Line_separator_at_the_start_is_escaped()
    {
        Assert.Equal("\\u2028x", ControlEscape.Sanitize("\u2028x"));
    }

    [Fact]
    public void Paragraph_separator_at_the_end_is_escaped()
    {
        Assert.Equal("x\\u2029", ControlEscape.Sanitize("x\u2029"));
    }

    [Fact]
    public void Lone_separator_alone_is_escaped()
    {
        Assert.Equal("\\u2028", ControlEscape.Sanitize("\u2028"));
    }

    [Fact]
    public void Separator_after_an_emoji_pair_keeps_the_pair_and_escapes_the_separator()
    {
        Assert.Equal("\uD83D\uDE00\\u2028", ControlEscape.Sanitize("\uD83D\uDE00\u2028"));
    }

    [Fact]
    public void Separator_between_a_lone_high_surrogate_and_a_low_surrogate_splits_them_into_escapes()
    {
        Assert.Equal("\\ud83d\\u2028\\ude00", ControlEscape.Sanitize("\uD83D\u2028\uDE00"));
    }

    [Fact]
    public void Separator_next_to_line_feed_escapes_both_without_merging_them()
    {
        Assert.Equal("\\u2028\\n\\u2029", ControlEscape.Sanitize("\u2028\n\u2029"));
    }

    [Fact]
    public void Lone_low_surrogate_at_the_start_is_escaped()
    {
        Assert.Equal("\\udc00", ControlEscape.Sanitize("\uDC00"));
    }

    [Fact]
    public void Replacement_character_is_ordinary_content_and_is_left_alone()
    {
        Assert.Equal("a\uFFFDb", ControlEscape.Sanitize("a\uFFFDb"));
    }

    [Fact]
    public void Backslash_before_a_separator_is_kept_and_the_separator_is_escaped()
    {
        Assert.Equal("C:\\temp\\u2028", ControlEscape.Sanitize("C:\\temp\u2028"));
    }

    [Fact]
    public void Empty_string_is_returned_unchanged()
    {
        Assert.Equal("", ControlEscape.Sanitize(""));
    }

    [Fact]
    public void Yaml_scalar_quotes_a_value_that_starts_with_a_line_separator()
    {
        Assert.Equal("\"\\u2028x\"", YamlEscape.Scalar("\u2028x"));
    }

    [Fact]
    public void Yaml_scalar_quotes_a_value_that_ends_with_a_paragraph_separator()
    {
        Assert.Equal("\"x\\u2029\"", YamlEscape.Scalar("x\u2029"));
    }

    [Fact]
    public void Yaml_scalar_escapes_an_emoji_pair_as_one_long_escape()
    {
        Assert.Equal("\"\\U0001f600\"", YamlEscape.Scalar("\uD83D\uDE00"));
    }

    [Fact]
    public void Yaml_scalar_replaces_a_lone_surrogate_with_the_replacement_character()
    {
        Assert.Equal("\"a\uFFFD\"", YamlEscape.Scalar("a\uD800"));
    }

    [Fact]
    public void Yaml_scalar_uses_a_byte_escape_for_a_c1_control_character()
    {
        Assert.Equal("\"\\x85\"", YamlEscape.Scalar("\u0085"));
    }

    [Fact]
    public void Yaml_scalar_quotes_a_value_that_is_only_a_space()
    {
        Assert.Equal("\" \"", YamlEscape.Scalar(" "));
    }

    [Theory]
    [InlineData("-item")]
    [InlineData("&anchor")]
    [InlineData("`tick")]
    [InlineData("'quoted")]
    public void Yaml_scalar_quotes_a_value_with_a_leading_indicator(string value)
    {
        Assert.StartsWith("\"", YamlEscape.Scalar(value));
    }

    [Fact]
    public void Yaml_scalar_escapes_backslash_and_quote_inside_the_quoted_form()
    {
        Assert.Equal("\"C:\\\\t \\\"x\\\"\"", YamlEscape.Scalar("C:\\t \"x\""));
    }

    [Fact]
    public void Yaml_scalar_leaves_a_plain_value_unquoted()
    {
        Assert.Equal("plain value", YamlEscape.Scalar("plain value"));
    }
}
