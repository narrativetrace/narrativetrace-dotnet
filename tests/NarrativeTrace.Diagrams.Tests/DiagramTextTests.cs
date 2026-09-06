// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

public class DiagramTextTests
{
    [Fact]
    public void Folds_every_control_character_to_space_not_just_line_feed()
    {
        // CR, TAB and a generic C0 control alongside LF — narrowing the
        // fold to '\n' alone would leave these line-injection vectors live.
        var raw = "a\nb\rc\td" + (char)0 + "e";

        Assert.Equal("a b c d e", DiagramText.Message(raw));
    }

    [Fact]
    public void Preserves_text_with_no_control_characters_unchanged()
    {
        Assert.Equal("\"order-42\"", DiagramText.Message("\"order-42\""));
    }

    [Fact]
    public void Identifier_folds_control_characters_to_space()
    {
        Assert.Equal("a b c", DiagramText.Identifier("a\nb\rc"));
    }

    [Fact]
    public void Identifier_turns_a_quote_into_an_apostrophe()
    {
        // Neither diagram grammar can escape a quote inside a quoted name —
        // the character must stop being a quote.
        Assert.Equal("Foo'Bar", DiagramText.Identifier("Foo\"Bar"));
    }

    [Fact]
    public void Identifier_breaks_up_a_mermaid_comment_opener()
    {
        Assert.DoesNotContain("%%", DiagramText.Identifier("Foo%%comment"));
    }

    [Fact]
    public void Identifier_caps_length()
    {
        var huge = new string('a', 500);

        Assert.True(DiagramText.Identifier(huge).Length <= 100);
    }

    [Fact]
    public void Identifier_of_empty_text_is_the_unnamed_marker()
    {
        Assert.Equal("<unnamed>", DiagramText.Identifier(""));
    }

    [Fact]
    public void Identifier_of_all_control_text_is_the_unnamed_marker()
    {
        Assert.Equal("<unnamed>", DiagramText.Identifier("\n\r\t"));
    }

    [Fact]
    public void QuoteIfNeeded_quotes_angle_brackets()
    {
        Assert.Equal("\"<unnamed>\"", DiagramText.QuoteIfNeeded("<unnamed>"));
    }
}
