// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class StructuralDeltaTests
{
    [Fact]
    public void Identical_documents_are_unchanged_with_empty_summary_and_diff()
    {
        const string doc = "scenario: x\n\n- A.b()\n";

        var delta = StructuralDelta.Between(doc, doc);

        Assert.True(delta.Unchanged);
        Assert.Equal(string.Empty, delta.Summary());
        Assert.Equal(string.Empty, delta.Diff());
    }

    [Fact]
    public void Between_rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => StructuralDelta.Between(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => StructuralDelta.Between("x", null!));
    }

    [Fact]
    public void An_added_call_is_reported_as_a_plus_one_count()
    {
        var baseline = "scenario: x\n\n- A.b()\n";
        var current = "scenario: x\n\n- A.b()\n- A.c()\n";

        var delta = StructuralDelta.Between(baseline, current);

        Assert.False(delta.Unchanged);
        Assert.Equal("+1 call A.c", delta.Summary());
    }

    [Fact]
    public void A_removed_call_is_reported_as_a_minus_one_count()
    {
        var baseline = "scenario: x\n\n- A.b()\n- A.c()\n";
        var current = "scenario: x\n\n- A.b()\n";

        var delta = StructuralDelta.Between(baseline, current);

        Assert.Equal("-1 call A.c", delta.Summary());
    }

    [Fact]
    public void Multiple_added_calls_to_the_same_signature_are_counted_with_the_plural_noun()
    {
        var baseline = "scenario: x\n\n- A.b()\n";
        var current = "scenario: x\n\n- A.b()\n- CurrencyConverter.toBaseCurrency(amount)\n"
            + "- CurrencyConverter.toBaseCurrency(amount)\n";

        var delta = StructuralDelta.Between(baseline, current);

        Assert.Equal("+2 calls CurrencyConverter.toBaseCurrency", delta.Summary());
    }

    /// <summary>
    /// A structural difference that is not a whole-call add/remove (a fork
    /// marker changing shape) still counts as CHANGED, and the summary falls
    /// back to a generic phrase rather than reporting a misleading zero
    /// change.
    /// </summary>
    [Fact]
    public void A_change_with_no_net_call_count_difference_still_reports_structure_changed()
    {
        var baseline = "scenario: x\n\n~ fork [2]\n  - A.b()\n  - A.c()\n";
        var current = "scenario: x\n\n~ async [2]\n  - A.b()\n  - A.c()\n";

        var delta = StructuralDelta.Between(baseline, current);

        Assert.False(delta.Unchanged);
        Assert.Equal("structure changed", delta.Summary());
    }

    [Fact]
    public void Diff_marks_removed_and_added_lines_and_keeps_context_unmarked()
    {
        var baseline = "scenario: x\n\n- A.b()\n- A.c()\n";
        var current = "scenario: x\n\n- A.b()\n- A.d()\n";

        var diff = StructuralDelta.Between(baseline, current).Diff();

        Assert.Equal(
            " scenario: x\n \n - A.b()\n-- A.c()\n+- A.d()\n",
            diff);
    }
}
