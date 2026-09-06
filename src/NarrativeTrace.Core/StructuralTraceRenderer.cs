// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Renders the AI-safe structural trace artifact (ADR-002): the
/// developer-authored shape of a scenario with zero runtime values.
/// </summary>
/// <remarks>
/// One artifact per test scenario (<c>.nt</c>) containing only code structure —
/// class, method, and parameter <em>names</em>, the call hierarchy, and outcome
/// <em>kinds</em>. No argument or return values, no exception messages, no
/// durations, no timestamps, no trace identifiers. Zero runtime values means
/// zero prompt-injection surface and zero PII, and the output is deterministic
/// byte-for-byte for identical behavior — the property that makes it the
/// approval-testing baseline (<c>.approved.nt</c>) and the cross-platform
/// conformance-fixture format. The format is normative and shared by every
/// port: see <c>documentation/structural-trace-format.md</c>.
/// </remarks>
public static class StructuralTraceRenderer
{
    /// <summary>
    /// Full artifact form: a <c>scenario:</c> header (the humanized test name —
    /// stable across runs) followed by the structural call flow. Nothing else:
    /// no result, no ids, no dates, so the file changes only when behavior
    /// changes.
    /// </summary>
    public static string RenderDocument(TraceTree tree, string scenario)
    {
        return "scenario: " + ControlEscape.Sanitize(scenario)
            + "\n\n" + Render(tree);
    }

    /// <summary>
    /// The call flow alone, without the <c>scenario:</c> header — for embedding
    /// a structural trace inside a larger document.
    /// </summary>
    /// <param name="tree">The finished trace; an empty tree yields an empty string.</param>
    /// <returns>
    /// The structural rendering: names, hierarchy and outcome kinds only. Never
    /// <see langword="null"/>, and byte-identical across runs of identical
    /// behavior — which is what makes it safe to commit as an approval baseline.
    /// </returns>
    /// <remarks>
    /// Use <see cref="RenderDocument"/> for the complete artifact. This overload
    /// omits the scenario header, so two different scenarios with the same call
    /// shape render identically — do not use it as a fixture on its own.
    /// </remarks>
    public static string Render(TraceTree tree)
    {
        var sb = new StringBuilder();

        // Roots go through the same partitioning as children: async work that
        // outlived its caller is a root, and its order is the scheduler's,
        // not the code's.
        //
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        RenderChildren(sb, TreeWalk.Bound(tree.Roots), 0);
        return sb.ToString();
    }

    private static void RenderNode(
        StringBuilder sb, TraceNode node, int depth)
    {
        var sig = node.Signature;
        AppendIndent(sb, depth);
        sb.Append("- ")
            .Append(ControlEscape.Sanitize(sig.ClassName))
            .Append('.')
            .Append(ControlEscape.Sanitize(sig.MethodName))
            .Append('(');
        AppendParameterNames(sb, sig.Parameters);
        sb.Append(')');
        RenderOutcomeKind(sb, node.Outcome);
        sb.Append('\n');
        RenderChildren(sb, node.Children, depth + 1);
    }

    private static void RenderChildren(
        StringBuilder sb, IReadOnlyList<TraceNode> children, int depth)
    {
        var segments = ChildSegment.Partition(children);
        for (var i = 0; i < segments.Count; i++)
        {
            RenderSegment(sb, segments[i], depth);
        }
    }

    private static void RenderSegment(
        StringBuilder sb, ChildSegment segment, int depth)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            RenderFireAndForget(sb, segment.Nodes, depth);
        }
        else if (segment.IsConcurrent)
        {
            RenderGroup(sb, MarkerFor(segment.Kind), segment.Nodes, depth);
        }
        else
        {
            RenderNode(sb, segment.Nodes[0], depth);
        }
    }

    private static string MarkerFor(ConcurrencyKind? kind)
    {
        return kind == ConcurrencyKind.Async ? "~ async" : "~ fork";
    }

    /// <summary>
    /// Concurrent groups render under a marker (<c>~ fork [n]</c>,
    /// <c>~ async [n]</c>) with members sorted by <c>Class.method</c> — capture
    /// order across threads is the scheduler's choice, not behavior, and this
    /// artifact must be byte-identical for identical behavior. Thread identity
    /// is runtime data and never appears.
    /// </summary>
    private static void RenderGroup(
        StringBuilder sb, string marker,
        IReadOnlyList<TraceNode> members, int depth)
    {
        AppendIndent(sb, depth);
        sb.Append(marker).Append(" [").Append(members.Count).Append("]\n");
        var sorted = SortedBySignature(members);
        for (var i = 0; i < sorted.Count; i++)
        {
            RenderNode(sb, sorted[i], depth + 1);
        }
    }

    /// <summary>
    /// Background work renders under a <c>~ fire-and-forget</c> marker followed
    /// by the launched calls. The synthetic launcher stub is layout scaffolding,
    /// not a call the developer wrote, so it never reaches the artifact.
    /// </summary>
    private static void RenderFireAndForget(
        StringBuilder sb, IReadOnlyList<TraceNode> segment, int depth)
    {
        AppendIndent(sb, depth);
        sb.Append("~ fire-and-forget\n");
        var body = FireAndForgetSection.Body(segment);
        for (var i = 0; i < body.Count; i++)
        {
            RenderNode(sb, body[i], depth + 1);
        }
    }

    private static IReadOnlyList<TraceNode> SortedBySignature(
        IReadOnlyList<TraceNode> nodes)
    {
        var sorted = new List<TraceNode>(nodes);
        sorted.Sort((a, b) => string.CompareOrdinal(
            SignatureKey(a), SignatureKey(b)));
        return sorted;
    }

    private static string SignatureKey(TraceNode node)
    {
        return node.Signature.ClassName + "." + node.Signature.MethodName;
    }

    private static void AppendIndent(StringBuilder sb, int depth)
    {
        sb.Append(' ', depth * 2);
    }

    private static void AppendParameterNames(
        StringBuilder sb, IReadOnlyList<ParameterCapture> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(ControlEscape.Sanitize(parameters[i].Name));
        }
    }

    private static void RenderOutcomeKind(
        StringBuilder sb, TraceOutcome outcome)
    {
        switch (outcome)
        {
            case Returned { RenderedValue: not null }:
                sb.Append(" → value");
                break;
            case Threw { Error: { } error }:
                sb.Append(" !! ").Append(
                    ControlEscape.Sanitize(error.GetType().Name));
                break;
            case Incomplete:
                sb.Append(" ?? incomplete");
                break;
        }
    }
}
