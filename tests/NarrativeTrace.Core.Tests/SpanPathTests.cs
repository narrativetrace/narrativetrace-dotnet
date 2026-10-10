// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class SpanPathTests
{
    [Theory]
    [InlineData(null, 1, "#1")]
    [InlineData(null, 12, "#12")]
    [InlineData("#1", 3, "#1.3")]
    [InlineData("#1.3", 2, "#1.3.2")]
    public void Child_appends_a_one_based_position_to_its_parent(string? parent, int position, string id)
    {
        Assert.Equal(id, SpanPath.Child(parent, position));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Child_refuses_a_position_below_one(int position)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SpanPath.Child("#1", position));
    }

    [Theory]
    [InlineData("#1", true)]
    [InlineData("#1.3.20", true)]
    [InlineData("#", false)]
    [InlineData("#1.", false)]
    [InlineData("#.1", false)]
    [InlineData("#1..2", false)]
    [InlineData("1.2", false)]
    [InlineData("#1 ", false)]
    [InlineData("#1a", false)]
    [InlineData("#١", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWellFormed_accepts_only_hash_then_dot_separated_ascii_digit_runs(string? text, bool wellFormed)
    {
        Assert.Equal(wellFormed, SpanPath.IsWellFormed(text));
    }

    [Theory]
    [InlineData("  #1.2 - A.B()", "  - A.B()", "#1.2")]
    [InlineData("#1 ~ fire-and-forget", "~ fire-and-forget", "#1")]
    [InlineData("  - A.B()", "  - A.B()", null)]
    [InlineData("  ~ fork [2]", "  ~ fork [2]", null)]
    [InlineData("scenario: #1 is not an id here", "scenario: #1 is not an id here", null)]
    [InlineData("#1.- A.B()", "#1.- A.B()", null)]
    [InlineData("#12", "#12", null)]
    [InlineData("", "", null)]
    public void Strip_and_Of_read_only_a_well_formed_id_after_the_indent(string line, string stripped, string? id)
    {
        Assert.Equal(stripped, SpanPath.Strip(line));
        Assert.Equal(id, SpanPath.Of(line));
    }

    [Fact]
    public void Strip_and_Of_refuse_null()
    {
        Assert.Throws<ArgumentNullException>(() => SpanPath.Strip(null!));
        Assert.Throws<ArgumentNullException>(() => SpanPath.Of(null!));
        Assert.Throws<ArgumentNullException>(() => SpanPath.IdsOf(null!, null));
    }

    [Fact]
    public void IdsOf_numbers_capture_order_lists_and_launched_work_under_its_launch()
    {
        var tree = SpanIdFlavourParityTests.Checkout();
        var checkout = tree.Roots[0];

        Assert.Equal(["#1", "#2"], SpanPath.IdsOf(tree.Roots, null));
        // stock, discount (fork: by Class.method), charge, launcher stub, launched send
        Assert.Equal(
            ["#1.2", "#1.1", "#1.3", "#1.4", "#1.4.1"],
            SpanPath.IdsOf(checkout.Children, "#1"));
    }

    [Fact]
    public void IdsOf_keeps_capture_order_between_concurrent_members_of_one_signature()
    {
        var info = new ConcurrencyInfo("fork-1", "t", 1, "t", true, ConcurrencyKind.ForkJoin);
        TraceNode Member(string value) => new(
            new MethodSignature("Repo", "Load", []), new Returned(value), [], 0, 0, info);

        Assert.Equal(["#1", "#2", "#3"], SpanPath.IdsOf([Member("a"), Member("b"), Member("c")], null));
    }

    [Fact]
    public void Concurrent_order_compares_class_and_method_as_one_dotted_name()
    {
        // Ordinal over "Class.Method": '+' (a nested type) sorts before '.', so Outer+Inner.M comes
        // before Outer.M — a class-then-method comparison would put Outer first and disagree with the .nt.
        var info = new ConcurrencyInfo("fork-1", "t", 1, "t", true, ConcurrencyKind.ForkJoin);
        var outer = new TraceNode(new MethodSignature("Outer", "M", []), new Returned(null), [], 0, 0, info);
        var nested = new TraceNode(new MethodSignature("Outer+Inner", "M", []), new Returned(null), [], 0, 0, info);
        var tree = new TraceTree([new TraceNode(new MethodSignature("Svc", "Run", []), new Returned(null), [outer, nested], 0)]);

        Assert.Equal(["#1.2", "#1.1"], SpanPath.IdsOf([outer, nested], "#1"));
        Assert.Contains("    #1.1 - Outer+Inner.M()\n    #1.2 - Outer.M()\n", StructuralTraceRenderer.Render(tree), StringComparison.Ordinal);
        Assert.Contains("Outer+Inner.M() #1.1", IndentedTextRenderer.Render(tree), StringComparison.Ordinal);
    }
}
