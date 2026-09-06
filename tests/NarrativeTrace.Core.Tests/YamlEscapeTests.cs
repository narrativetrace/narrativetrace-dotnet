// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class YamlEscapeTests
{
    [Fact]
    public void Plain_value_passes_through_unquoted()
    {
        Assert.Equal(
            "Customer places order",
            YamlEscape.Scalar("Customer places order"));
    }

    [Theory]
    [InlineData("deploy: prod", "\"deploy: prod\"")]
    [InlineData("issue #42", "\"issue #42\"")]
    public void Yaml_significant_characters_force_quoting(
        string input, string expected)
    {
        Assert.Equal(expected, YamlEscape.Scalar(input));
    }

    [Fact]
    public void Embedded_quote_is_backslash_escaped()
    {
        Assert.Equal(
            "\"say \\\"hi\\\"\"",
            YamlEscape.Scalar("say \"hi\""));
    }

    [Fact]
    public void Embedded_backslash_is_doubled()
    {
        Assert.Equal(
            "\"a\\\\b\"",
            YamlEscape.Scalar("a\\b"));
    }

    [Fact]
    public void Newline_collapses_to_escape_on_a_single_line()
    {
        var result = YamlEscape.Scalar("line1\nline2\rline3");

        Assert.Equal("\"line1\\nline2\\rline3\"", result);
        Assert.DoesNotContain('\n', result);
    }

    [Fact]
    public void Empty_string_stays_empty()
    {
        Assert.Equal("", YamlEscape.Scalar(""));
    }

    [Theory]
    [InlineData("[flow-sequence]")]
    [InlineData("{flow-mapping}")]
    [InlineData("&anchor")]
    [InlineData("*alias")]
    [InlineData("`backtick`")]
    [InlineData("-item")]
    public void A_leading_yaml_indicator_character_forces_quoting(string input)
    {
        var result = YamlEscape.Scalar(input);

        Assert.StartsWith("\"", result, StringComparison.Ordinal);
        Assert.EndsWith("\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_leading_space_forces_quoting_even_when_followed_by_an_indicator()
    {
        // Found by the metadata fuzz route (class name " `'"): a leading
        // space does not shield the reserved backtick right behind it — a
        // real YAML scanner skips insignificant leading whitespace when
        // deciding where the plain scalar actually starts, so this crashed
        // a real parser even though value[0] itself (the space) is
        // harmless.
        var result = YamlEscape.Scalar(" `'");

        Assert.Equal("\" `'\"", result);
    }

    [Fact]
    public void A_trailing_space_forces_quoting_to_preserve_it()
    {
        // An unquoted plain scalar's trailing space is insignificant to a
        // YAML parser and would be silently dropped on reparse.
        var result = YamlEscape.Scalar("trailing ");

        Assert.Equal("\"trailing \"", result);
    }

    [Fact]
    public void An_indicator_character_that_is_not_leading_needs_no_quoting()
    {
        // Only a LEADING indicator is a structural hazard; the same
        // character mid-string is ordinary content.
        Assert.Equal(
            "price [discounted]", YamlEscape.Scalar("price [discounted]"));
    }

    [Fact]
    public void A_c1_control_character_is_escaped()
    {
        // The C1 range (U+0080-U+009F) is a control range ISOControl also
        // covers, but is neither a mid-string trigger character nor a
        // leading indicator by itself — found by the metadata fuzz route in
        // OutputFormatPropertyTests (corpus case "c1-controls") reaching a
        // real YAML parser, which rejected the raw character outright.
        var result = YamlEscape.Scalar("\u0085\u0090\u009f");

        Assert.Equal("\"\\x85\\x90\\x9F\"", result);
    }

    [Fact]
    public void A_lone_surrogate_becomes_the_unicode_replacement_character()
    {
        // No YAML escape can stand for a lone surrogate: \uD800 is valid
        // escape SYNTAX but decodes to the same invalid code unit, and a
        // real YAML parser rejects it right back (also found by the
        // metadata fuzz route, corpus case "unpaired-high-surrogate").
        var result = YamlEscape.Scalar("a\ud800b");

        Assert.Equal("\"a�b\"", result);
    }

    [Fact]
    public void A_well_formed_surrogate_pair_passes_through_unescaped()
    {
        // Every surrogate half triggers quoting on its own (NeedsQuoting has
        // no way to tell "half of a valid pair" from "lone" without walking
        // the whole string), but a well-formed pair is copied through intact
        // once inside the quotes — fidelity for a real character, an emoji
        // included, is part of being correct; only an actually-invalid code
        // unit gets replaced.
        const string emoji = "😀";

        Assert.Equal($"\"{emoji}\"", YamlEscape.Scalar(emoji));
    }
}
