// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class LineDiffTests
{
    [Fact]
    public void ToLines_of_an_empty_document_is_empty()
    {
        Assert.Empty(LineDiff.ToLines(string.Empty));
    }

    [Fact]
    public void ToLines_drops_only_the_trailing_empty_entry_from_a_final_terminator()
    {
        Assert.Equal(["a", "b"], LineDiff.ToLines("a\nb\n"));
    }

    [Fact]
    public void ToLines_of_a_document_with_no_trailing_terminator_keeps_every_line()
    {
        Assert.Equal(["a", "b"], LineDiff.ToLines("a\nb"));
    }

    [Fact]
    public void ToLines_keeps_an_interior_blank_line()
    {
        Assert.Equal(["a", "", "b"], LineDiff.ToLines("a\n\nb"));
    }

    [Fact]
    public void Unified_of_two_empty_documents_is_empty()
    {
        Assert.Equal(string.Empty, LineDiff.Unified(string.Empty, string.Empty));
    }

    [Fact]
    public void Unified_reports_every_line_of_an_empty_baseline_as_added()
    {
        Assert.Equal("+a\n+b\n", LineDiff.Unified(string.Empty, "a\nb"));
    }

    [Fact]
    public void Unified_reports_every_line_of_an_empty_current_as_removed()
    {
        Assert.Equal("-a\n-b\n", LineDiff.Unified("a\nb", string.Empty));
    }

    /// <summary>Exercises the mid-loop insertion branch: an insertion strictly ahead of any deletion.</summary>
    [Fact]
    public void Unified_prefers_insertion_when_it_is_the_only_way_to_reach_the_common_suffix()
    {
        var diff = LineDiff.Unified("a", "x\na");

        Assert.Equal("+x\n a\n", diff);
    }

    [Fact]
    public void Unified_of_identical_documents_is_all_context()
    {
        Assert.Equal(" a\n b\n", LineDiff.Unified("a\nb", "a\nb"));
    }
}
