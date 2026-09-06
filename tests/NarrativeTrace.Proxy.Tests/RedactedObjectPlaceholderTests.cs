// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// A template placeholder naming a whole object must not leak the object's own hidden members
/// through its <c>ToString()</c>.
/// </summary>
/// <remarks>
/// A java security fuzz suite finding, "the most serious one," mirrored here.
/// <c>{card.cvv}</c> (a path into an object) was already redacted — <see cref="NarrationResolver"/>'s
/// <c>PathIsRedacted</c> covers it — but <c>{card}</c> (the whole object, the more common
/// placeholder spelling) was not: <c>Card</c>'s own <c>ToString()</c> prints every component,
/// <c>[NotTraced]</c> included, and knows nothing about redaction. The owner's invariant — naming a
/// path never weakens the rules that apply to the value directly — says nothing about paths in
/// particular, so the same rule has to hold for the whole-object spelling.
/// </remarks>
public class RedactedObjectPlaceholderTests
{
    [Fact]
    public void A_placeholder_naming_the_whole_object_redacts_its_hidden_component()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IChargeService>(new ChargeService(), ctx);

        proxy.Charge(new Card("4111", "123"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.DoesNotContain("123", narration, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, narration, StringComparison.Ordinal);
    }

    /// <summary>A component nested one level inside the named object must redact too.</summary>
    [Fact]
    public void A_placeholder_naming_an_object_redacts_a_nested_hidden_component()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IChargeService>(new ChargeService(), ctx);

        proxy.Bill(new Order("order-42", new Card("4111", "123")));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.DoesNotContain("123", narration, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, narration, StringComparison.Ordinal);
    }

    /// <summary>
    /// An owner-approved trade-off (mirroring the Java flagship's own ruling): a non-scalar
    /// placeholder now renders structurally through <c>ValueRenderer</c> even when nothing is
    /// hidden, rather than keeping its own hand-written <c>ToString()</c> byte for byte. Deciding
    /// "safe to skip the renderer" from the value's shape alone is exactly the reasoning this fix
    /// disproved — there is no version of that shortcut that isn't eventually unsound. This also
    /// ends a divergence: <see cref="Money"/> passed as a captured <em>argument</em> already
    /// rendered this way; only the whole-object placeholder spelling used to differ.
    /// </summary>
    [Fact]
    public void An_object_with_nothing_hidden_now_renders_structurally()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IChargeService>(new ChargeService(), ctx);

        proxy.Pay(new Money("EUR", "10.00"));

        Assert.Equal(
            "paying Money(Currency: \"EUR\", Amount: \"10.00\")",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void A_scalar_placeholder_is_still_unquoted_and_plain()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IChargeService>(new ChargeService(), ctx);

        proxy.Label("order-42");

        Assert.Equal(
            "labeling order-42",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    /// <summary>
    /// The same shape: the redacted component sits past <see cref="RenderOptions"/>'s five-field
    /// cap, so <c>ValueRenderer</c>'s safe rendering truncates it away and carries no redaction
    /// marker at all — "no marker" must not be read as "nothing is hidden". Unlike the
    /// path-into-a-redacted-member cases, the assertion here is absence of the sentinel only: the
    /// field is truncated away, not shown behind a marker, so there is nothing that would make the
    /// marker appear.
    /// </summary>
    [Fact]
    public void A_placeholder_naming_an_object_does_not_leak_a_component_past_the_field_cap()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IChargeService>(new ChargeService(), ctx);

        proxy.Widen(new Wide("1", "2", "3", "4", "5", "sentinel-six"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.DoesNotContain("sentinel-six", narration, StringComparison.Ordinal);
    }

    /// <summary>
    /// The same leak reached through the other cap: a redacted leaf nested deeper than
    /// <see cref="RenderOptions"/>'s depth cap, so the walk stops before ever reaching it — again,
    /// absence of the sentinel only; see the field-cap case above for why no marker is expected.
    /// </summary>
    [Fact]
    public void A_placeholder_naming_an_object_does_not_leak_a_leaf_below_the_depth_cap()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IChargeService>(new ChargeService(), ctx);

        proxy.Follow(Chain("sentinel-leaf"));

        var narration = ctx.CaptureTrace().Roots[0].Signature.Narration;
        Assert.NotNull(narration);
        Assert.DoesNotContain("sentinel-leaf", narration, StringComparison.Ordinal);
    }

    /// <summary>Six links deep, past the default <see cref="RenderOptions.MaxDepth"/> of four.</summary>
    private static Link Chain(string sentinel)
    {
        object link = new Card("4111", sentinel);
        for (var i = 0; i < 6; i++)
        {
            link = new Link(link);
        }

        return (Link)link;
    }

    public interface IChargeService
    {
        [Narrated("charging {card}")]
        void Charge(Card card);

        [Narrated("billing {order}")]
        void Bill(Order order);

        [Narrated("paying {money}")]
        void Pay(Money money);

        [Narrated("labeling {value}")]
        void Label(string value);

        [Narrated("widening {wide}")]
        void Widen(Wide wide);

        [Narrated("following {chain}")]
        void Follow(Link chain);
    }

    private sealed class ChargeService : IChargeService
    {
        public void Charge(Card card) { }

        public void Bill(Order order) { }

        public void Pay(Money money) { }

        public void Label(string value) { }

        public void Widen(Wide wide) { }

        public void Follow(Link chain) { }
    }

    /// <summary>The dogfood shape: a payment card whose verification code is annotated out of every output.</summary>
    public sealed record Card(string Number, [property: NotTraced] string Cvv);

    /// <summary>A card one level down, so a whole-object placeholder can name an outer object with a redacted grandchild.</summary>
    public sealed record Order(string Id, Card Card);

    /// <summary>An author's own plain <c>ToString()</c> — narrates as written, not as a field dump.</summary>
    public sealed record Money(string Currency, string Amount)
    {
        /// <inheritdoc />
        public override string ToString() => $"{Currency} {Amount}";
    }

    /// <summary>
    /// A record whose redacted component sits past <see cref="RenderOptions"/>'s five-field cap, so
    /// the safe rendering truncates the component away and carries no redaction marker at all.
    /// </summary>
    public sealed record Wide(
        string One, string Two, string Three, string Four, string Five,
        [property: NotTraced] string Six);

    /// <summary>One link of a chain longer than <see cref="RenderOptions"/>'s depth cap.</summary>
    public sealed record Link(object Next);
}
