// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 2 of the parity document's fuzzing list, and the reason the suite exists: the value
/// renderer over hostile object graphs, with the <b>redaction oracle</b>.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: Redaction is the one rendering rule whose failure mode is a leak rather than an ugly
/// line, and every instance of it found upstream (<c>Optional</c>, an atomic reference, a
/// standalone map entry, a template naming a redacted path) was found by a reader looking at
/// output, not by a test. The oracle here is mechanical instead: plant a random token behind
/// <c>[NotTraced]</c> anywhere in an arbitrary graph, render the graph, drive every emitter the
/// product ships, and assert the token is in no byte of any of them.
/// </para>
/// <para>
/// @llmNote The sentinel is fresh per case and checked whole <em>and</em> by prefix. A partial leak
/// through a truncating emitter is still a leak, and a fixed secret string would let one case's
/// passing output hide another's.
/// </para>
/// <para>
/// @edgeCase Only graph cases whose corpus entry says <c>payload: "secret-record"</c> carry a
/// sentinel. Cases that carry prose in an exception message deliberately do not: showing an
/// application's own exception message is the contract, so asserting containment there would pin
/// the opposite of it.
/// </para>
/// </remarks>
public class ValueRendererRedactionPropertyTests
{
    public static IEnumerable<object[]> SecretGraphCases() =>
        HostileCorpus.Graphs().Where(g => g.CarriesSecret).Select(g => new object[] { g });

    public static IEnumerable<object[]> RenderableGraphCases() =>
        HostileCorpus.Graphs().Where(g => !HostileGraphs.NeverRendered.Contains(g.Id))
            .Select(g => new object[] { g });

    [Theory]
    [MemberData(nameof(SecretGraphCases))]
    public void Every_hostile_graph_keeps_a_redacted_value_out_of_every_output(GraphCase graphCase) =>
        AssertContained(graphCase);

    [Theory]
    [MemberData(nameof(RenderableGraphCases))]
    public void Every_hostile_graph_renders_without_throwing_and_in_bounded_time(GraphCase graphCase)
    {
        var graph = HostileGraphs.Build(graphCase, Oracles.FreshSentinel());
        Oracles.WithinBudget("render " + graphCase.Id, () => ValueRenderer.Render(graph));
        Oracles.WithinBudget("renderStructured " + graphCase.Id, () => ValueRenderer.RenderStructured(graph));
    }

    /// <summary>
    /// A graph the renderer cannot walk must still say so in a way a reader can act on. Silence
    /// would satisfy containment too, which is why this exists beside it.
    /// </summary>
    [Fact]
    public void A_redacted_component_shows_the_marker_rather_than_nothing()
    {
        var sentinel = Oracles.FreshSentinel();

        var rendered = ValueRenderer.Render(HostileGraphs.BuildSecret(sentinel));

        Assert.Contains(RedactionPolicy.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, rendered, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(RenderableGraphCases))]
    public void Rendering_is_idempotent_for_every_hostile_graph(GraphCase graphCase)
    {
        var sentinel = Oracles.FreshSentinel();
        var graph = HostileGraphs.Build(graphCase, sentinel);
        Oracles.Idempotent("render " + graphCase.Id, () => ValueRenderer.Render(graph));
    }

    [Fact]
    public void Rendering_starts_no_background_thread()
    {
        foreach (var graphCase in RenderableGraphCases().Select(a => (GraphCase)a[0]))
            ValueRenderer.Render(HostileGraphs.Build(graphCase, Oracles.FreshSentinel()));

        Assert.True(Oracles.NoLibraryThreadLeft(), "rendering must start no background thread");
    }

    /// <summary>
    /// The bug class rather than the reported instance: redaction survives <em>any</em> stack of
    /// the wrappers the renderer opens, to any depth, in any order.
    /// </summary>
    [Property(Arbitrary = [typeof(SecurityArbitraries.WrapperStackArbitraries)])]
    public void A_redacted_component_survives_any_stack_of_wrappers(List<string> layers)
    {
        var graphCase = new GraphCase("generated", "generated stack", null, layers, null, null, null, null, "secret-record", 0);

        AssertContained(graphCase);
    }

    /// <summary>The same, through the structured path the OpenTelemetry-shaped exporter reads.</summary>
    [Property(Arbitrary = [typeof(SecurityArbitraries.WrapperStackArbitraries)])]
    public void The_structured_path_redacts_wherever_the_flat_path_does(List<string> layers)
    {
        var sentinel = Oracles.FreshSentinel();
        var graphCase = new GraphCase("generated", "generated stack", null, layers, null, null, null, null, "secret-record", 0);
        var graph = HostileGraphs.Build(graphCase, sentinel);

        var described = Formats.Describe(ValueRenderer.RenderStructured(graph));

        Assert.DoesNotContain(sentinel, described, StringComparison.Ordinal);
    }

    /// <summary>Depth alone must not defeat the guard: a chain is not a cycle, and neither may leak.</summary>
    [Property(MaxTest = 50)]
    public void A_redacted_component_survives_an_arbitrarily_deep_chain(PositiveInt depth)
    {
        var bounded = 1 + (depth.Get % 200);
        var graphCase = new GraphCase(
            "generated-depth", "generated chain", "repeatLayer", [], "holder", null, null, null, "secret-record", bounded);

        AssertContained(graphCase);
    }

    /// <summary>Width alone must not defeat it either: the payload may sit past any truncation limit.</summary>
    [Property(MaxTest = 30, Arbitrary = [typeof(SecurityArbitraries.ContainerArbitraries)])]
    public void A_redacted_component_survives_an_arbitrarily_wide_container(NonNegativeInt width, string container)
    {
        var bounded = width.Get % 500;
        var graphCase = new GraphCase(
            "generated-width", "generated width", "width", [], null, container, null, null, "secret-record", bounded);

        AssertContained(graphCase);
    }

    private static void AssertContained(GraphCase graphCase) => AssertContained(graphCase, Oracles.FreshSentinel());

    private static void AssertContained(GraphCase graphCase, string sentinel)
    {
        var graph = HostileGraphs.Build(graphCase, sentinel);
        var outputs = Oracles.WithinBudget("every output for " + graphCase.Id, () => EveryOutput(graph));

        Oracles.ContainsNoSentinel(outputs, sentinel);
        Oracles.BoundedSize(outputs);
    }

    /// <summary>Both renderer paths, then every emitter the product ships, over one graph.</summary>
    private static Dictionary<string, string> EveryOutput(object graph)
    {
        var flat = ValueRenderer.Render(graph);
        var outputs = new Dictionary<string, string>
        {
            ["renderer:value-flat"] = flat,
            ["renderer:value-structured"] = Formats.Describe(ValueRenderer.RenderStructured(graph)),
        };
        foreach (var (key, value) in Emitters.EveryOutput(Emitters.TreeOf(flat, flat)))
            outputs[key] = value;
        return outputs;
    }
}
