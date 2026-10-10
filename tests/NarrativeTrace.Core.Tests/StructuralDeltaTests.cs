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

    [Fact]
    public void A_baseline_written_before_span_ids_compares_equal_to_the_same_flow_with_ids()
    {
        const string baseline = "scenario: x\n\n- A.b()\n  - C.d() → value\n";
        const string current = "scenario: x\n\n#1 - A.b()\n  #1.1 - C.d() → value\n";

        var delta = StructuralDelta.Between(baseline, current);

        Assert.True(delta.Unchanged);
        Assert.Equal(string.Empty, delta.Diff());
    }

    [Fact]
    public void Crlf_terminators_and_a_missing_final_newline_are_not_structure()
    {
        const string baseline = "scenario: x\r\n\r\n#1 - A.b()\r\n  #1.1 - C.d()";
        const string current = "scenario: x\n\n#1 - A.b()\n  #1.1 - C.d()\n";

        Assert.True(StructuralDelta.Between(baseline, current).Unchanged);
    }

    [Fact]
    public void A_changed_id_alone_with_the_same_lines_is_unchanged()
    {
        // Ids are derived from position; the same lines mean the same flow.
        const string baseline = "scenario: x\n\n#7 - A.b()\n";
        const string current = "scenario: x\n\n#1 - A.b()\n";

        Assert.True(StructuralDelta.Between(baseline, current).Unchanged);
    }

    [Fact]
    public void Summary_counts_call_lines_that_carry_span_ids()
    {
        const string baseline = "scenario: x\n\n#1 - A.b()\n";
        const string current = "scenario: x\n\n#1 - A.b()\n  #1.1 ~ fire-and-forget\n    #1.1.1 - A.c()\n";

        Assert.Equal("+1 call A.c", StructuralDelta.Between(baseline, current).Summary());
    }

    [Fact]
    public void Diff_cites_the_baseline_id_of_a_context_line_an_insertion_shifted()
    {
        const string baseline = "scenario: x\n\n#1 - A.b()\n  #1.1 - C.d()\n  #1.2 - E.f()\n";
        const string current =
            "scenario: x\n\n#1 - A.b()\n  #1.1 - C.d()\n  #1.2 - New.call()\n  #1.3 - E.f()\n";

        var diff = StructuralDelta.Between(baseline, current).Diff();

        Assert.Equal(
            " scenario: x\n \n #1 - A.b()\n   #1.1 - C.d()\n+  #1.2 - New.call()\n"
            + "   #1.3 - E.f()  (was #1.2)\n",
            diff);
    }

    [Fact]
    public void Diff_against_an_id_free_baseline_prints_the_current_lines_and_cites_nothing()
    {
        const string baseline = "scenario: x\n\n- A.b()\n";
        const string current = "scenario: x\n\n#1 - A.b()\n#2 - A.c()\n";

        Assert.Equal(
            " scenario: x\n \n #1 - A.b()\n+#2 - A.c()\n",
            StructuralDelta.Between(baseline, current).Diff());
    }

    [Fact]
    public void A_removed_line_prints_as_the_baseline_wrote_it()
    {
        const string baseline = "scenario: x\n\n#1 - A.b()\n#2 - A.c()\n";
        const string current = "scenario: x\n\n#1 - A.b()\n";

        Assert.Equal(
            " scenario: x\n \n #1 - A.b()\n-#2 - A.c()\n",
            StructuralDelta.Between(baseline, current).Diff());
    }
}
