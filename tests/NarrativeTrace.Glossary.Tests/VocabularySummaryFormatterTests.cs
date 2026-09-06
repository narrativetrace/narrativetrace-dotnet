// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class VocabularySummaryFormatterTests
{
    [Theory]
    [InlineData(0, "Vocabulary: 0 new terms harvested")]
    [InlineData(1, "Vocabulary: 1 new term harvested")]
    [InlineData(3, "Vocabulary: 3 new terms harvested")]
    public void Formats_new_term_count_with_singular_plural(int count, string expected)
    {
        Assert.Equal(expected, VocabularySummaryFormatter.FormatSummary(count, []));
    }

    [Fact]
    public void Appends_detail_line_per_violation_with_rename_suggestion()
    {
        var summary = VocabularySummaryFormatter.FormatSummary(
            0,
            [
                Violation("accountWithOverdraft", "overdraftAccount"),
                Violation("openAccountWithOverdraft", "openOverdraftAccount"),
            ]);

        Assert.Equal(
            "Vocabulary: 0 new terms harvested, 2 deprecated synonyms in use\n"
            + "  accountWithOverdraft → use overdraftAccount"
            + " (billing: \"overdraft account\")\n"
            + "  openAccountWithOverdraft → use openOverdraftAccount"
            + " (billing: \"overdraft account\")",
            summary);
    }

    [Fact]
    public void Singular_violation_line_and_no_suggestion_fallback()
    {
        var summary = VocabularySummaryFormatter.FormatSummary(
            1, [Violation("legacyName", null)]);

        Assert.Equal(
            "Vocabulary: 1 new term harvested, 1 deprecated synonym in use\n"
            + "  legacyName → use canonical term \"overdraft account\" (billing)",
            summary);
    }

    [Fact]
    public void Rejects_negative_count_and_null_violations()
    {
        Assert.Throws<ArgumentException>(
            () => VocabularySummaryFormatter.FormatSummary(-1, []));
        Assert.Throws<ArgumentNullException>(
            () => VocabularySummaryFormatter.FormatSummary(0, null!));
    }

    private static VocabularyViolation Violation(string identifier, string? suggested)
    {
        return new VocabularyViolation(
            "billing", "account with overdraft", "overdraft account",
            "A.site", identifier, suggested, 1);
    }
}
