// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Content-addressed value deduplication in the Markdown renderer: a captured
/// value rendered identically more than once is defined with a readable
/// reference on first emission and referred to by that reference afterwards.
/// Byte equality certifies sameness; any difference renders in full.
/// </summary>
public class MarkdownValueReferenceTests
{
    private const string Hotel =
        "Expense(Description: \"Hotel\", Amount: 400.00, Currency: \"EUR\")";

    private static TraceNode Leaf(
        string className, string method,
        string paramName, string value,
        RenderedValue? structured = null)
    {
        return new TraceNode(
            new MethodSignature(
                className, method,
                [new ParameterCapture(paramName, value, false, structured)]),
            new Returned("\"ok\""),
            [],
            TimeSpan.TicksPerMillisecond);
    }

    private static TraceNode Returning(
        string className, string method, string value)
    {
        return new TraceNode(
            new MethodSignature(className, method, []),
            new Returned(value),
            [],
            TimeSpan.TicksPerMillisecond);
    }

    private static RenderedValue.ObjectVal Structured(
        string typeName, params (string Name, string Value)[] fields)
    {
        var map = new Dictionary<string, RenderedValue>(
            StringComparer.Ordinal);
        foreach (var field in fields)
        {
            map[field.Name] = new RenderedValue.StringVal(field.Value);
        }

        return new RenderedValue.ObjectVal(typeName, map);
    }

