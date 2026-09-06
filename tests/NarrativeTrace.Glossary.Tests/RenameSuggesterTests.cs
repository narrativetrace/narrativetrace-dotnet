// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class RenameSuggesterTests
{
    private const string Alias = "account with overdraft";
    private const string Canonical = "overdraft account";

    [Theory]
    [InlineData("openAccountWithOverdraft", "openOverdraftAccount")]
    [InlineData("AccountWithOverdraftService", "OverdraftAccountService")]
    [InlineData("open_account_with_overdraft", "open_overdraft_account")]
    [InlineData("openAccountsWithOverdraft", "openOverdraftAccount")]
    [InlineData("accountWithOverdraft", "overdraftAccount")]
    [InlineData("accountWithOverdraftXMLExport", "overdraftAccountXMLExport")]
    [InlineData("_account_with_overdraft", "overdraft_account")]
    public void Splices_canonical_term_preserving_casing_convention(
        string identifier, string expected)
    {
        Assert.Equal(expected, RenameSuggester.Suggest(identifier, Alias, Canonical));
    }

    [Theory]
    [InlineData("chargeCard")]
    [InlineData("accountForOverdraft")]
    public void Returns_null_when_alias_tokens_do_not_appear_contiguously(string identifier)
    {
        Assert.Null(RenameSuggester.Suggest(identifier, Alias, Canonical));
    }

    [Theory]
    [InlineData(null, "a", "b")]
    [InlineData(" ", "a", "b")]
    [InlineData("x", null, "b")]
    [InlineData("x", " ", "b")]
    [InlineData("x", "a", null)]
    [InlineData("x", "a", " ")]
    public void Rejects_blank_arguments(string? identifier, string? alias, string? canonical)
    {
        Assert.Throws<ArgumentException>(
            () => RenameSuggester.Suggest(identifier!, alias!, canonical!));
    }
}
