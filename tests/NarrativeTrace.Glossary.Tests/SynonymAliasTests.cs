// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class SynonymAliasTests
{
    [Fact]
    public void Holds_alias_and_note()
    {
        var synonym = new SynonymAlias("account with overdraft", "legacy v1 API phrasing");

        Assert.Equal("account with overdraft", synonym.Alias);
        Assert.Equal("legacy v1 API phrasing", synonym.Note);
    }

    [Fact]
    public void Note_is_optional()
    {
        Assert.Null(new SynonymAlias("account with overdraft").Note);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_alias_is_rejected(string? alias)
    {
        Assert.Throws<ArgumentException>(() => new SynonymAlias(alias!));
    }

    [Fact]
    public void Equality_is_structural()
    {
        Assert.Equal(
            new SynonymAlias("old phrasing", "note"),
            new SynonymAlias("old phrasing", "note"));
        Assert.NotEqual(
            new SynonymAlias("old phrasing"),
            new SynonymAlias("other phrasing"));
    }
}
