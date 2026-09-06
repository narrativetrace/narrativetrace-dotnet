// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ControlEscapeTests
{
    [Fact]
    public void Newline_tab_and_carriage_return_map_to_mnemonic_escapes()
    {
        Assert.Equal("a\\nb\\tc\\rd", ControlEscape.Sanitize("a\nb\tc\rd"));
    }

    [Fact]
    public void Backspace_and_form_feed_map_to_mnemonic_escapes()
    {
        Assert.Equal("a\\bb\\fc", ControlEscape.Sanitize("a\bb\fc"));
    }

    [Fact]
    public void Other_control_chars_map_to_lowercase_unicode_escapes()
    {
        // ESC (0x1B) is the ANSI terminal-injection vector; it has no
        // mnemonic and becomes \\u001b.
        Assert.Equal("x\\u001by", ControlEscape.Sanitize("x\u001by"));
    }

    [Fact]
    public void Quotes_and_backslashes_stay_unchanged()
    {
        // Distinguishes control-escaping from JSON escaping: value
        // content like C:\temp and " survive.
        Assert.Equal(
            "C:\\temp \"q\"",
            ControlEscape.Sanitize("C:\\temp \"q\""));
    }

    [Fact]
    public void Plain_text_stays_unchanged()
    {
        Assert.Equal("order-42", ControlEscape.Sanitize("order-42"));
    }

    /// <summary>
    /// A java security fuzz suite finding, mirrored here: a lone <c>\uD800</c> is not a
    /// control character, so it reached <see cref="System.Text.Encoding.UTF8"/> unescaped — and
    /// .NET's default encoder raises <see cref="System.Text.EncoderFallbackException"/> on one, so a
    /// value the application merely returned could fail the run that traced it. Found by the
    /// security fuzz suite's <c>unpaired-high-surrogate</c> corpus case.
    /// </summary>
    [Fact]
    public void A_lone_high_surrogate_is_escaped_like_a_control_character()
    {
        Assert.Equal("a\\ud800b", ControlEscape.Sanitize("a\ud800b"));
    }

    [Fact]
    public void A_lone_low_surrogate_is_escaped_like_a_control_character()
    {
        Assert.Equal("a\\udc00b", ControlEscape.Sanitize("a\udc00b"));
    }

    [Fact]
    public void A_high_surrogate_at_the_end_of_the_text_is_escaped()
    {
        Assert.Equal("a\\ud800", ControlEscape.Sanitize("a\ud800"));
    }

    [Fact]
    public void A_reversed_pair_low_then_high_is_two_lone_surrogates()
    {
        Assert.Equal("\\udc00\\ud800", ControlEscape.Sanitize("\udc00\ud800"));
    }

    [Fact]
    public void A_well_formed_surrogate_pair_is_left_alone()
    {
        const string monkey = "🙈";
        Assert.Equal("see " + monkey + " here", ControlEscape.Sanitize("see " + monkey + " here"));
    }

    [Fact]
    public void Every_sanitized_string_is_utf8_encodable()
    {
        foreach (var hostile in new[] { "\ud800", "\udc00", "\udc00\ud800", "a\ud800b\udc00c" })
        {
            var sanitized = ControlEscape.Sanitize(hostile);
            var exception = Record.Exception(() => new System.Text.UTF8Encoding(false, throwOnInvalidBytes: true).GetBytes(sanitized));
            Assert.Null(exception);
        }
    }
}
