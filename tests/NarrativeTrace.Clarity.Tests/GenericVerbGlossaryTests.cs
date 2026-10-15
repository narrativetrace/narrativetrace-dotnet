// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Clarity.Tests;

#pragma warning disable S1144 // sample members are reachable only via reflection under scan

/// <summary>
/// The checkout scenario's <c>OrderProcessor.Execute(order)</c>: the scan flags the generic verb, and a
/// project glossary that lists a verb for the noun turns the flag into a proposal for that verb.
/// </summary>
public sealed class GenericVerbGlossaryTests
{
    [Fact]
    public void A_generic_verb_method_is_flagged()
    {
        var issues = Scan(vocabulary: null);

        Assert.Contains(issues, issue =>
            issue.Category == "method-name" && issue.Element == "OrderProcessor.Execute");
    }

    [Fact(Skip = "Parity check 2026-10-11, capability C12 (clarity suggestions tier 1, not yet built): the method-name suggestion is a fixed string and never names the glossary verb")]
    public void A_glossary_verb_for_the_noun_is_proposed_in_place_of_the_generic_one()
    {
        var glossary = DomainVocabulary.Of(["ship"], ["order"]);

        var issues = Scan(glossary);

        var flagged = Assert.Single(issues, issue => issue.Element == "OrderProcessor.Execute");
        Assert.Contains("ship", flagged.Suggestion, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ClarityIssue> Scan(DomainVocabulary? vocabulary)
    {
        var results = ClarityScanner.Scan([typeof(OrderProcessor)], vocabulary);
        return results[nameof(OrderProcessor)].Issues;
    }

    private sealed record Order(string Id);

    private sealed class OrderProcessor
    {
        private readonly List<Order> handled = [];

        public void Execute(Order order) => this.handled.Add(order);
    }
}
