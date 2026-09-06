// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Intra-trace value deltas: when the same entity reappears in one trace
/// slightly changed, the later emission renders as a field-level diff against
/// the in-document reference instead of a second full blob, so the one changed
/// field is the thing the reader sees.
/// </summary>
public class MarkdownValueDeltaTests
{
    private const string DinnerUsd =
        "Expense(Description: \"Dinner\", Amount: 100.00, Currency: \"USD\")";

    private const string DinnerEur =
        "Expense(Description: \"Dinner\", Amount: 92.0000, Currency: \"EUR\")";

    private const string DeltaToEur =
        "{Amount: 100→92, Currency: \"USD\"→\"EUR\"}";

    private static RenderedValue.ObjectVal Object(
        string typeName, params (string Name, RenderedValue Value)[] fields)
    {
        var map = new Dictionary<string, RenderedValue>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            map[field.Name] = field.Value;
        }

        return new RenderedValue.ObjectVal(typeName, map);
    }

    private static RenderedValue Text(string value)
    {
        return new RenderedValue.StringVal(value);
    }

    private static RenderedValue Number(double value)
    {
        return new RenderedValue.DoubleVal(value);
    }

    private static RenderedValue.ObjectVal Expense(
        string description, double amount, string currency)
    {
        return Object(
            "Expense",
            ("Description", Text(description)),
            ("Amount", Number(amount)),
            ("Currency", Text(currency)));
    }

    private static TraceNode Leaf(
        string className, string method, string value, RenderedValue? held)
    {
        return new TraceNode(
            new MethodSignature(
                className, method,
                [new ParameterCapture("expense", value, false, held)]),
            new Returned("\"ok\""),
            [],
            TimeSpan.TicksPerMillisecond);
    }

    private static TraceNode Returning(
        string className, string method, string value, RenderedValue? held)
    {
        return new TraceNode(
            new MethodSignature(className, method, []),
            new Returned(value, held),
            [],
            TimeSpan.TicksPerMillisecond);
    }

    private static string Render(params TraceNode[] nodes)
    {
        return MarkdownRenderer.Render(new TraceTree(nodes));
    }

    [Fact]
    public void Changed_scalar_fields_render_as_a_delta_against_the_reference()
    {
        var result = Render(
            Leaf("TripLedger", "RecordExpense", DinnerUsd,
                Expense("Dinner", 100.00, "USD")),
            Leaf("ShareCalculator", "Split", DinnerEur,
                Expense("Dinner", 92.0, "EUR")));

        Assert.Contains(
            "RecordExpense**(expense: `‹Dinner›=" + DinnerUsd + "`)", result);
        Assert.Contains(
            "Split**(expense: `‹Dinner›′" + DeltaToEur + "`)", result);
    }

    [Fact]
    public void Repeated_changed_variant_is_defined_as_a_delta_of_the_reference()
    {
        var result = Render(
            Leaf("TripLedger", "RecordExpense", DinnerUsd,
                Expense("Dinner", 100.00, "USD")),
            Leaf("ShareCalculator", "Split", DinnerEur,
                Expense("Dinner", 92.0, "EUR")),
            Leaf("AuditLog", "Append", DinnerEur,
                Expense("Dinner", 92.0, "EUR")));

        Assert.Contains(
            "RecordExpense**(expense: `‹Dinner›=" + DinnerUsd + "`)", result);
        Assert.Contains(
            "Split**(expense: `‹Dinner·2›=‹Dinner›′" + DeltaToEur + "`)",
            result);
        Assert.Contains("Append**(expense: `‹Dinner·2›`)", result);
    }

    [Fact]
    public void Every_later_variant_diffs_against_the_same_reference()
    {
        const string DinnerGbp =
            "Expense(Description: \"Dinner\", Amount: 79.0000, Currency: \"GBP\")";

        var result = Render(
            Leaf("TripLedger", "RecordExpense", DinnerUsd,
                Expense("Dinner", 100.00, "USD")),
            Leaf("ShareCalculator", "Split", DinnerEur,
                Expense("Dinner", 92.0, "EUR")),
            Leaf("Reporter", "Report", DinnerGbp,
                Expense("Dinner", 79.0, "GBP")));

        Assert.Contains(
            "Split**(expense: `‹Dinner›′" + DeltaToEur + "`)", result);
        Assert.Contains(
            "Report**(expense: `‹Dinner›′"
            + "{Amount: 100→79, Currency: \"USD\"→\"GBP\"}`)",
            result);
    }

    [Fact]
    public void Delta_renders_on_a_return_value_too()
    {
        var result = Render(
            Returning("TripLedger", "RecordExpense", DinnerUsd,
                Expense("Dinner", 100.00, "USD")),
            Returning("ShareCalculator", "Normalize", DinnerEur,
                Expense("Dinner", 92.0, "EUR")));

        Assert.Contains(
            "RecordExpense**() → `‹Dinner›=" + DinnerUsd + "`", result);
        Assert.Contains(
            "Normalize**() → `‹Dinner›′" + DeltaToEur + "`", result);
    }

    [Fact]
    public void An_unchanged_nested_field_does_not_suppress_the_delta()
    {
        // .NET-specific: record equality on ObjectVal/ListVal compares their
        // dictionary and list members by reference, so without ValueDelta's
        // structural walk this identical nested value would read as "changed"
        // and kill the whole diff. Java gets it free from Map/List equality.
        var before = "Expense(Description: \"Dinner\", Amount: 100.00, "
            + "Split: Share(Payer: \"Alice\"))";
        var after = "Expense(Description: \"Dinner\", Amount: 92.0000, "
            + "Split: Share(Payer: \"Alice\"))";

        var result = Render(
            Leaf("TripLedger", "RecordExpense", before, WithShare(100.00)),
            Leaf("ShareCalculator", "Split", after, WithShare(92.0)));

        Assert.Contains(
            "Split**(expense: `‹Dinner›′{Amount: 100→92}`)", result);
    }

    private static RenderedValue.ObjectVal WithShare(double amount)
    {
        return Object(
            "Expense",
            ("Description", Text("Dinner")),
            ("Amount", Number(amount)),
            ("Split", Object("Share", ("Payer", Text("Alice")))));
    }

    [Fact]
    public void A_changed_nested_field_falls_back_to_the_full_render()
    {
        var before = "Trip(Name: \"Rome week\", Expenses: [Expense(Amount: 10)])";
        var after = "Trip(Name: \"Rome week\", Expenses: [Expense(Amount: 20)])";

        var result = Render(
            Leaf("Planner", "Plan", before, Trip(10.0)),
            Leaf("Planner", "Replan", after, Trip(20.0)));

        Assert.Contains("Plan**(expense: `" + before + "`)", result);
        Assert.Contains("Replan**(expense: `" + after + "`)", result);
        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    private static RenderedValue.ObjectVal Trip(double amount)
    {
        return Object(
            "Trip",
            ("Name", Text("Rome week")),
            ("Expenses", new RenderedValue.ListVal(
                [Object("Expense", ("Amount", Number(amount)))])));
    }

    [Fact]
    public void A_different_field_set_falls_back_to_the_full_render()
    {
        var before = "Order(Id: \"order-77\", Total: 10, Currency: \"EUR\")";
        var after = "Order(Id: \"order-77\", Total: 10, Coupon: \"SUMMER\")";
        var priced = Object(
            "Order",
            ("Id", Text("order-77")),
            ("Total", Number(10.0)),
            ("Currency", Text("EUR")));
        var couponed = Object(
            "Order",
            ("Id", Text("order-77")),
            ("Total", Number(10.0)),
            ("Coupon", Text("SUMMER")));

        var result = Render(
            Leaf("Checkout", "Price", before, priced),
            Leaf("Checkout", "Apply", after, couponed));

        Assert.Contains("Price**(expense: `" + before + "`)", result);
        Assert.Contains("Apply**(expense: `" + after + "`)", result);
        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_identity_on_a_different_type_is_not_the_same_entity()
    {
        var refund =
            "Refund(Description: \"Dinner\", Amount: 100.00, Currency: \"USD\")";
        var refundValue = Object(
            "Refund",
            ("Description", Text("Dinner")),
            ("Amount", Number(100.0)),
            ("Currency", Text("USD")));

        var result = Render(
            Leaf("TripLedger", "RecordExpense", DinnerUsd,
                Expense("Dinner", 100.00, "USD")),
            Leaf("TripLedger", "RecordRefund", refund, refundValue));

        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_value_without_an_identity_field_is_never_a_delta()
    {
        var before = "Expense(Amount: 100.00, Currency: \"USD\", Category: \"FOOD\")";
        var after = "Expense(Amount: 92.0000, Currency: \"EUR\", Category: \"FOOD\")";

        var result = Render(
            Leaf("TripLedger", "RecordExpense", before, Anonymous(100.0, "USD")),
            Leaf("ShareCalculator", "Split", after, Anonymous(92.0, "EUR")));

        Assert.Contains("RecordExpense**(expense: `" + before + "`)", result);
        Assert.Contains("Split**(expense: `" + after + "`)", result);
        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    private static RenderedValue.ObjectVal Anonymous(
        double amount, string currency)
    {
        return Object(
            "Expense",
            ("Amount", Number(amount)),
            ("Currency", Text(currency)),
            ("Category", Text("FOOD")));
    }

    [Fact]
    public void A_redacted_identity_field_never_anchors_a_delta()
    {
        var before =
            "Expense(Description: [REDACTED], Amount: 100.00, Currency: \"USD\")";
        var after =
            "Expense(Description: [REDACTED], Amount: 92.0000, Currency: \"EUR\")";

        var result = Render(
            Leaf("TripLedger", "RecordExpense", before,
                Expense(RedactionPolicy.Marker, 100.0, "USD")),
            Leaf("ShareCalculator", "Split", after,
                Expense(RedactionPolicy.Marker, 92.0, "EUR")));

        Assert.Contains("Split**(expense: `" + after + "`)", result);
        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Identical_structured_forms_with_different_bytes_render_in_full()
    {
        var before =
            "Expense(Description: \"Dinner\", Amount: 100.00, Currency: \"USD\")";
        var after =
            "Expense(Description: \"Dinner\", Amount: 100.0, Currency: \"USD\")";
        var same = Expense("Dinner", 100.0, "USD");

        var result = Render(
            Leaf("TripLedger", "RecordExpense", before, same),
            Leaf("ShareCalculator", "Split", after, same));

        Assert.Contains("RecordExpense**(expense: `" + before + "`)", result);
        Assert.Contains("Split**(expense: `" + after + "`)", result);
        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Values_below_the_reference_length_never_become_deltas()
    {
        var before = "Fee(Name: \"Bank\", Amount: 1)";
        var after = "Fee(Name: \"Bank\", Amount: 2)";
        var fee1 = Object("Fee", ("Name", Text("Bank")), ("Amount", Number(1.0)));
        var fee2 = Object("Fee", ("Name", Text("Bank")), ("Amount", Number(2.0)));

        var result = Render(
            Leaf("Bank", "Charge", before, fee1),
            Leaf("Bank", "Refund", after, fee2));

        Assert.DoesNotContain("′", result, StringComparison.Ordinal);
        Assert.DoesNotContain("‹", result, StringComparison.Ordinal);
    }

    [Fact]
    public void A_contained_reference_and_its_delta_share_one_label()
    {
        var listOfOne = "[" + DinnerUsd + "]";

        var result = Render(
            Leaf("TripLedger", "RecordExpense", DinnerUsd,
                Expense("Dinner", 100.00, "USD")),
            Returning("TripLedger", "ExpensesOf", listOfOne, null),
            Leaf("ShareCalculator", "Split", DinnerEur,
                Expense("Dinner", 92.0, "EUR")));

        Assert.Contains(
            "RecordExpense**(expense: `‹Dinner›=" + DinnerUsd + "`)", result);
        Assert.Contains("ExpensesOf**() → `[‹Dinner›]`", result);
        Assert.Contains(
            "Split**(expense: `‹Dinner›′" + DeltaToEur + "`)", result);
    }

    [Fact]
    public void Every_scalar_kind_formats_the_way_the_flat_renderer_prints_it()
    {
        var before = "Order(Id: \"order-77\", Count: 1, Active: true, "
            + "Note: null, At: 2020-01-01)";
        var after = "Order(Id: \"order-77\", Count: 2, Active: false, "
            + "Note: \"rush\", At: 2020-01-02)";

        var result = Render(
            Leaf("Warehouse", "Receive", before,
                Order(1, true, new RenderedValue.NullVal(), 1_577_836_800_000L)),
            Leaf("Warehouse", "Ship", after,
                Order(2, false, Text("rush"), 1_577_923_200_000L)));

        Assert.Contains(
            "Ship**(expense: `‹order-77›′{Count: 1→2, Active: true→false, "
            + "Note: null→\"rush\", "
            + "At: 2020-01-01T00:00:00.0000000Z→2020-01-02T00:00:00.0000000Z}`)",
            result);
    }

    private static RenderedValue.ObjectVal Order(
        long count, bool active, RenderedValue note, long millis)
    {
        return Object(
            "Order",
            ("Id", Text("order-77")),
            ("Count", new RenderedValue.LongVal(count)),
            ("Active", new RenderedValue.BooleanVal(active)),
            ("Note", note),
            ("At", new RenderedValue.InstantVal(millis)));
    }

    [Fact]
    public void An_identity_field_is_matched_whatever_its_casing()
    {
        // .NET property names are PascalCase; the cross-port identity ladder is
        // lower-case. The delta must group on the same ladder the label uses.
        var before = "Note(TITLE: \"Standup\", Body: \"the first standup note\")";
        var after = "Note(TITLE: \"Standup\", Body: \"the second standup note\")";

        var result = Render(
            Leaf("Journal", "Write", before, Note("TITLE", "the first standup note")),
            Leaf("Journal", "Amend", after, Note("TITLE", "the second standup note")));

        Assert.Contains(
            "Amend**(expense: `‹Standup›′{Body: \"the first standup note\""
            + "→\"the second standup note\"}`)",
            result);
    }

    private static RenderedValue.ObjectVal Note(string titleKey, string body)
    {
        return Object("Note", (titleKey, Text("Standup")), ("Body", Text(body)));
    }
}
