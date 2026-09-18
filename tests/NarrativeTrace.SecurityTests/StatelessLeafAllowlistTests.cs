// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Runtime.CompilerServices;
using System.Text.Json;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// A value with no readable members is not thereby a stateless leaf: only a type on the explicit
/// platform-leaf allowlist may render through its own <c>ToString</c>, and every other member-less
/// value renders as its type name with its state unread.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: "no members" used to be the whole test, and it is not proof of "nothing to hide". Two
/// shapes make that concrete, and both leaked every channel before this suite existed:
/// <see cref="JsonElement"/>, whose state is an index into a <see cref="JsonDocument"/> rather
/// than a field, and whose <c>ToString</c> returns the raw JSON text of the whole subtree it
/// points at; and an ordinary class holding its state in a static
/// <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by <c>this</c>, invisible to reflection
/// and freely readable from inside its own <c>ToString</c>.
/// </para>
/// <para>
/// @llmNote Each fixture's own <c>ToString</c> really does carry the sentinel (asserted first, as
/// a sanity check on the fixture) — a green suite here means the renderer chose not to call it.
/// </para>
/// <para>
/// @edgeCase Both rendering paths and the capture path are asserted for both shapes: a value
/// merely masked on the way out of one renderer has already travelled through the captured event
/// that reaches the audit path, the buffered consumer and every pipeline listener.
/// </para>
/// </remarks>
public class StatelessLeafAllowlistTests
{
    /// <summary>The probe's parameter name is innocuous — nothing about it invites redaction.</summary>
    public interface IPayloadProbe
    {
        string Handle(object data);
    }

    private sealed class PayloadProbe : IPayloadProbe
    {
        public string Handle(object data) => "ok";
    }

    /// <summary>
    /// A class with no instance field at all, whose state lives in a static side table keyed by
    /// the instance — reflection sees an empty type; its own <c>ToString</c> sees everything.
    /// </summary>
    private sealed class SideTableToken
    {
        private static readonly ConditionalWeakTable<SideTableToken, string> Side = new();

        public SideTableToken(string secret) => Side.Add(this, secret);

        /// <inheritdoc />
        public override string ToString() =>
            Side.TryGetValue(this, out var secret) ? $"token({secret})" : "token(unknown)";
    }

    [Fact]
    public void A_json_element_renders_as_its_type_name_and_never_as_the_document_it_points_at()
    {
        var sentinel = Oracles.FreshSentinel();
        using var document = JsonDocument.Parse(
            $"{{\"label\":\"visible\",\"secret\":\"LEAK-TOKEN-{sentinel}\"}}");

        AssertStateUnread(document.RootElement, sentinel, nameof(JsonElement));
    }

    [Fact]
    public void A_field_less_value_holding_its_state_in_a_side_table_renders_as_its_type_name()
    {
        var sentinel = Oracles.FreshSentinel();

        AssertStateUnread(new SideTableToken(sentinel), sentinel, nameof(SideTableToken));
    }

    /// <summary>
    /// Confirms the fixture really would leak if its <c>ToString</c> were trusted, then asserts
    /// every channel names the type and withholds the state: the flat path, the structured path,
    /// and the captured parameter the real proxy produces.
    /// </summary>
    private static void AssertStateUnread(object value, string sentinel, string typeName)
    {
        Assert.Contains(sentinel, value.ToString()!, StringComparison.Ordinal);

        AssertNames(ValueRenderer.Render(value), sentinel, typeName, "flat");
        AssertNames(
            Formats.Describe(ValueRenderer.RenderStructured(value)), sentinel, typeName, "structured");

        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IPayloadProbe>(new PayloadProbe(), context);
        proxy.Handle(value);

        var tree = context.CaptureTrace();
        AssertNames(
            tree.Roots[0].Signature.Parameters[0].RenderedValue, sentinel, typeName, "capture");

        foreach (var (emitter, output) in Emitters.EveryOutput(tree))
        {
            Assert.False(
                output.Contains(sentinel, StringComparison.Ordinal),
                $"{typeName} must not reach {emitter}");
        }
    }

    private static void AssertNames(string rendered, string sentinel, string typeName, string channel)
    {
        Assert.False(
            rendered.Contains(sentinel, StringComparison.Ordinal),
            $"{channel}: state was read — {rendered}");
        Assert.Contains(typeName, rendered, StringComparison.Ordinal);
    }
}
