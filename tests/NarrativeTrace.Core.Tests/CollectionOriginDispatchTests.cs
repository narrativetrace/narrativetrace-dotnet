// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Structured-rendering coverage for the origin-dispatch mechanism
/// (<c>PlatformTypes</c>, <c>ValueRenderer.RenderEnumerableByOrigin</c>/
/// <c>RenderStructuredEnumerableByOrigin</c>): the flat-render half is
/// pinned by <c>RenderReadsStateTests</c>, but flat and structured are two
/// independent code paths this repository's engineering guide requires
/// parity between, and the structured twin had no test of its own before
/// this file.
/// </summary>
public class CollectionOriginDispatchTests
{
    private sealed class ListSubclass : List<int>
    {
    }

    [Fact]
    public void A_List_subclass_renders_its_elements_in_structured_form_through_the_ancestors_state()
    {
        var list = new ListSubclass { 1, 2, 3 };

        var result = ValueRenderer.RenderStructured(list);

        var listVal = Assert.IsType<RenderedValue.ListVal>(result);
        Assert.Equal(3, listVal.Elements.Count);
        Assert.Equal(1L, Assert.IsType<RenderedValue.LongVal>(listVal.Elements[0]).Value);
        Assert.Equal(2L, Assert.IsType<RenderedValue.LongVal>(listVal.Elements[1]).Value);
        Assert.Equal(3L, Assert.IsType<RenderedValue.LongVal>(listVal.Elements[2]).Value);
    }

    private sealed class SelfReferentialListSubclass : List<object>
    {
    }

    [Fact]
    public void A_List_subclass_that_holds_itself_renders_a_circular_reference_marker_not_a_stack_overflow()
    {
        var list = new SelfReferentialListSubclass();
        list.Add(list);

        var flat = ValueRenderer.Render(list);
        var structured = ValueRenderer.RenderStructured(list);

        Assert.Contains("@", flat, StringComparison.Ordinal);
        Assert.Contains("SelfReferentialListSubclass", flat, StringComparison.Ordinal);
        var listVal = Assert.IsType<RenderedValue.ListVal>(structured);
        var marker = Assert.IsType<RenderedValue.StringVal>(listVal.Elements[0]);
        Assert.Contains("SelfReferentialListSubclass", marker.Value, StringComparison.Ordinal);
    }

    private sealed class CorruptedListSubclass : List<int>
    {
    }

    // _size lives on List<T> itself, so this reaches it through the exact
    // same ancestor field the renderer reads (PlatformTypes.ListAncestor +
    // TypeShape-style reflection), not a different field.
    private static void CorruptSizeBeyondItemsLength(List<int> list)
    {
        var sizeField = typeof(List<int>).GetField(
            "_size", BindingFlags.NonPublic | BindingFlags.Instance)!;
        sizeField.SetValue(list, ((int[])typeof(List<int>)
            .GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(list)!).Length + 5);
    }

    [Fact]
    public void A_List_subclass_with_a_corrupted_size_degrades_only_the_out_of_bounds_elements()
    {
        // Totality/no-poison contract (matches ValueRendererTotalityTests'
        // style): the renderer trusts List<T>'s own _size field rather than
        // re-deriving it, so a hostile/corrupted _size larger than the real
        // backing array must degrade the elements it cannot read, not throw
        // out of the render entirely.
        var list = new CorruptedListSubclass { 1, 2 };
        CorruptSizeBeyondItemsLength(list);

        var flat = ValueRenderer.Render(list);
        var structured = ValueRenderer.RenderStructured(list);

        Assert.Contains("1", flat, StringComparison.Ordinal);
        Assert.Contains("<error:", flat, StringComparison.Ordinal);
        var listVal = Assert.IsType<RenderedValue.ListVal>(structured);
        Assert.Contains(
            listVal.Elements,
            e => e is RenderedValue.StringVal s
                && s.Value.StartsWith("<error:", StringComparison.Ordinal));
    }

    [NarrativeElements]
    private sealed class DeclaredElementsSequence : IEnumerable<int>
    {
        private readonly List<int> _data;

        public DeclaredElementsSequence(IEnumerable<int> data)
        {
            _data = new List<int>(data);
        }

        public IEnumerator<int> GetEnumerator() => _data.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void A_type_declaring_the_elements_hook_renders_its_elements_in_structured_form()
    {
        var seq = new DeclaredElementsSequence([1, 2, 3]);

        var result = ValueRenderer.RenderStructured(seq);

        var listVal = Assert.IsType<RenderedValue.ListVal>(result);
        Assert.Equal(3, listVal.Elements.Count);
    }

    private sealed class UndeclaredSequence : IEnumerable<int>
    {
        public int GetEnumeratorCalls;
        private readonly int[] _data = [1, 2, 3];

        public IEnumerator<int> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return ((IEnumerable<int>)_data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void An_undeclared_user_IEnumerable_never_has_its_GetEnumerator_invoked_by_structured_rendering()
    {
        var seq = new UndeclaredSequence();

        ValueRenderer.RenderStructured(seq);

        Assert.Equal(0, seq.GetEnumeratorCalls);
    }
}
