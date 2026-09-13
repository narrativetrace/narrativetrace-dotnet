// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class JsonEscapeTests
{
    [Theory]
    [InlineData("\\", "\\\\")]
    [InlineData("\"", "\\\"")]
    [InlineData("\n", "\\n")]
    [InlineData("\r", "\\r")]
    [InlineData("\t", "\\t")]
    [InlineData("\b", "\\b")]
    [InlineData("\f", "\\f")]
    public void Escapes_every_named_control_character(string input, string expected)
    {
        Assert.Equal(expected, JsonEscape.Escape(input));
    }

    [Fact]
    public void An_unnamed_control_character_becomes_a_unicode_escape()
    {
        var input = ((char)1).ToString();

        Assert.Equal("\\u0001", JsonEscape.Escape(input));
    }

    [Fact]
    public void An_ordinary_character_passes_through_verbatim()
    {
        Assert.Equal("hello", JsonEscape.Escape("hello"));
    }

    [Fact]
    public void The_boundary_character_0x20_is_not_escaped()
    {
        Assert.Equal(" ", JsonEscape.Escape(" "));
    }
}
