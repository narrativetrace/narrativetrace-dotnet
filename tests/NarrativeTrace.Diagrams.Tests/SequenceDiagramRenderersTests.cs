// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

public class SequenceDiagramRenderersTests
{
    private static TraceTree SampleTree()
    {
        var child = new TraceNode(
            new MethodSignature("Repo", "Save",
                [new ParameterCapture("id", "1", false)]),
            new Returned("\"ok\""), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []),
            new Returned(null), [child], 0);
        return new TraceTree([root]);
    }

    [Fact]
    public void Mermaid_matches_the_renderer_it_wraps()
    {
        var tree = SampleTree();

        var viaTable = SequenceDiagramRenderers.Mermaid(tree);
        var direct = MermaidSequenceRenderer.Render(tree);

        Assert.Equal(direct, viaTable);
    }

    [Fact]
    public void PlantUml_matches_the_renderer_it_wraps()
    {
        var tree = SampleTree();

        var viaTable = SequenceDiagramRenderers.PlantUml(tree);
        var direct = PlantUmlSequenceRenderer.Render(tree);

        Assert.Equal(direct, viaTable);
    }
}