    [Fact]
    public void Repeated_long_value_defines_a_reference_once_and_reuses_it()
    {
        var tree = new TraceTree([
            Leaf("ExpenseValidator", "EnsureValid", "expense", Hotel),
            Leaf("TripLedger", "RecordExpense", "expense", Hotel),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "EnsureValid**(expense: `‹v1›=" + Hotel + "`)", result);
        Assert.Contains(
            "RecordExpense**(expense: `‹v1›`)", result);
    }

    [Fact]
    public void Label_comes_from_the_identity_field_of_the_structured_value()
    {
        var structured = Structured("Expense", ("Description", "Hotel"));
        var tree = new TraceTree([
            Leaf("ExpenseValidator", "EnsureValid", "expense",
                Hotel, structured),
            Leaf("TripLedger", "RecordExpense", "expense",
                Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "EnsureValid**(expense: `‹Hotel›=" + Hotel + "`)", result);
        Assert.Contains("RecordExpense**(expense: `‹Hotel›`)", result);
    }

    [Fact]
    public void Referenced_value_is_replaced_inside_container_values()
    {
        var tree = new TraceTree([
            Leaf("ExpenseValidator", "EnsureValid", "expense", Hotel),
            Leaf("TripLedger", "RecordExpense", "expense", Hotel),
            Returning("TripLedger", "ExpensesOf", "[" + Hotel + "]"),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("ExpensesOf**() → `[‹v1›]`", result);
    }

    [Fact]
    public void Containment_inside_another_captured_value_earns_a_reference()
    {
        var tree = new TraceTree([
            Leaf("ExpenseValidator", "EnsureValid", "expense", Hotel),
            Returning("TripLedger", "ExpensesOf", "[" + Hotel + "]"),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "EnsureValid**(expense: `‹v1›=" + Hotel + "`)", result);
        Assert.Contains("ExpensesOf**() → `[‹v1›]`", result);
    }

    [Fact]
    public void First_occurrence_inside_a_container_defines_the_reference()
    {
        var tree = new TraceTree([
            Returning("TripLedger", "ExpensesOf", "[" + Hotel + "]"),
            Leaf("ExpenseValidator", "EnsureValid", "expense", Hotel),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "ExpensesOf**() → `[‹v1›=" + Hotel + "]`", result);
        Assert.Contains("EnsureValid**(expense: `‹v1›`)", result);
    }

    [Fact]
    public void Colliding_identity_labels_are_disambiguated_with_ordinals()
    {
        var otherHotel =
            "Expense(Description: \"Hotel\", Amount: 999.99, Currency: \"CHF\")";
        var structured = Structured("Expense", ("Description", "Hotel"));
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
            Leaf("B", "Third", "expense", otherHotel, structured),
            Leaf("B", "Fourth", "expense", otherHotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("First**(expense: `‹Hotel›=" + Hotel + "`)", result);
        Assert.Contains(
            "Third**(expense: `‹Hotel·2›=" + otherHotel + "`)", result);
        Assert.Contains("Fourth**(expense: `‹Hotel·2›`)", result);
    }

    [Fact]
    public void Identity_label_is_capped_at_twenty_four_characters()
    {
        var longName = "A-very-long-hotel-description-beyond-cap";
        var structured = Structured("Expense", ("Description", longName));
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹" + longName[..24] + "…›", result);
    }

    [Fact]
    public void Label_exactly_at_the_cap_length_is_not_truncated()
    {
        var exactlyTwentyFour = "123456789012345678901234";
        var structured =
            Structured("Expense", ("Description", exactlyTwentyFour));
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹" + exactlyTwentyFour + "›=" + Hotel, result);
        Assert.DoesNotContain("…›", result);
    }

    [Fact]
    public void Redacted_identity_field_is_skipped_for_the_next_candidate()
    {
        var structured = Structured(
            "Member", ("Name", "[REDACTED]"), ("Id", "m-17"));
        var tree = new TraceTree([
            Leaf("A", "First", "member", Hotel, structured),
            Leaf("A", "Second", "member", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹m-17›=" + Hotel, result);
        Assert.DoesNotContain("‹[REDACTED]›", result);
    }

    [Fact]
    public void Object_without_a_usable_identity_field_falls_back_to_the_type()
    {
        var structured = new RenderedValue.ObjectVal(
            "Expense",
            new Dictionary<string, RenderedValue>(StringComparer.Ordinal)
            {
                ["Amount"] = new RenderedValue.DoubleVal(1.0),
            });
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹Expense›=" + Hotel, result);
    }

    [Fact]
    public void Control_characters_in_the_identity_value_are_sanitized()
    {
        var structured = Structured("Expense", ("Description", "Ho\ntel"));
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹Ho\\ntel›", result);
    }

    [Fact]
    public void Short_repeated_values_are_never_referenced()
    {
        var tree = new TraceTree([
            Leaf("A", "First", "tripName", "\"Ski Weekend\""),
            Leaf("A", "Second", "tripName", "\"Ski Weekend\""),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("First**(tripName: `\"Ski Weekend\"`)", result);
        Assert.Contains("Second**(tripName: `\"Ski Weekend\"`)", result);
        Assert.DoesNotContain("‹", result);
    }

    [Fact]
    public void Value_exactly_at_the_minimum_reference_length_is_referenced()
    {
        var exactlyForty = "0123456789012345678901234567890123456789";
        var tree = new TraceTree([
            Leaf("A", "First", "token", exactlyForty),
            Leaf("A", "Second", "token", exactlyForty),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "First**(token: `‹v1›=" + exactlyForty + "`)", result);
        Assert.Contains("Second**(token: `‹v1›`)", result);
    }

    [Fact]
    public void Value_one_short_of_the_minimum_length_is_never_referenced()
    {
        var thirtyNine = "012345678901234567890123456789012345678";
        var tree = new TraceTree([
            Leaf("A", "First", "token", thirtyNine),
            Leaf("A", "Second", "token", thirtyNine),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("‹", result);
    }

    [Fact]
    public void Parent_inline_return_defines_the_reference_for_its_child()
    {
        var transfers =
            "[Transfer(From: \"Carol\", To: \"Alice\", Amount: 111.25)]";
        var child = Returning("SettlementPlanner", "PlanTransfers", transfers);
        var parent = new TraceNode(
            new MethodSignature("TripSettlementService", "SettleTrip", []),
            new Returned(transfers),
            [child],
            2 * TimeSpan.TicksPerMillisecond);
        var tree = new TraceTree([parent]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "SettleTrip**() → `‹v1›=" + transfers + "`", result);
        Assert.Contains("PlanTransfers**() → `‹v1›`", result);
        Assert.DoesNotContain("\n  - → ", result);
    }

    [Fact]
    public void Containment_weight_counts_each_emission_of_the_container()
    {
        var listRender = "[" + Hotel + "]";
        var tree = new TraceTree([
            Leaf("ExpenseValidator", "EnsureValid", "expense", Hotel),
            Returning("TripLedger", "ExpensesOf", listRender),
            Returning("TripLedger", "ExpensesOf", listRender),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "EnsureValid**(expense: `‹v1›=" + Hotel + "`)", result);
        Assert.Contains("`‹v2›=[‹v1›]`", result);
        Assert.Contains("→ `‹v2›`", result);
    }

    [Fact]
    public void Value_at_index_zero_of_a_container_is_counted_and_replaced()
    {
        var container = Hotel + " recorded at 09:15";
        var tree = new TraceTree([
            Leaf("ExpenseValidator", "EnsureValid", "expense", Hotel),
            Returning("AuditLog", "LastEntry", container),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains(
            "EnsureValid**(expense: `‹v1›=" + Hotel + "`)", result);
        Assert.Contains(
            "LastEntry**() → `‹v1› recorded at 09:15`", result);
    }

    [Fact]
    public void Redacted_parameter_renders_the_marker_not_a_reference()
    {
        var node = new TraceNode(
            new MethodSignature(
                "AccountService", "Login",
                [new ParameterCapture(
                    "password", "\"hunter2\"", true)]),
            new Returned("\"ok\""),
            [],
            TimeSpan.TicksPerMillisecond);
        var tree = new TraceTree([node]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("Login**(password: `[REDACTED]`)", result);
        Assert.DoesNotContain("hunter2", result);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Id")]
    [InlineData("Description")]
    [InlineData("Title")]
    [InlineData("Key")]
    [InlineData("Label")]
    [InlineData("Code")]
    public void Every_identity_field_can_supply_the_label(string fieldName)
    {
        var structured = Structured("Expense", (fieldName, "Hotel"));
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹Hotel›=" + Hotel, result);
    }

    [Fact]
    public void A_non_identity_field_never_supplies_the_label()
    {
        var structured = Structured("Expense", ("Nickname", "Hotel"));
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel, structured),
            Leaf("A", "Second", "expense", Hotel, structured),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("‹Hotel›", result);
        Assert.Contains("‹Expense›=" + Hotel, result);
    }

    [Fact]
    public void The_first_structured_value_seen_wins_the_label()
    {
        var tree = new TraceTree([
            Leaf("A", "First", "expense", Hotel,
                Structured("Expense", ("Name", "Alpha"))),
            Leaf("A", "Second", "expense", Hotel,
                Structured("Expense", ("Name", "Beta"))),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("‹Alpha›=" + Hotel, result);
        Assert.DoesNotContain("‹Beta›", result);
    }

    [Fact]
    public void Longer_referenced_values_are_replaced_before_shorter_ones()
    {
        // The long value contains the short one. Replacing shortest-first
        // would rewrite the inside of the long value and stop it matching,
        // so the container would never resolve to a single reference.
        var shortValue = new string('a', 40);
        var longValue = shortValue + new string('b', 40);
        var container = "[" + longValue + "]";
        var tree = new TraceTree([
            Leaf("A", "First", "v", shortValue),
            Leaf("A", "Second", "v", longValue),
            Returning("A", "Third", container),
            Returning("A", "Fourth", container),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("First**(v: `‹v1›=" + shortValue + "`)", result);
        Assert.Contains("Second**(v: `‹v2›=‹v1›bbb", result);
        Assert.Contains("Third**() → `‹v3›=[‹v2›]`", result);
    }

    [Fact]
    public void Equal_length_values_are_labelled_in_ordinal_order()
    {
        // The container is emitted once, so it earns no reference of its own.
        // Rendering it defines labels for the values it contains, in the
        // index's ordering. Both are captured standalone too, which is what
        // makes them candidates at all — a bare substring never is.
        var alpha = "a" + new string('x', 39);
        var beta = "b" + new string('x', 39);
        var tree = new TraceTree([
            Returning("A", "Container", "[" + beta + ", " + alpha + "]"),
            Leaf("B", "UsesAlpha", "v", alpha),
            Leaf("B", "AlphaAgain", "v", alpha),
            Leaf("C", "UsesBeta", "v", beta),
            Leaf("C", "BetaAgain", "v", beta),
        ]);

        var result = MarkdownRenderer.Render(tree);

        // Ordinal tie-break on equal lengths: alpha is defined first.
        Assert.Contains("‹v1›=" + alpha, result);
        Assert.Contains("‹v2›=" + beta, result);
    }

    [Fact]
    public void Consecutive_occurrences_are_each_replaced()
    {
        var value = new string('z', 40);
        var tree = new TraceTree([
            Leaf("A", "First", "v", value),
            Leaf("A", "Second", "v", value),
            Returning("A", "Both", value + value),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("First**(v: `‹v1›=" + value + "`)", result);
        Assert.Contains("Both**() → `‹v1›‹v1›`", result);
    }

    [Fact]
    public void Void_completion_never_enters_the_reference_machinery()
    {
        var node = new TraceNode(
            new MethodSignature("Auditor", "Record", []),
            new Returned(null),
            [],
            TimeSpan.TicksPerMillisecond);
        var tree = new TraceTree([node]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("- **Auditor.Record**()", result);
        Assert.DoesNotContain("→", result);
    }
}
