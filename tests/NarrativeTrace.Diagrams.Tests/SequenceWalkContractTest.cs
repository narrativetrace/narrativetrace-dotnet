// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

/// <summary>
/// The invariant <see cref="SequenceWalk"/> gives every grammar for free, pinned directly: over the
/// same hostile trees (a chain past <see cref="TreeWalk.MaxDepth"/>, a genuine reference cycle, wide
/// siblings, and every outcome kind mixed together), the Mermaid grammar, the plain PlantUML grammar
/// and the lifeline PlantUML grammar each receive exactly one call-arrow hook and exactly one
/// outcome hook (return, throw, or incomplete) per node in the bounded tree — so the two shipped
/// renderers cannot silently diverge in how many arrows they draw.
/// </summary>
public class SequenceWalkContractTest
{
    [Fact]
    public void Deep_chain_gets_one_call_arrow_and_one_outcome_per_node() =>
        AssertContract(Chain(50));

    [Fact]
    public void Chain_past_the_depth_limit_gets_one_call_arrow_and_one_outcome_per_node() =>
        AssertContract(Chain(TreeWalk.MaxDepth + 5));

    [Fact]
    public void Reference_cycle_gets_one_call_arrow_and_one_outcome_per_node() =>
        AssertContract(Cycle(3));

    [Fact]
    public void Self_cycle_gets_one_call_arrow_and_one_outcome_per_node() =>
        AssertContract(Cycle(1));

    [Fact]
    public void Wide_siblings_get_one_call_arrow_and_one_outcome_per_node() =>
        AssertContract(Wide(25));

    [Fact]
    public void Mixed_outcomes_get_one_call_arrow_and_one_outcome_per_node() =>
        AssertContract(MixedOutcomes());

    private static void AssertContract(TraceTree tree)
    {
        var roots = TreeWalk.Bound(tree.Roots);
        var nodeCount = CountNodes(roots);

        AssertOneArrowAndOneOutcomePerNode(roots, MermaidSequenceGrammar.Instance, nodeCount);
        AssertOneArrowAndOneOutcomePerNode(
            roots, new PlantUmlSequenceGrammar(includeLifelines: false), nodeCount);
        AssertOneArrowAndOneOutcomePerNode(
            roots, new PlantUmlSequenceGrammar(includeLifelines: true), nodeCount);
    }

    private static void AssertOneArrowAndOneOutcomePerNode(
        IReadOnlyList<TraceNode> roots, ISequenceGrammar grammar, int nodeCount)
    {
        var counting = new CountingGrammar(grammar);
        var sb = new StringBuilder();
        SequenceWalk.RenderCalls(roots, counting, className => DiagramLabel.Identifier(className), sb);

        Assert.Equal(nodeCount, counting.CallArrows);
        Assert.Equal(nodeCount, counting.Outcomes);
    }

    private static int CountNodes(IReadOnlyList<TraceNode> nodes)
    {
        var count = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            count++;
            count += CountNodes(nodes[i].Children);
        }

        return count;
    }

    // A chain `depth` links deep, with a single leaf at the very bottom — the same shape a deeply
    // recursive business method produces.
    private static TraceTree Chain(int depth)
    {
        var node = Leaf("Bottom");
        for (var i = 0; i < depth; i++)
        {
            node = new TraceNode(new MethodSignature("Svc", "Link", []), new Returned(null), [node], 0);
        }

        return new TraceTree([node]);
    }

    // A genuine reference cycle: n nodes, each holding the next, the last holding the first. n = 1
    // is a node that holds itself. Built with mutable backing lists (kept alive by the closure,
    // exposed only as the record's AsReadOnly() view) because TraceNode's constructor has no other
    // way to close a cycle — the same shape a hand-built or deserialized hostile tree could produce.
    private static TraceTree Cycle(int n)
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

        return new TraceTree([nodes[0]]);
    }

    private static TraceTree Wide(int siblingCount)
    {
        var children = new List<TraceNode>();
        for (var i = 0; i < siblingCount; i++)
        {
            children.Add(Leaf("Sibling" + i));
        }

        var root = new TraceNode(new MethodSignature("Svc", "FanOut", []), new Returned(null), children, 0);
        return new TraceTree([root]);
    }

    private static TraceTree MixedOutcomes()
    {
        var returned = Leaf("Ok");
        var threw = new TraceNode(
            new MethodSignature("Svc", "Fails", []), new Threw(new InvalidOperationException("x")), [], 0);
        var incomplete = new TraceNode(
            new MethodSignature("Svc", "StillRunning", []), new Incomplete(), [], 0);
        var root = new TraceNode(
            new MethodSignature("Svc", "Run", []), new Returned(null), [returned, threw, incomplete], 0);
        return new TraceTree([root]);
    }

    private static TraceNode Leaf(string name) =>
        new(new MethodSignature("Svc", name, []), new Returned(null), [], 0);

    private sealed class CountingGrammar(ISequenceGrammar inner) : ISequenceGrammar
    {
        public int CallArrows { get; private set; }

        public int Outcomes { get; private set; }

        public string Header => inner.Header;

        public string Footer => inner.Footer;

        public string Participant(DiagramLabel alias, DiagramLabel displayName) =>
            inner.Participant(alias, displayName);

        public string CallArrow(DiagramLabel caller, DiagramLabel target, DiagramLabel signature)
        {
            CallArrows++;
            return inner.CallArrow(caller, target, signature);
        }

        public string ReturnArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel message)
        {
            Outcomes++;
            return inner.ReturnArrow(target, caller, message);
        }

        public string ThrowArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel exceptionType)
        {
            Outcomes++;
            return inner.ThrowArrow(target, caller, exceptionType);
        }

        public string Incomplete(DiagramLabel target)
        {
            Outcomes++;
            return inner.Incomplete(target);
        }
    }
}
