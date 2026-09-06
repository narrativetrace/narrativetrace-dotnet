// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Linq;
using System.Threading.Tasks;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Audit for the container-redaction item (Java item 46's family): does a
/// .NET wrapper/holder print past a payload's redaction the way Java's
/// <c>Optional</c>, <c>AtomicReference</c>, <c>AtomicReferenceArray</c> and
/// standalone <c>Map.Entry</c> did? Each case here is a probe first — see
/// the item 18b writeup in the repo's tracking doc for which of these were
/// verified red before a fix landed, and which were verified already-safe
/// and are pinned as containment tests instead.
/// </summary>
public class ValueRendererWrapperTests
{
    public sealed record Card(string Last4, [NotTraced] string Cvv);

    [Fact]
    public void Nullable_of_a_value_type_is_never_a_wrapper_the_renderer_can_see()
    {
        // The CLR boxes a Nullable<T> with HasValue == true as a boxed T
        // directly, and HasValue == false as a plain null reference — there
        // is no boxed "Nullable<T> wrapper" object for a renderer to be
        // handed in the first place. Verified, not assumed.
        int? withValue = 42;
        int? withoutValue = null;

        object? boxedWithValue = withValue;
        object? boxedWithoutValue = withoutValue;

        Assert.IsType<int>(boxedWithValue);
        Assert.Null(boxedWithoutValue);
    }

    [Fact]
    public void A_completed_ValueTask_of_a_redacted_record_does_not_leak_in_flat_rendering()
    {
        var task = new ValueTask<Card>(new Card("4111", "123"));

        var rendered = ValueRenderer.Render(task);

        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("123", rendered);
    }

    [Fact]
    public void A_completed_ValueTask_of_a_redacted_record_does_not_leak_in_structured_rendering()
    {
        var task = new ValueTask<Card>(new Card("4111", "123"));

        var rendered = ValueRenderer.RenderStructured(task);

        var text = RenderedValueText(rendered);
        Assert.Contains(RedactionPolicy.Marker, text);
        Assert.DoesNotContain("123", text);
    }

    [Fact]
    public void A_standalone_KeyValuePair_with_a_redacted_value_does_not_leak()
    {
        var pair = new System.Collections.Generic.KeyValuePair<string, Card>(
            "card", new Card("4111", "123"));

        var rendered = ValueRenderer.Render(pair);

        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("123", rendered);
    }

    [Fact]
    public void A_standalone_KeyValuePair_with_a_redacted_value_does_not_leak_in_structured_rendering()
    {
        var pair = new System.Collections.Generic.KeyValuePair<string, Card>(
            "card", new Card("4111", "123"));

        var text = RenderedValueText(ValueRenderer.RenderStructured(pair));

        Assert.Contains(RedactionPolicy.Marker, text);
        Assert.DoesNotContain("123", text);
    }

    [Fact]
    public void A_value_tuple_with_a_redacted_element_does_not_leak()
    {
        var tuple = ("card", new Card("4111", "123"));

        var rendered = ValueRenderer.Render(tuple);

        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("123", rendered);
    }

    [Fact]
    public void A_value_tuple_with_a_redacted_element_does_not_leak_in_structured_rendering()
    {
        var tuple = ("card", new Card("4111", "123"));

        var text = RenderedValueText(ValueRenderer.RenderStructured(tuple));

        Assert.Contains(RedactionPolicy.Marker, text);
        Assert.DoesNotContain("123", text);
    }

    [Fact]
    public void A_reference_tuple_with_a_redacted_element_does_not_leak()
    {
        var tuple = System.Tuple.Create("card", new Card("4111", "123"));

        var rendered = ValueRenderer.Render(tuple);

        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("123", rendered);
    }

    public readonly record struct CardStruct(string Last4, [property: NotTraced] string Cvv);

    [Fact]
    public void A_record_struct_with_a_redacted_property_does_not_leak()
    {
        var value = new CardStruct("4111", "123");

        var rendered = ValueRenderer.Render(value);

        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("123", rendered);
    }

    [Fact]
    public void A_record_struct_with_a_redacted_property_does_not_leak_in_structured_rendering()
    {
        var value = new CardStruct("4111", "123");

        var text = RenderedValueText(ValueRenderer.RenderStructured(value));

        Assert.Contains(RedactionPolicy.Marker, text);
        Assert.DoesNotContain("123", text);
    }

    [Fact]
    public void A_Lazy_of_a_redacted_record_does_not_leak_once_evaluated()
    {
        var lazy = new System.Lazy<Card>(() => new Card("4111", "123"));

        var rendered = ValueRenderer.Render(lazy);

        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("123", rendered);
    }

    [Fact]
    public void A_Lazy_of_a_redacted_record_does_not_leak_in_structured_rendering()
    {
        var lazy = new System.Lazy<Card>(() => new Card("4111", "123"));

        var text = RenderedValueText(ValueRenderer.RenderStructured(lazy));

        Assert.Contains(RedactionPolicy.Marker, text);
        Assert.DoesNotContain("123", text);
    }

    [Fact]
    public void Rendering_a_not_yet_created_Lazy_forces_its_factory_to_run()
    {
        // Recorded, not fixed (see item 18b in the tracking doc): unlike Task<T>, which has a
        // dedicated <pending> branch that never touches an incomplete task's
        // Result, Lazy<T> has no such branch — it reaches the renderer only
        // through the generic public-member path, so rendering it evaluates
        // it. This is a side-effect/purity concern (the annotations guide's
        // "keep members pure" contract), not a redaction leak: whatever the
        // factory returns is still rendered under full redaction rules, as
        // the tests above show.
        var evaluated = false;
        var lazy = new System.Lazy<Card>(() =>
        {
            evaluated = true;
            return new Card("4111", "123");
        });

        ValueRenderer.Render(lazy);

        Assert.True(evaluated);
    }

    private static string RenderedValueText(RenderedValue value)
    {
        return value switch
        {
            RenderedValue.StringVal s => s.Value,
            RenderedValue.ObjectVal o => string.Join(
                ", ",
                o.Fields.Select(kv => $"{kv.Key}={RenderedValueText(kv.Value)}")),
            RenderedValue.ListVal l => string.Join(
                ", ", l.Elements.Select(RenderedValueText)),
            _ => value.ToString() ?? "",
        };
    }
}
