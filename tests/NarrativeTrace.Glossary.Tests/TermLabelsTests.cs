// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class TermLabelsTests
{
    [Theory]
    [InlineData(TermKind.Word, "word")]
    [InlineData(TermKind.NounPhrase, "noun-phrase")]
    [InlineData(TermKind.VerbPhrase, "verb-phrase")]
    [InlineData(TermKind.Template, "template")]
    public void Kind_json_name_is_kebab_case(TermKind kind, string expected)
    {
        Assert.Equal(expected, kind.JsonName());
    }

    [Theory]
    [InlineData(TermStatus.Harvested, "harvested")]
    [InlineData(TermStatus.Curated, "curated")]
    [InlineData(TermStatus.Stale, "stale")]
    public void Status_json_name_is_lowercase(TermStatus status, string expected)
    {
        Assert.Equal(expected, status.JsonName());
    }

    [Fact]
    public void Unknown_kind_value_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((TermKind)99).JsonName());
    }

    [Fact]
    public void Unknown_status_value_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((TermStatus)99).JsonName());
    }
}
