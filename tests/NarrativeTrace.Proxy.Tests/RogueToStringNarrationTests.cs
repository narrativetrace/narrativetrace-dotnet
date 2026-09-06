// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// A parameter whose <c>ToString()</c> throws must never break the traced call.
/// <para>
/// <c>ValueRenderer.SafeToString</c> already treats a rogue <c>ToString()</c> as a known
/// hazard and degrades to a type marker. These tests hold the narration path to the same
/// contract: observability failure must never become application failure.
/// </para>
/// </summary>
public class RogueToStringNarrationTests
{
    [Fact]
    public void Rogue_ToString_in_a_narration_placeholder_does_not_break_the_call()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueNarrated>(
            new RogueNarratedService(), ctx);

        proxy.Handle(new RogueToString());

        Assert.Equal(
            "Processing <RogueToString>",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void Rogue_ToString_behind_a_property_placeholder_degrades_to_a_marker()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueNarrated>(
            new RogueNarratedService(), ctx);

        proxy.Inspect(new Holder(new RogueToString()));

        Assert.Equal(
            "Processing <RogueToString>",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    // A throwing GETTER and a throwing ToString() are different failures and
    // must stay distinguishable: the getter leaves the {obj.prop} literal
    // visible (a template typo looks the same as a broken accessor and both
    // want the placeholder back), while a value that resolved fine but cannot
    // render degrades to the type marker.
    [Fact]
    public void A_throwing_getter_still_preserves_the_literal_placeholder()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueNarrated>(
            new RogueNarratedService(), ctx);

        proxy.Probe(new ThrowingGetter());

        Assert.Equal(
            "Probing {probe.Boom}",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    // A java-mirrored fix changed this test's shape: the IFormattable fast path now applies only to scalar
    // values (NarrationResolver.PlainValue), because trying it for a non-scalar was the same
    // "safe to skip the renderer" shortcut that leaked a redacted value elsewhere. A non-scalar
    // IFormattable type therefore renders through ValueRenderer like any other object — nothing
    // ever calls its explicit ToString(format, provider), so a rogue implementation of only that
    // method no longer has anywhere to throw from, and this pins the resulting narration against
    // ValueRenderer's own (independently guarded) rendering of the same instance.
    [Fact]
    public void Rogue_IFormattable_renders_through_the_value_renderer_instead_of_its_own_format_method()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueFormattable>(
            new RogueFormattableService(), ctx);
        var payload = new RogueFormattable();

        proxy.Handle(payload);

        Assert.Equal(
            $"Processing {ValueRenderer.Render(payload)}",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void A_ToString_returning_null_degrades_to_a_marker()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueNarrated>(
            new RogueNarratedService(), ctx);

        proxy.Blank(new NullToString());

        Assert.Equal(
            "Processing <NullToString>",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    // The worst manifestation: ResolveErrorContext runs inside the catch that
    // rethrows the business exception, so a rogue ToString() there REPLACED
    // the caller's real failure with the rendering failure. A production error
    // would vanish and be reported as a template crash.
    [Fact]
    public void Rogue_ToString_in_an_OnError_template_does_not_mask_the_real_exception()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueOnError>(
            new RogueOnErrorService(), ctx);

        var thrown = Assert.Throws<InvalidOperationException>(
            () => proxy.Fail(new RogueToString()));

        Assert.Equal("the real failure", thrown.Message);
    }

    [Fact]
    public void Rogue_ToString_in_an_OnError_template_still_records_the_context()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueOnError>(
            new RogueOnErrorService(), ctx);

        Assert.Throws<InvalidOperationException>(
            () => proxy.Fail(new RogueToString()));

        Assert.Equal(
            "Failed while processing <RogueToString>",
            ctx.CaptureTrace().Roots[0].Signature.ErrorContext);
    }

    [Fact]
    public void Other_placeholders_still_resolve_around_a_rogue_value()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IRogueNarrated>(
            new RogueNarratedService(), ctx);

        proxy.HandleWithId(7, new RogueToString());

        Assert.Equal(
            "7: <RogueToString>",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    public interface IRogueNarrated
    {
        [Narrated("Processing {payload}")]
        void Handle(RogueToString payload);

        [Narrated("Processing {holder.Value}")]
        void Inspect(Holder holder);

        [Narrated("Probing {probe.Boom}")]
        void Probe(ThrowingGetter probe);

        [Narrated("Processing {payload}")]
        void Blank(NullToString payload);

        [Narrated("{id}: {payload}")]
        void HandleWithId(int id, RogueToString payload);
    }

    public interface IRogueFormattable
    {
        [Narrated("Processing {payload}")]
        void Handle(RogueFormattable payload);
    }

    public interface IRogueOnError
    {
        [OnError("Failed while processing {payload}")]
        void Fail(RogueToString payload);
    }

    /// <summary>A value whose rendering blows up — a lazy proxy over a dead session, say.</summary>
    public sealed class RogueToString
    {
        // S3877 says do not throw from ToString(). That is exactly the
        // application bug under test — real code does it by accident, and the
        // narration path has to survive it. Suppressed, not fixed.
#pragma warning disable S3877
        public override string ToString() =>
            throw new InvalidOperationException("ToString exploded");
#pragma warning restore S3877
    }

    public sealed class Holder(object value)
    {
        public object Value { get; } = value;
    }

    /// <summary>A getter that throws — a broken accessor, not a broken value.</summary>
    public sealed class ThrowingGetter
    {
        // Must stay an INSTANCE property: NarrationResolver looks properties
        // up with BindingFlags.Public | BindingFlags.Instance, so a static
        // Boom would be invisible and the test would pass on the "unknown
        // property" path instead of the throwing-getter path. Reading the
        // field is what keeps S2325 from demanding it be made static.
        private readonly string _failure = "getter blew up";

        public string Boom =>
            throw new InvalidOperationException(_failure);
    }

    /// <summary>The .NET analogue of Java's null-returning toString().</summary>
    public sealed class NullToString
    {
        public override string ToString() => null!;
    }

    /// <summary>
    /// PlainValue tries IFormattable before ToString(), so the guard has to cover that arm too.
    /// </summary>
    public sealed class RogueFormattable : IFormattable
    {
        public string ToString(
            string? format, IFormatProvider? formatProvider) =>
            throw new InvalidOperationException("IFormattable exploded");
    }

    private sealed class RogueNarratedService : IRogueNarrated
    {
        public void Handle(RogueToString payload) { }

        public void Inspect(Holder holder) { }

        public void Probe(ThrowingGetter probe) { }

        public void Blank(NullToString payload) { }

        public void HandleWithId(int id, RogueToString payload) { }
    }

    private sealed class RogueFormattableService : IRogueFormattable
    {
        public void Handle(RogueFormattable payload) { }
    }

    private sealed class RogueOnErrorService : IRogueOnError
    {
        public void Fail(RogueToString payload) =>
            throw new InvalidOperationException("the real failure");
    }
}
