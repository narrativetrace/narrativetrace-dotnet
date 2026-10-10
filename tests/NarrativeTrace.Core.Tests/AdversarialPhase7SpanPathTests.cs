// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Adversarial probes of <see cref="SpanPath"/>: hostile id text, boundary positions, and sibling
/// lists mixing forks, async groups and fire-and-forget launches.
/// </summary>
public sealed class AdversarialPhase7SpanPathTests
{
    private static TraceNode Plain(string cls, string method, params TraceNode[] children) =>
        new(new MethodSignature(cls, method, []), new Returned(null), children, 0);

    private static TraceNode Grouped(
        string cls, string method, string group, ConcurrencyKind kind, params TraceNode[] children) =>
        new(new MethodSignature(cls, method, []), new Returned(null), children, 0, 0,
            new ConcurrencyInfo(group, method, 1, null, false, kind));

    [Fact]
    public void Child_of_a_root_is_a_bare_position()
    {
        Assert.Equal("#1", SpanPath.Child(null, 1));
    }

    [Fact]
    public void Child_rejects_position_zero_because_positions_are_one_based()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpanPath.Child(null, 0));
    }

    [Fact]
    public void Child_rejects_a_negative_position_under_a_parent()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpanPath.Child("#1", -3));
    }

    [Fact]
    public void Child_keeps_every_digit_of_the_largest_position()
    {
        Assert.Equal("#1.2147483647", SpanPath.Child("#1", int.MaxValue));
    }

    [Fact]
    public void Child_output_is_always_a_well_formed_id()
    {
        Assert.True(SpanPath.IsWellFormed(SpanPath.Child("#1.4", 12)));
    }

    [Theory]
    [InlineData("#1")]
    [InlineData("#1.3.2")]
    [InlineData("#1234567890.1")]
    public void IsWellFormed_accepts_derived_position_paths(string id)
    {
        Assert.True(SpanPath.IsWellFormed(id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#")]
    [InlineData("1.2")]
    [InlineData("#1.")]
    [InlineData("#.1")]
    [InlineData("#1..2")]
    [InlineData(" #1")]
    [InlineData("#1 ")]
    [InlineData("#1\t")]
    [InlineData("#1.2a")]
    [InlineData("#١")]
    [InlineData("#1.-")]
    [InlineData("# 1")]
    public void IsWellFormed_rejects_near_miss_text(string text)
    {
        Assert.False(SpanPath.IsWellFormed(text));
    }

    [Fact]
    public void IsWellFormed_rejects_null()
    {
        Assert.False(SpanPath.IsWellFormed(null));
    }

    [Theory]
    [InlineData("    #1.2 - A.B()", "#1.2")]
    [InlineData("#1 - A.B()", "#1")]
    [InlineData("#1234567890.1 - A.B()", "#1234567890.1")]
    public void Of_reads_the_id_after_the_indent(string line, string id)
    {
        Assert.Equal(id, SpanPath.Of(line));
    }

    [Theory]
    [InlineData("#1.2\t- A.B()")]
    [InlineData("#1.-  - A.B()")]
    [InlineData("#1.2")]
    [InlineData("  # 1 - A.B()")]
    [InlineData("#١ - A.B()")]
    [InlineData("#1..2 - A.B()")]
    public void Of_returns_null_for_text_that_only_looks_like_an_id(string line)
    {
        Assert.Null(SpanPath.Of(line));
    }

    [Fact]
    public void Of_on_empty_line_is_null()
    {
        Assert.Null(SpanPath.Of(""));
    }

    [Fact]
    public void Strip_removes_the_id_and_exactly_one_space_keeping_the_indent()
    {
        Assert.Equal("  - A.B()", SpanPath.Strip("  #1.2 - A.B()"));
    }

    [Fact]
    public void Strip_keeps_a_second_space_that_follows_the_id_separator()
    {
        Assert.Equal(" - A.B()", SpanPath.Strip("#1  - A.B()"));
    }

    [Fact]
    public void Strip_leaves_a_baseline_line_that_only_looks_like_an_id()
    {
        Assert.Equal("  #1.- A.B()", SpanPath.Strip("  #1.- A.B()"));
    }

    [Fact]
    public void Strip_leaves_a_line_whose_id_is_followed_by_a_tab()
    {
        Assert.Equal("#1.2\t- A.B()", SpanPath.Strip("#1.2\t- A.B()"));
    }

    [Fact]
    public void Strip_of_an_id_only_line_leaves_an_empty_line()
    {
        Assert.Equal("", SpanPath.Strip("#1 "));
    }

    [Fact]
    public void Strip_of_an_id_without_a_trailing_space_is_unchanged()
    {
        Assert.Equal("#1", SpanPath.Strip("#1"));
    }

    [Fact]
    public void Strip_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SpanPath.Strip(null!));
    }

    [Fact]
    public void Of_null_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SpanPath.Of(null!));
    }

    [Fact]
    public void IdsOf_null_siblings_throws()
    {
        Assert.Throws<ArgumentNullException>(() => SpanPath.IdsOf(null!, null));
    }

    [Fact]
    public void IdsOf_empty_siblings_is_empty()
    {
        Assert.Empty(SpanPath.IdsOf([], null));
    }

    [Fact]
    public void IdsOf_under_a_parent_prefixes_every_id_with_the_parent()
    {
        Assert.Equal(["#3.1.1", "#3.1.2"], SpanPath.IdsOf([Plain("A", "x"), Plain("B", "y")], "#3.1"));
    }

    [Fact]
    public void IdsOf_fork_members_are_numbered_by_class_and_method_not_capture_order()
    {
        var siblings = new[]
        {
            Grouped("Zed", "Run", "f1", ConcurrencyKind.ForkJoin),
            Grouped("Alpha", "Run", "f1", ConcurrencyKind.ForkJoin),
        };

        Assert.Equal(["#2", "#1"], SpanPath.IdsOf(siblings, null));
    }

    [Fact]
    public void IdsOf_orders_members_ordinally_so_an_uppercase_class_sorts_before_a_lowercase_one()
    {
        var siblings = new[]
        {
            Grouped("a", "run", "f1", ConcurrencyKind.ForkJoin),
            Grouped("B", "run", "f1", ConcurrencyKind.ForkJoin),
        };

        Assert.Equal(["#2", "#1"], SpanPath.IdsOf(siblings, null));
    }

    [Fact]
    public void IdsOf_members_with_equal_signatures_keep_capture_order()
    {
        var siblings = new[]
        {
            Grouped("A", "Run", "f1", ConcurrencyKind.ForkJoin),
            Grouped("A", "Run", "f1", ConcurrencyKind.ForkJoin),
        };

        Assert.Equal(["#1", "#2"], SpanPath.IdsOf(siblings, null));
    }

    [Fact]
    public void IdsOf_a_fork_interrupted_by_a_plain_call_takes_separate_positions()
    {
        var siblings = new[]
        {
            Grouped("A", "x", "f1", ConcurrencyKind.ForkJoin),
            Plain("P", "y"),
            Grouped("B", "z", "f1", ConcurrencyKind.ForkJoin),
        };

        Assert.Equal(["#1", "#2", "#3"], SpanPath.IdsOf(siblings, null));
    }

    [Fact]
    public void IdsOf_fire_and_forget_launch_takes_one_position_and_nests_the_launched_work()
    {
        var siblings = new[]
        {
            Grouped("Notif", "fire-and-forget", "g2", ConcurrencyKind.FireAndForget),
            Grouped("Work", "One", "g2", ConcurrencyKind.FireAndForget),
            Plain("P", "Two"),
        };

        Assert.Equal(["#1", "#1.1", "#2"], SpanPath.IdsOf(siblings, null));
    }

    [Fact]
    public void IdsOf_launcher_stub_in_the_middle_still_carries_the_launch_id()
    {
        var siblings = new[]
        {
            Grouped("Work", "One", "g2", ConcurrencyKind.FireAndForget),
            Grouped("Notif", "fire-and-forget", "g2", ConcurrencyKind.FireAndForget),
        };

        Assert.Equal(["#1.1", "#1"], SpanPath.IdsOf(siblings, null));
    }

    [Fact]
    public void IdsOf_fire_and_forget_node_named_fire_and_forget_with_children_is_launched_work()
    {
        var stubWithChild = Grouped(
            "Svc", "fire-and-forget", "g2", ConcurrencyKind.FireAndForget, Plain("Child", "Do"));

        Assert.Equal(["#1.1"], SpanPath.IdsOf([stubWithChild], null));
    }

    [Fact]
    public void IdsOf_a_plain_sibling_after_a_launch_takes_the_next_position_not_a_launched_one()
    {
        var siblings = new[]
        {
            Grouped("Notif", "fire-and-forget", "g2", ConcurrencyKind.FireAndForget),
            Plain("After", "Launch"),
        };

        Assert.Equal(["#1", "#2"], SpanPath.IdsOf(siblings, null));
    }
}
