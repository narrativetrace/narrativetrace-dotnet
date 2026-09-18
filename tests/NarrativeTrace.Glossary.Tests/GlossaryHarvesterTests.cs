// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryHarvesterTests
{
    private static readonly GlossaryHarvester Harvester = new(
        new ContextResolver(new Glossary(
            1,
            new Dictionary<string, BoundedContext>
            {
                ["billing"] = new("billing", ["Acme.Billing"]),
            },
            [])),
        className => className.StartsWith("Overdraft", StringComparison.Ordinal)
            ? "Acme.Billing"
            : "Other");

    [Fact]
    public void Harvests_method_parameter_and_class_candidates_with_context_and_site()
    {
        var result = Harvester.Harvest(
            [Tree(Node("OverdraftService", "openAccountWithOverdraft", "overdraftAccountId"))]);

        Assert.Equal(
            [
                new HarvestCandidate(
                    "billing", "account with overdraft", TermKind.NounPhrase,
                    "OverdraftService.openAccountWithOverdraft", "openAccountWithOverdraft", 1),
                new HarvestCandidate(
                    "billing", "open account with overdraft", TermKind.VerbPhrase,
                    "OverdraftService.openAccountWithOverdraft", "openAccountWithOverdraft", 1),
                new HarvestCandidate(
                    "billing", "overdraft", TermKind.Word,
                    "OverdraftService", "OverdraftService", 1),
                new HarvestCandidate(
                    "billing", "overdraft account", TermKind.NounPhrase,
                    "OverdraftService.openAccountWithOverdraft", "overdraftAccountId", 1),
            ],
            result.Candidates);
    }

    [Fact]
    public void Harvests_exception_type_from_failed_node()
    {
        var failed = new TraceNode(
            new MethodSignature("OverdraftService", "charge", []),
            new Threw(new InvalidOperationException("boom")),
            [],
            0);

        var result = Harvester.Harvest([Tree(failed)]);

        Assert.Contains(
            new HarvestCandidate(
                "billing", "invalid operation", TermKind.NounPhrase,
                "OverdraftService.charge", "InvalidOperationException", 1),
            result.Candidates);
    }

    [Fact]
    public void Node_that_threw_null_exception_harvests_no_exception_candidate()
    {
        var failed = new TraceNode(
            new MethodSignature("OverdraftService", "charge", []),
            new Threw(null),
            [],
            0);

        var result = Harvester.Harvest([Tree(failed)]);

        Assert.DoesNotContain(
            result.Candidates,
            candidate => candidate.Identifier.EndsWith("Exception", StringComparison.Ordinal));
    }

    [Fact]
    public void Walks_nested_children_and_aggregates_repeated_observations()
    {
        var leaf = Node("OverdraftService", "charge");
        var parent = new TraceNode(
            new MethodSignature("OverdraftService", "charge", []),
            new Incomplete(),
            [leaf],
            0);

        var result = Harvester.Harvest([Tree(parent)]);

        Assert.Contains(
            new HarvestCandidate(
                "billing", "charge", TermKind.VerbPhrase,
                "OverdraftService.charge", "charge", 2),
            result.Candidates);
    }

    [Fact]
    public void Skips_synthetic_non_identifier_names()
    {
        Assert.Empty(Harvester.Harvest([Tree(Node("<launcher>", "<fork>"))]).Candidates);
        Assert.Empty(Harvester.Harvest([Tree(Node("Foo-Bar", "bad-name"))]).Candidates);
    }

    [Fact]
    public void Treats_null_namespace_lookup_as_unassigned()
    {
        var nullNamespaces = new GlossaryHarvester(
            new ContextResolver(
                new Glossary(1, new Dictionary<string, BoundedContext>(), [])),
            _ => null);

        var result = nullNamespaces.Harvest([Tree(Node("TicketDesk", "escalate"))]);

        Assert.NotEmpty(result.Candidates);
        Assert.All(
            result.Candidates,
            candidate => Assert.Equal(ContextResolver.Unassigned, candidate.Context));
    }

    [Fact]
    public void Resolves_unknown_namespaces_to_unassigned()
    {
        var result = Harvester.Harvest([Tree(Node("TicketDesk", "escalate"))]);

        Assert.NotEmpty(result.Candidates);
        Assert.All(
            result.Candidates,
            candidate => Assert.Equal(ContextResolver.Unassigned, candidate.Context));
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        var resolver = new ContextResolver(
            new Glossary(1, new Dictionary<string, BoundedContext>(), []));

        Assert.Throws<ArgumentNullException>(() => Harvester.Harvest(null!));
        Assert.Throws<ArgumentNullException>(() => new GlossaryHarvester(null!, _ => ""));
        Assert.Throws<ArgumentNullException>(() => new GlossaryHarvester(resolver, null!));
    }

    [Fact]
    public void Static_harvest_records_raw_narration_and_error_templates_verbatim()
    {
        var result = Harvester.HarvestStatic([Tree(Templated(
            "Opening overdraft for {customerId}",
            "Overdraft refused for {customerId}"))]);

        Assert.Equal(
            [
                new HarvestCandidate(
                    "billing", "Opening overdraft for {customerId}", TermKind.Template,
                    "OverdraftService.charge", "Opening overdraft for {customerId}", 1),
                new HarvestCandidate(
                    "billing", "Overdraft refused for {customerId}", TermKind.Template,
                    "OverdraftService.charge", "Overdraft refused for {customerId}", 1),
            ],
            result.Candidates.Where(c => c.Kind == TermKind.Template));
    }

    [Fact]
    public void Trace_harvest_never_records_templates_because_values_are_interpolated()
    {
        var interpolated = Tree(Templated("Opening overdraft for 42", null));

        var result = Harvester.Harvest([interpolated]);

        Assert.DoesNotContain(result.Candidates, c => c.Kind == TermKind.Template);
    }

    [Fact]
    public void Static_harvest_skips_absent_and_blank_templates()
    {
        var result = Harvester.HarvestStatic([Tree(Templated(null, "   "))]);

        Assert.DoesNotContain(result.Candidates, c => c.Kind == TermKind.Template);
    }

    [Fact]
    public void Static_harvest_still_records_the_identifier_candidates()
    {
        var result = Harvester.HarvestStatic([Tree(Templated("Charging {id}", null))]);

        Assert.Contains(
            new HarvestCandidate(
                "billing", "charge", TermKind.VerbPhrase,
                "OverdraftService.charge", "charge", 1),
            result.Candidates);
    }

    [Fact]
    public void Static_harvest_rejects_null_trees()
    {
        Assert.Throws<ArgumentNullException>(() => Harvester.HarvestStatic(null!));
    }

    private static TraceNode Templated(string? narration, string? errorContext)
    {
        return new TraceNode(
            new MethodSignature(
                "OverdraftService", "charge", [], narration, errorContext),
            new Incomplete(),
            [],
            0);
    }

    /// <summary>
    /// A java security fuzz suite finding, mirrored here: "is a legal identifier" is not the
    /// same question as "has a word in it" — <c>__</c> answers yes to the first and no to the
    /// second, and bytecode/IL is full of names shaped like that.
    /// <see cref="TermNormalizer.MethodCandidates"/> throws (a declared guard, not a crash) on a
    /// method name with no readable word, and used to reach that guard for every one of these
    /// through the harvester before <c>IsIdentifier</c> also required a letter or digit.
    /// </summary>
    [Fact]
    public void A_name_with_no_readable_word_is_skipped_rather_than_harvested_or_thrown_over()
    {
        var trees = new List<TraceTree> { Tree(Node("__", "___", "_")) };

        var result = Harvester.Harvest(trees);

        Assert.Empty(result.Candidates);
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Harvest must terminate rather than recurse the call
    // stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Harvest_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var result = Harvester.Harvest([Tree(a)]);

        Assert.NotEmpty(result.Candidates);
    }

    // --- Harvest and translation must resolve a node's bounded context by the same rule. A term
    // filed under one context and looked up under another produces a gap no curation can close. ---

    private static readonly Glossary ShopAndBilling = new(
        1,
        new Dictionary<string, BoundedContext>
        {
            ["shop"] = new("shop", ["Acme.Shop"]),
            ["billing"] = new("billing", ["Acme.Billing"]),
            ["_unassigned"] = new("_unassigned", []),
        },
        []);

    private static TraceNode OrderNode(string? capturedNamespace)
    {
        return new TraceNode(
            new MethodSignature("OrderService", "placeOrder", [], Namespace: capturedNamespace),
            new Incomplete(),
            [],
            0);
    }

    private static CanonicalEntry OrderEntry(string? capturedNamespace)
    {
        return new CanonicalEntry(
            Timestamp: "2026-08-14T10:00:00.000Z",
            Level: "trace",
            Message: "enter",
            Service: null,
            Environment: null,
            TraceId: "0123456789abcdef0123456789abcdef",
            SpanId: "0000000000000001",
            ParentSpanId: null,
            CodeNamespace: "OrderService",
            CodeFunction: "placeOrder",
            NtEventType: "method_enter",
            NtPackage: capturedNamespace);
    }

    private static Glossary WithPlaceOrderCuratedIn(string context)
    {
        return new Glossary(
            ShopAndBilling.SchemaVersion,
            ShopAndBilling.Contexts,
            [
                new GlossaryTerm(
                    "place order",
                    context,
                    TermKind.VerbPhrase,
                    TermStatus.Curated,
                    null,
                    new Dictionary<string, string> { ["es"] = "realizar pedido" },
                    [],
                    [],
                    new DateTime(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc)),
            ]);
    }

    /// <summary>
    /// Harvests one node, curates its verb phrase in exactly the context the harvest filed it
    /// under, then renders the same call: the translation must land. It only can when both halves
    /// resolved the context the same way.
    /// </summary>
    private static void AssertHarvestAndTranslationAgree(
        Func<string, string?> namespaceOf, string? capturedNamespace)
    {
        var harvested = new GlossaryHarvester(new ContextResolver(ShopAndBilling), namespaceOf)
            .Harvest([Tree(OrderNode(capturedNamespace))]);
        var context = harvested.Candidates
            .Where(candidate => candidate.Phrase == "place order")
            .Select(candidate => candidate.Context)
            .First();

        var view = new TraceTranslationView(WithPlaceOrderCuratedIn(context), namespaceOf);

        Assert.Contains(
            "realizar pedido (placeOrder)",
            view.Render([OrderEntry(capturedNamespace)], "es"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Harvest_and_translation_agree_when_the_index_resolves_the_simple_name()
    {
        AssertHarvestAndTranslationAgree(_ => "Acme.Shop", "Acme.Shop");
    }

    [Fact]
    public void Harvest_and_translation_agree_when_the_class_ships_in_an_assembly_the_index_never_scanned()
    {
        AssertHarvestAndTranslationAgree(_ => null, "Acme.Shop");
    }

    [Fact]
    public void Harvest_and_translation_agree_when_the_simple_name_is_ambiguous_in_the_index()
    {
        // Two classes share the simple name, so the index answers for the wrong one (or, as the
        // real index does, not at all). The namespace captured at the site outranks it either way.
        AssertHarvestAndTranslationAgree(_ => "Acme.Billing", "Acme.Shop");
    }

    [Fact]
    public void A_pre_1_2_signature_with_no_captured_namespace_still_falls_back_to_the_index()
    {
        AssertHarvestAndTranslationAgree(_ => "Acme.Shop", null);
    }

    private static TraceTree Tree(params TraceNode[] roots)
    {
        return new TraceTree(roots);
    }

    private static TraceNode Node(
        string className, string methodName, params string[] parameterNames)
    {
        var parameters = parameterNames
            .Select(name => new ParameterCapture(name, "\"v\"", false))
            .ToArray();
        return new TraceNode(
            new MethodSignature(className, methodName, parameters),
            new Incomplete(),
            [],
            0);
    }
}
