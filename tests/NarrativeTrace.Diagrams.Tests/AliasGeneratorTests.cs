// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

public class AliasGeneratorTests
{
    [Fact]
    public void Extracts_uppercase_initials()
    {
        var existing = new Dictionary<string, string>();

        var alias = AliasGenerator.Generate(
            "OrderService", existing);

        Assert.Equal("OS", alias);
    }

    [Fact]
    public void Single_capital_name_uses_that_capital()
    {
        var existing = new Dictionary<string, string>();

        var alias = AliasGenerator.Generate(
            "Repository", existing);

        Assert.Equal("R", alias);
    }

    [Fact]
    public void All_lowercase_name_uses_full_name()
    {
        var existing = new Dictionary<string, string>();

        var alias = AliasGenerator.Generate(
            "scheduler", existing);

        Assert.Equal("scheduler", alias);
    }

    [Fact]
    public void Three_capitals_use_first_two_only()
    {
        var existing = new Dictionary<string, string>();

        var alias = AliasGenerator.Generate(
            "ABCService", existing);

        Assert.Equal("AB", alias);
    }

    [Fact]
    public void Handles_conflicts_with_numbered_suffixes()
    {
        var existing = new Dictionary<string, string>
        {
            ["OtherService"] = "OS",
        };

        var alias = AliasGenerator.Generate(
            "OrderService", existing);

        Assert.Equal("OS2", alias);
    }

    [Fact]
    public void Resolves_three_way_conflict_with_incrementing_suffixes()
    {
        var existing = new Dictionary<string, string>();

        var first = AliasGenerator.Generate("OrderService", existing);
        var second = AliasGenerator.Generate("OtherStore", existing);
        var third = AliasGenerator.Generate("OpenSession", existing);

        Assert.Equal("OS", first);
        Assert.Equal("OS2", second);
        Assert.Equal("OS3", third);
    }

    [Fact]
    public void All_lowercase_name_with_hostile_characters_is_sanitized()
    {
        // An alias is never quoted at its use site — every arrow line emits
        // it bare — so a name with no uppercase letters (the ExtractInitials
        // fallback) must not hand back space/newline/quote characters that
        // would break an unquoted arrow line.
        var existing = new Dictionary<string, string>();

        var alias = AliasGenerator.Generate(
            "hostile name\nwith \"quotes\"", existing);

        Assert.DoesNotContain(' ', alias);
        Assert.DoesNotContain('\n', alias);
        Assert.DoesNotContain('"', alias);
    }

    [Fact]
    public void Same_name_returns_same_alias()
    {
        var existing = new Dictionary<string, string>
        {
            ["OrderService"] = "OS",
        };

        var alias = AliasGenerator.Generate(
            "OrderService", existing);

        Assert.Equal("OS", alias);
    }
}
