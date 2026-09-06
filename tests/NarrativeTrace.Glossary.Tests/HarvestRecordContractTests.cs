// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class HarvestRecordContractTests
{
    private static readonly HarvestCandidate Valid = new(
        "billing", "overdraft account", TermKind.NounPhrase,
        "OverdraftService.open", "overdraftAccount", 1);

    [Theory]
    [InlineData("", "phrase", "site", "id")]
    [InlineData("context", " ", "site", "id")]
    [InlineData("context", "phrase", "", "id")]
    [InlineData("context", "phrase", "site", " ")]
    public void Candidate_rejects_blank_fields(
        string context, string phrase, string site, string identifier)
    {
        Assert.Throws<ArgumentException>(() => new HarvestCandidate(
            context, phrase, TermKind.Word, site, identifier, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Candidate_rejects_non_positive_occurrences(int occurrences)
    {
        Assert.Throws<ArgumentException>(() => new HarvestCandidate(
            "billing", "phrase", TermKind.Word, "Site.s", "id1", occurrences));
    }

    [Fact]
    public void Candidate_equality_is_structural()
    {
        Assert.Equal(
            Valid,
            new HarvestCandidate(
                "billing", "overdraft account", TermKind.NounPhrase,
                "OverdraftService.open", "overdraftAccount", 1));
    }

    [Fact]
    public void Harvest_result_copies_and_rejects_null()
    {
        var mutable = new List<HarvestCandidate> { Valid };
        var result = new HarvestResult(mutable);

        mutable.Clear();

        Assert.Single(result.Candidates);
        Assert.Throws<ArgumentNullException>(() => new HarvestResult(null!));
    }

    [Fact]
    public void Merge_result_rejects_nulls()
    {
        var glossary = new Glossary(1, new Dictionary<string, BoundedContext>(), []);

        Assert.Throws<ArgumentNullException>(() => new MergeResult(null!, [], []));
        Assert.Throws<ArgumentNullException>(() => new MergeResult(glossary, null!, []));
        Assert.Throws<ArgumentNullException>(() => new MergeResult(glossary, [], null!));
    }
}
