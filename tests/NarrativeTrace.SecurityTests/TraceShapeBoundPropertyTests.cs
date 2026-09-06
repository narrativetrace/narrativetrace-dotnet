// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Every renderer and exporter against <c>TraceNode</c> call-tree shapes themselves — depth and
/// cycles in the tree structure, as opposed to <see cref="ValueRendererRedactionPropertyTests"/>'s
/// object-graph shapes fed to the value renderer.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: mirrors the Java flagship's 2026-09-03 free-renderer depth-bound run
/// (<c>ai.narrativetrace.core.tree.TreeWalk</c>): a hand-built, replayed or deserialized
/// <see cref="TraceTree"/> can be cyclic or absurdly deep, and every renderer/exporter this port
/// ships must degrade with <see cref="TreeWalk.CycleMarker"/>/<see cref="TreeWalk.DepthLimitMarker"/>
/// rather than crash the process with an uncatchable <see cref="StackOverflowException"/> or hang.
/// </para>
/// <para>
/// @edgeCase This replays through <see cref="Emitters.Renderers"/> — every in-memory renderer,
/// the same set <see cref="InjectionContainmentPropertyTests"/> and
/// <see cref="OutputFormatPropertyTests"/> use for value-shaped hostility — rather than the
/// heavier <see cref="Emitters.EveryOutput"/> (which also writes every artifact to disk), and
/// computes it exactly once per shape rather than once per assertion: a ten-thousand-plus-node
/// chain is legitimately expensive (Oracle 2, "bounded time and size", is itself part of what this
/// proves), and paying for it more than once per shape pushed this file well past "Tier A: seconds"
/// (<c>documentation/security-testing.md</c>). The disk-writing path (<c>TraceArtifactWriter</c>)
/// is a thin wrapper over the same renderer calls and is already covered for value-shaped
/// hostility elsewhere in this suite. <c>ChapterExporter</c> is not part of either pipeline (it is
/// not one of the shipped artifact writers) and is pinned separately, in
/// <c>NarrativeTrace.Core.Tests.ChapterExporterTests</c>.
/// </para>
/// </remarks>
public class TraceShapeBoundPropertyTests
{
    public static IEnumerable<object[]> Shapes() =>
        HostileCorpus.TreeShapes().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Every_renderer_survives_the_shape_and_marks_where_it_stopped(TreeShapeCase shapeCase)
    {
        var tree = HostileTreeShapes.Build(shapeCase);

        var outputs = Emitters.Renderers(tree);

        Assert.NotEmpty(outputs);
        if (shapeCase.Kind == "cycle")
        {
            AssertAnyContains(outputs, TreeWalk.CycleMarker);
        }

        if (shapeCase.Kind == "chain" && shapeCase.N > TreeWalk.MaxDepth)
        {
            AssertAnyContains(outputs, TreeWalk.DepthLimitMarker);
        }
    }

    private static void AssertAnyContains(
        IReadOnlyDictionary<string, string> outputs, string marker)
    {
        Assert.Contains(
            outputs,
            kv => kv.Value.Contains(marker, StringComparison.Ordinal));
    }
}
