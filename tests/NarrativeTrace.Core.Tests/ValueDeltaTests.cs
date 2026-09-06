// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The delta helper on its own: which pairs of structured values it can
/// express as a one-line field diff, and which it refuses so the caller keeps
/// the full flat render.
/// </summary>
public class ValueDeltaTests
{
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

    private static RenderedValue.ObjectVal Expense(
        string typeName, double amount)
    {
        return Object(
            typeName,
            ("Description", new RenderedValue.StringVal("Dinner")),
            ("Amount", new RenderedValue.DoubleVal(amount)));
    }

    private static RenderedValue.ObjectVal Note(string body)
    {
        return Object(
            "Note",
            ("Title", new RenderedValue.StringVal("Standup")),
            ("Body", new RenderedValue.StringVal(body)));
    }

    [Fact]
    public void A_scalar_field_change_becomes_a_one_line_diff()
    {
        Assert.Equal(
            "{Amount: 100→92}",
            ValueDelta.Between(Expense("Expense", 100.0), Expense("Expense", 92.0)));
    }

    [Fact]
    public void An_unchanged_pair_has_no_delta()
    {
        Assert.Null(
            ValueDelta.Between(Expense("Expense", 100.0), Expense("Expense", 100.0)));
    }

    [Fact]
    public void A_different_type_name_has_no_delta()
    {
        Assert.Null(
            ValueDelta.Between(Expense("Expense", 100.0), Expense("Refund", 92.0)));
    }

    [Fact]
    public void A_smaller_field_set_has_no_delta()
    {
        var fewer = Object(
            "Expense", ("Description", new RenderedValue.StringVal("Dinner")));

        Assert.Null(ValueDelta.Between(Expense("Expense", 100.0), fewer));
    }

    [Fact]
    public void An_added_field_is_never_silently_dropped_from_the_diff()
    {
        var richer = Object(
            "Expense",
            ("Description", new RenderedValue.StringVal("Dinner")),
            ("Amount", new RenderedValue.DoubleVal(92.0)),
            ("Currency", new RenderedValue.StringVal("EUR")));

        Assert.Null(ValueDelta.Between(Expense("Expense", 100.0), richer));
    }

    [Fact]
    public void A_scalar_compared_against_a_structured_value_has_no_delta()
    {
        var listed = Object(
            "Expense",
            ("Description", new RenderedValue.StringVal("Dinner")),
            ("Amount", new RenderedValue.ListVal([new RenderedValue.LongVal(1)])));

        Assert.Null(ValueDelta.Between(Expense("Expense", 100.0), listed));
    }

    [Fact]
    public void A_non_object_value_is_never_one_side_of_a_delta()
    {
        var scalar = new RenderedValue.StringVal("Dinner");

        Assert.Null(ValueDelta.Between(scalar, Expense("Expense", 92.0)));
        Assert.Null(ValueDelta.Between(Expense("Expense", 100.0), scalar));
    }

    [Fact]
    public void A_null_side_is_never_a_delta()
    {
        Assert.Null(ValueDelta.Between(null, Expense("Expense", 92.0)));
        Assert.Null(ValueDelta.Between(Expense("Expense", 100.0), null));
    }

    [Fact]
    public void A_string_exactly_at_the_scalar_cap_is_not_elided()
    {
        var sixty = new string('x', 60);

        Assert.Equal(
            "{Body: \"before\"→\"" + sixty + "\"}",
            ValueDelta.Between(Note("before"), Note(sixty)));
    }

    [Fact]
    public void A_string_one_character_past_the_scalar_cap_is_elided()
    {
        Assert.Equal(
            "{Body: \"before\"→\"" + new string('x', 60) + "…\"}",
            ValueDelta.Between(Note("before"), Note(new string('x', 61))));
    }

    [Fact]
    public void A_control_character_never_reaches_the_delta_raw()
    {
        var delta = ValueDelta.Between(Note("before"), Note("a\nb"));

        Assert.Equal("{Body: \"before\"→\"a\\nb\"}", delta);
    }

    [Fact]
    public void An_equal_nested_object_is_walked_not_reference_compared()
    {
        var before = Object(
            "Expense",
            ("Description", new RenderedValue.StringVal("Dinner")),
            ("Amount", new RenderedValue.DoubleVal(100.0)),
            ("Split", Object("Share", ("Payer", new RenderedValue.StringVal("Alice")))));
        var after = Object(
            "Expense",
            ("Description", new RenderedValue.StringVal("Dinner")),
            ("Amount", new RenderedValue.DoubleVal(92.0)),
            ("Split", Object("Share", ("Payer", new RenderedValue.StringVal("Alice")))));

        Assert.Equal("{Amount: 100→92}", ValueDelta.Between(before, after));
    }

    [Fact]
    public void An_equal_nested_list_is_walked_not_reference_compared()
    {
        var before = Object(
            "Cart",
            ("Id", new RenderedValue.StringVal("cart-1")),
            ("Total", new RenderedValue.DoubleVal(10.0)),
            ("Lines", new RenderedValue.ListVal([new RenderedValue.LongVal(7)])));
        var after = Object(
            "Cart",
            ("Id", new RenderedValue.StringVal("cart-1")),
            ("Total", new RenderedValue.DoubleVal(12.0)),
            ("Lines", new RenderedValue.ListVal([new RenderedValue.LongVal(7)])));

        Assert.Equal("{Total: 10→12}", ValueDelta.Between(before, after));
    }

    [Fact]
    public void A_changed_nested_list_length_has_no_delta()
    {
        var before = Object(
            "Cart",
            ("Id", new RenderedValue.StringVal("cart-1")),
            ("Lines", new RenderedValue.ListVal([new RenderedValue.LongVal(7)])));
        var after = Object(
            "Cart",
            ("Id", new RenderedValue.StringVal("cart-1")),
            ("Lines", new RenderedValue.ListVal(
                [new RenderedValue.LongVal(7), new RenderedValue.LongVal(8)])));

        Assert.Null(ValueDelta.Between(before, after));
    }
}
