// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;
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
    public void A_well_formed_surrogate_pair_becomes_a_unicode_escape()
    {
        // A raw astral-plane character in the frontmatter is spec-valid YAML, but a downstream
        // parser that reads in fixed-size chunks can land its buffer boundary between a pair's two
        // halves — SnakeYAML 2.3 (the java runtime's YAML oracle) reads 1024-char chunks and
        // crashes with IndexOutOfBoundsException on exactly this input. Frontmatter is emitted
        // BMP-only: the 8-digit \U escape is YAML's own (ns-esc-32-bit), a conforming parser
        // decodes it back to the same code point, and no boundary can ever split what is no longer
        // a pair. Confirmed: YamlDotNet (this port's own YAML oracle) does not reproduce that
        // crash on any tested alignment/size — the fix is for the machine-readable contract
        // broadly, not a bug found in this port's own oracle.
        const string emoji = "😀";

        Assert.Equal("\"\\U0001f600\"", YamlEscape.Scalar(emoji));
    }

    [Fact]
    public void A_long_astral_run_leaves_no_surrogate_at_any_parser_buffer_boundary()
    {
        // The minimized shape of java's fuzz crash class: enough astral pairs that a chunked
        // reader's buffer boundary could land on a high surrogate at some alignment. After
        // escaping, no output character is a surrogate at all, so the property holds for every
        // alignment, not just one.
        var result = YamlEscape.Scalar(string.Concat(Enumerable.Repeat("🙈", 600)));

        Assert.DoesNotContain(result, c => char.IsSurrogate(c));
    }
}
