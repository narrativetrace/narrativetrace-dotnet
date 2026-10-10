// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Adversarial probes of <see cref="StructuralDelta"/> sameness and the line diff: id-only changes,
/// line terminators, empty documents, id-looking text that is not an id.
/// </summary>
public sealed class AdversarialPhase7DeltaTests
{
    [Fact]
    public void Two_empty_documents_are_unchanged_with_no_summary_and_no_diff()
    {
        var delta = StructuralDelta.Between("", "");

        Assert.True(delta.Unchanged);
        Assert.Equal("", delta.Summary());
        Assert.Equal("", delta.Diff());
    }

    [Fact]
    public void Empty_baseline_against_one_call_reports_that_call_as_added()
    {
        var delta = StructuralDelta.Between("", "#1 - A.B()\n");

        Assert.False(delta.Unchanged);
        Assert.Equal("+1 call A.B", delta.Summary());
        Assert.Contains("+#1 - A.B()", delta.Diff());
    }

    [Fact]
    public void A_missing_final_newline_is_not_a_change()
    {
        Assert.True(StructuralDelta.Between("scenario: x\n\n#1 - A.B()", "scenario: x\n\n#1 - A.B()\n").Unchanged);
    }

    [Fact]
    public void CRLF_and_LF_documents_with_the_same_lines_are_unchanged()
    {
        Assert.True(StructuralDelta.Between("#1 - A.B()\r\n#2 - C.D()\r\n", "#1 - A.B()\n#2 - C.D()\n").Unchanged);
    }

    [Fact]
    public void CR_only_line_endings_are_encoding_and_leave_the_document_unchanged()
    {
        Assert.True(StructuralDelta.Between("#1 - A.B()\r#2 - C.D()", "#1 - A.B()\n#2 - C.D()\n").Unchanged);
    }

    [Fact]
    public void Id_looking_text_that_is_not_an_id_is_structure_and_makes_the_documents_differ()
    {
        var delta = StructuralDelta.Between("#1.- - A.B()\n", "#1 - A.B()\n");

        Assert.False(delta.Unchanged);
    }

    [Fact]
    public void Id_only_shift_next_to_a_real_addition_reports_only_the_real_call_counts()
    {
        var delta = StructuralDelta.Between(
            "#1 - A.x()\n#2 - B.y()\n",
            "#1 - X.w()\n#2 - A.x()\n#3 - B.y()\n#4 - C.z()\n");

        Assert.Equal("+1 call X.w, +1 call C.z", delta.Summary());
    }

    [Fact]
    public void Diff_cites_the_baseline_id_on_context_lines_shifted_by_an_insertion()
    {
        var diff = StructuralDelta.Between(
            "#1 - A.x()\n#2 - B.y()\n",
            "#1 - X.w()\n#2 - A.x()\n#3 - B.y()\n").Diff();

        Assert.Contains("+#1 - X.w()", diff);
        Assert.Contains(" #2 - A.x()  (was #1)", diff);
        Assert.Contains(" #3 - B.y()  (was #2)", diff);
    }

    [Fact]
    public void Diff_of_a_crlf_baseline_prints_no_carriage_returns()
    {
        var diff = StructuralDelta.Between("#1 - A.x()\r\n#2 - B.y()\r\n", "#1 - A.x()\n#2 - Q.r()\n").Diff();

        Assert.DoesNotContain("\r", diff);
    }

    [Fact]
    public void Diff_against_an_id_free_baseline_cites_no_previous_id()
    {
        var diff = StructuralDelta.Between("- A.x()\n", "#1 - A.x()\n#2 - B.y()\n").Diff();

        Assert.DoesNotContain("(was", diff);
    }

    [Fact]
    public void Indentation_only_change_is_reported_as_structure_changed_because_no_call_count_moved()
    {
        var delta = StructuralDelta.Between("#1 - A.x()\n", "  #1 - A.x()\n");

        Assert.False(delta.Unchanged);
        Assert.Equal("structure changed", delta.Summary());
    }

    [Fact]
    public void Between_rejects_a_null_baseline()
    {
        Assert.Throws<ArgumentNullException>(() => StructuralDelta.Between(null!, ""));
    }

    [Fact]
    public void Between_rejects_a_null_current_document()
    {
        Assert.Throws<ArgumentNullException>(() => StructuralDelta.Between("", null!));
    }
}
