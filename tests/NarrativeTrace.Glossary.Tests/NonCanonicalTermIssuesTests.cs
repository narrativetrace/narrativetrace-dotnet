// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class NonCanonicalTermIssuesTests
{
    [Fact]
    public void Maps_violation_to_medium_issue_with_category_and_site()
    {
        var issues = NonCanonicalTermIssues.From(
        [
            new VocabularyViolation(
                "billing", "account with overdraft", "overdraft account",
                "AccountService.openAccountWithOverdraft", "openAccountWithOverdraft",
                "openOverdraftAccount", 1),
        ]);

        var issue = Assert.Single(issues);
        Assert.Equal(ClaritySeverity.Medium, issue.Severity);
        Assert.Equal("non-canonical-term", issue.Category);
        Assert.Equal(
            "billing.AccountService.openAccountWithOverdraft", issue.Element);
        Assert.Equal(
            "use canonical term 'overdraft account' → rename to openOverdraftAccount",
            issue.Suggestion);
    }

    [Fact]
    public void Occurrences_carry_into_the_issue_impact_score()
    {
        var issues = NonCanonicalTermIssues.From(
        [
            new VocabularyViolation(
                "billing", "account with overdraft", "overdraft account",
                "A.legacyName", "legacyName", null, 5),
        ]);

        var issue = Assert.Single(issues);
        Assert.Equal(5, issue.Occurrences);
        Assert.Equal(ClaritySeverity.Medium.Weight() * 5.0, issue.ImpactScore);
        Assert.Equal("use canonical term 'overdraft account'", issue.Suggestion);
    }

    [Fact]
    public void Preserves_order_and_rejects_null()
    {
        Assert.Empty(NonCanonicalTermIssues.From([]));
        Assert.Throws<ArgumentNullException>(() => NonCanonicalTermIssues.From(null!));
    }
}
