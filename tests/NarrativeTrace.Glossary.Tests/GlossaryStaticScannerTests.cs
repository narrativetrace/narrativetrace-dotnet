// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public class GlossaryStaticScannerTests
{
    [Fact]
    public void Builds_one_node_per_public_method_with_class_and_parameter_names()
    {
        var nodes = ScanNodes(typeof(OverdraftService));

        var node = Assert.Single(nodes, n => n.Signature.MethodName == "OpenAccount");
        Assert.Equal("OverdraftService", node.Signature.ClassName);
        Assert.Equal(
            ["customerId", "amount"],
            node.Signature.Parameters.Select(p => p.Name));
    }

    [Fact]
    public void Captures_the_raw_narration_template_with_placeholders_intact()
    {
        var node = Node(typeof(OverdraftService), "OpenAccount");

        Assert.Equal("Opening overdraft for {customerId}", node.Signature.Narration);
    }

    [Fact]
    public void Captures_the_error_template()
    {
        var node = Node(typeof(OverdraftService), "OpenAccount");

        Assert.Equal("Overdraft refused for {customerId}", node.Signature.ErrorContext);
    }

    [Fact]
    public void Method_without_annotations_carries_no_templates()
    {
        var node = Node(typeof(OverdraftService), "Close");

        Assert.Null(node.Signature.Narration);
        Assert.Null(node.Signature.ErrorContext);
    }

    [Fact]
    public void Every_repeated_error_template_is_scanned_exactly_once()
    {
        var nodes = ScanNodes(typeof(PaymentService))
            .Where(n => n.Signature.MethodName == "Charge")
            .ToList();

        Assert.Equal(
            ["Card declined for {customerId}", "Gateway unreachable for {customerId}"],
            nodes.Select(n => n.Signature.ErrorContext));
    }

    [Fact]
    public void Repeated_error_templates_do_not_repeat_the_narration()
    {
        var nodes = ScanNodes(typeof(PaymentService))
            .Where(n => n.Signature.MethodName == "Charge")
            .ToList();

        Assert.Equal(
            ["Charging {customerId}", null],
            nodes.Select(n => n.Signature.Narration));
    }

    [Fact]
    public void Skips_non_public_methods()
    {
        var names = ScanNodes(typeof(OverdraftService))
            .Select(n => n.Signature.MethodName);

        Assert.DoesNotContain("Audit", names);
    }

    [Fact]
    public void Skips_property_accessors()
    {
        var names = ScanNodes(typeof(OverdraftService))
            .Select(n => n.Signature.MethodName)
            .ToList();

        Assert.DoesNotContain("get_Limit", names);
        Assert.DoesNotContain("set_Limit", names);
    }

    [Fact]
    public void Skips_object_methods_even_when_overridden()
    {
        var names = ScanNodes(typeof(OverdraftService))
            .Select(n => n.Signature.MethodName);

        Assert.DoesNotContain("ToString", names);
        Assert.DoesNotContain("GetHashCode", names);
    }

    [Fact]
    public void Keeps_a_typed_equals_overload_which_is_not_an_object_method()
    {
        var names = ScanNodes(typeof(OverdraftService))
            .Select(n => n.Signature.MethodName);

        Assert.Contains("Equals", names);
    }

    [Fact]
    public void Type_contributing_no_methods_contributes_no_tree()
    {
        var trees = GlossaryStaticScanner.Scan([typeof(NoPublicMethods)]);

        Assert.Empty(trees);
    }

    [Fact]
    public void Builds_one_node_per_property_named_for_the_property()
    {
        var node = Node(typeof(OverdraftService), "Limit");

        Assert.Equal("OverdraftService", node.Signature.ClassName);
        Assert.Empty(node.Signature.Parameters);
        Assert.Null(node.Signature.Narration);
        Assert.Null(node.Signature.ErrorContext);
    }

    /// <summary>
    /// A record's positional properties are the vocabulary its Java
    /// counterpart declares as accessor methods; without them the whole
    /// data-shape half of a domain language never reaches the glossary.
    /// </summary>
    [Fact]
    public void Record_whose_only_authored_members_are_properties_contributes_a_tree()
    {
        var names = ScanNodes(typeof(OrderResult))
            .Select(n => n.Signature.MethodName)
            .ToList();

        Assert.Contains("OrderId", names);
        Assert.Contains("TotalCharged", names);
    }

    [Fact]
    public void Read_write_property_is_observed_once_not_once_per_accessor()
    {
        var nodes = ScanNodes(typeof(OverdraftService))
            .Where(n => n.Signature.MethodName == "Limit");

        Assert.Single(nodes);
    }

    /// <summary>An indexer's member name is the language's, not the author's.</summary>
    [Fact]
    public void Skips_an_indexer()
    {
        var names = ScanNodes(typeof(RoomBoard))
            .Select(n => n.Signature.MethodName);

        Assert.DoesNotContain("Item", names);
    }

    [Fact]
    public void A_property_harvests_the_phrase_its_java_accessor_method_would()
    {
        var result = Harvester().HarvestStatic(
            GlossaryStaticScanner.Scan([typeof(OrderResult)]));

        Assert.Contains(
            result.Candidates,
            c => c.Phrase == "total charged" && c.Site == "OrderResult.TotalCharged");
        Assert.Contains(
            result.Candidates,
            c => c.Phrase == "order id" && c.Site == "OrderResult.OrderId");
    }

    [Fact]
    public void Scans_each_type_into_its_own_tree()
    {
        var trees = GlossaryStaticScanner.Scan(
            [typeof(OverdraftService), typeof(PaymentService)]);

        Assert.Equal(2, trees.Count);
    }

    [Fact]
    public void Rejects_null_types()
    {
        Assert.Throws<ArgumentNullException>(() => GlossaryStaticScanner.Scan(null!));
    }

    [Fact]
    public void Scanned_trees_harvest_templates_and_identifiers_together()
    {
        var result = Harvester().HarvestStatic(
            GlossaryStaticScanner.Scan([typeof(OverdraftService)]));

        Assert.Contains(
            result.Candidates,
            c => c.Kind == TermKind.Template
                && c.Phrase == "Opening overdraft for {customerId}"
                && c.Site == "OverdraftService.OpenAccount");
        Assert.Contains(
            result.Candidates,
            c => c.Kind == TermKind.VerbPhrase && c.Phrase == "open account");
    }

    private static GlossaryHarvester Harvester()
    {
        return new GlossaryHarvester(
            new ContextResolver(new Glossary(
                1,
                new Dictionary<string, BoundedContext>
                {
                    ["billing"] = new("billing", ["NarrativeTrace.Glossary.Tests"]),
                },
                [])),
            _ => typeof(OverdraftService).Namespace);
    }

    private static TraceNode Node(Type type, string methodName)
    {
        return Assert.Single(
            ScanNodes(type), n => n.Signature.MethodName == methodName);
    }

    private static IReadOnlyList<TraceNode> ScanNodes(Type type)
    {
        var trees = GlossaryStaticScanner.Scan([type]);
        return Assert.Single(trees).Roots;
    }

    /// <summary>
    /// Scan fixture. Members are public because the scanner reads them by
    /// reflection, and the non-public <c>Audit</c> is what the visibility
    /// filter must skip.
    /// </summary>
    public sealed class OverdraftService : IEquatable<OverdraftService>
    {
        public int Limit { get; set; }

        [Narrated("Opening overdraft for {customerId}")]
        [OnError("Overdraft refused for {customerId}")]
        public void OpenAccount(string customerId, decimal amount)
        {
            Limit += (int)amount;
            Audit(customerId);
        }

        public void Close(string customerId)
        {
            Limit = 0;
            Audit(customerId);
        }

        public bool Equals(OverdraftService? other)
        {
            return ReferenceEquals(this, other);
        }

        public override bool Equals(object? obj)
        {
            return Equals(obj as OverdraftService);
        }

        public override int GetHashCode()
        {
            return Limit;
        }

        public override string ToString()
        {
            return nameof(OverdraftService);
        }

        private void Audit(string customerId)
        {
            Limit -= customerId.Length;
        }
    }

    /// <summary>
    /// The C# shape of a Java record: the authored vocabulary lives entirely
    /// in properties, and every member the compiler synthesizes beside them
    /// is marked compiler-generated.
    /// </summary>
    public sealed record OrderResult(string OrderId, decimal TotalCharged);

    /// <summary>Indexer fixture: <c>this[int]</c> is declared as a property named <c>Item</c>.</summary>
    public sealed class RoomBoard
    {
        private readonly string[] rooms = ["101", "102"];

        public int Occupancy => rooms.Length;

        public string this[int index] => rooms[index];
    }

    public sealed class PaymentService
    {
        [Narrated("Charging {customerId}")]
        [OnError("Card declined for {customerId}")]
        [OnError("Gateway unreachable for {customerId}")]
        public string Charge(string customerId)
        {
            return customerId;
        }
    }

    /// <summary>
    /// A type whose only members are a constructor and a private helper —
    /// nothing for the scanner to contribute.
    /// </summary>
    public sealed class NoPublicMethods
    {
        private readonly string name;

        public NoPublicMethods()
        {
            name = Hidden();
        }

        private static string Hidden()
        {
            return nameof(NoPublicMethods);
        }

        public override string ToString()
        {
            return name;
        }
    }
}
