// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Every <c>graphs.json</c> row, routed through BOTH rendering paths, must reach the same
/// enumeration verdict.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the rendering rule ("rendering reads state, never runs behaviour", owner ruling
/// 2026-09-17) is a property of the renderer, not of one of its two output encodings.
/// <see cref="ValueRenderer.Render"/> and <see cref="ValueRenderer.RenderStructured"/> already
/// switch over the SAME origin gate for enumeration — <c>RenderEnumerableByOrigin</c> and
/// <c>RenderStructuredEnumerableByOrigin</c> both ask <c>HasNarrativeElements</c>,
/// <see cref="PlatformTypes.IsPlatformDefined"/> and <see cref="PlatformTypes.ListAncestor"/> in
/// the same order, and the member-less-object fallback shares one
/// <see cref="PlatformTypes.IsStatelessLeaf"/> decision too —
/// this test is what keeps that true: whatever the corpus throws at either path, both must agree
/// on whether the value's elements were walked, and how.
/// </para>
/// <para>
/// @llmNote The witness is deliberately COARSE — elements, entries, or not enumerated at all —
/// because that, and only that, is what the shared origin decision decides. The two paths encode
/// the same verdict differently on purpose (a flat <c>[...]</c>/<c>{...}</c> string against a
/// typed <see cref="RenderedValue.ListVal"/>/<see cref="RenderedValue.ObjectVal"/>), so asserting
/// the renderings equal would pin the encodings rather than the routing.
/// </para>
/// <para>
/// @edgeCase An object or record dump always starts with its type name — never with <c>[</c> or
/// <c>{</c> — so the flat text's leading character alone distinguishes an actual element/entry
/// walk (<c>RenderEnumerable</c>/<c>RenderListAncestorState</c> for <c>[...]</c>,
/// <c>RenderDictionary</c>/<c>RenderHashMapAncestorState</c> for the bare, untyped <c>{k=v}</c>)
/// from a plain object dump (always <c>TypeName{...}</c> or the record form <c>TypeName(...)</c>).
/// The structured twin uses the same distinction on the typed tree: a bare
/// <see cref="RenderedValue.ObjectVal"/> named <c>"Map"</c> is the dictionary form; any other
/// <see cref="RenderedValue.ObjectVal"/> is an object/record dump. This witness does not detect a
/// value whose element walk happened but whose result then degraded to an error marker (that is
/// what the counter-based corpus replay tests pin) — it only detects a path ROUTING a value
/// differently than the other, which is this rule's own property.
/// </para>
/// </remarks>
public sealed class RenderingPathRoutingParityTests
{
    private enum Route
    {
        Elements,
        Entries,
        NotEnumerated,
    }

    [Fact]
    public void Both_rendering_paths_route_every_corpus_row_to_the_same_enumeration_verdict()
    {
        var mismatches = new List<string>();
        foreach (var graphCase in HostileCorpus.Graphs())
        {
            var graph = HostileGraphs.Build(graphCase, "sentinel-" + graphCase.Id);

            var flat = FlatRoute(ValueRenderer.Render(graph));
            var structured = StructuredRoute(ValueRenderer.RenderStructured(graph));

            if (flat != structured)
            {
                mismatches.Add($"{graphCase.Id}: flat={flat}, structured={structured}");
            }
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    private static Route FlatRoute(string text)
    {
        if (text.StartsWith('['))
        {
            return Route.Elements;
        }

        return text.StartsWith('{') ? Route.Entries : Route.NotEnumerated;
    }

    private static Route StructuredRoute(RenderedValue value)
    {
        return value switch
        {
            RenderedValue.ListVal => Route.Elements,
            RenderedValue.ObjectVal { TypeName: "Map" } => Route.Entries,
            _ => Route.NotEnumerated,
        };
    }
}
