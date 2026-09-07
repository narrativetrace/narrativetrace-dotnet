// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>Turns a declarative <c>tree-shapes.json</c> case into a live <see cref="TraceTree"/>.</summary>
/// <remarks>
/// The per-runtime half of the shared corpus (see the class remarks on <see cref="HostileGraphs"/>
/// for the same split): the JSON says <c>chain</c> or <c>cycle</c> and a size, this says what a
/// .NET <see cref="TraceNode"/> shaped that way actually looks like.
/// </remarks>
public static class HostileTreeShapes
{
    /// <summary>Builds the one-root forest <paramref name="shapeCase"/> describes.</summary>
    /// <exception cref="ArgumentException"><paramref name="shapeCase"/>'s <c>Kind</c> is not recognized.</exception>
    public static TraceTree Build(TreeShapeCase shapeCase)
    {
        var root = shapeCase.Kind switch
        {
            "chain" => Chain(shapeCase.N),
            "cycle" => Ring(shapeCase.N),
            _ => throw new ArgumentException($"unknown tree shape kind: {shapeCase.Kind}"),
        };
        return new TraceTree([root]);
    }

    // A chain `depth` links deep, with a single leaf at the very bottom.
    private static TraceNode Chain(int depth)
    {
        var node = Leaf("Bottom");
        for (var i = 0; i < depth; i++)
        {
            node = new TraceNode(
                new MethodSignature("Svc", "Link", []), new Returned(null), [node], 0);
        }

        return node;
    }

    // A genuine reference cycle: n nodes, each holding the next, the last holding the first.
    // n = 1 is a node that holds itself. Built with mutable backing lists (kept alive by the
    // closure, exposed only as the record's AsReadOnly() view) because TraceNode's constructor
    // has no other way to close a cycle - the same shape a hand-built or deserialized hostile
    // tree could produce.
    private static TraceNode Ring(int n)
    {
        var nodes = new TraceNode[n];
        var children = new List<TraceNode>[n];
        for (var i = 0; i < n; i++)
        {
            children[i] = [];
            nodes[i] = new TraceNode(
                new MethodSignature("Svc", "Node" + i, []), new Returned(null), children[i].AsReadOnly(), 0);
        }

        for (var i = 0; i < n; i++)
        {
            children[i].Add(nodes[(i + 1) % n]);
        }

        return nodes[0];
    }

    private static TraceNode Leaf(string name) =>
        new(new MethodSignature("Svc", name, []), new Returned(null), [], 0);
}
