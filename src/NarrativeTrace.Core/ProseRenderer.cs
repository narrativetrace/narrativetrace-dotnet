// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Renders a trace as flowing narrative prose, for reading rather than scanning.
/// </summary>
/// <remarks>
/// Trades structure for readability: the call hierarchy becomes sentences, which
/// is the friendliest form for someone unfamiliar with the code and the least
/// useful for locating a specific call. It takes no options — there is nothing
/// to tune — so where <see cref="MarkdownRenderer"/> is configurable this is
/// deliberately not.
/// </remarks>
public static class ProseRenderer
{
    /// <summary>Renders a trace as narrative prose.</summary>
    /// <param name="tree">The finished trace; an empty tree yields an empty string.</param>
    /// <returns>The prose rendering. Never <see langword="null"/>.</returns>
    public static string Render(TraceTree tree)
    {
        var sb = new StringBuilder();
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        RenderNodes(sb, TreeWalk.Bound(tree.Roots), 0);
        return sb.ToString();
    }

    private static void RenderNodes(
        StringBuilder sb,
        IReadOnlyList<TraceNode> nodes,
        int depth)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            RenderNode(sb, nodes[i], depth);
        }
    }

    private static void RenderNode(
        StringBuilder sb, TraceNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent);
        AppendSentence(sb, node);
        sb.AppendLine();
        RenderChildren(sb, node.Children, depth + 1);
    }

    private static void RenderChildren(
        StringBuilder sb,
        IReadOnlyList<TraceNode> children,
        int depth)
    {
        var segments = ChildSegment.Partition(children);
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsConcurrent)
            {
                RenderConcurrent(
                    sb, segments[i], depth);
            }
            else
            {
                RenderNodes(
                    sb, segments[i].Nodes, depth);
            }
        }
    }

    private static void RenderConcurrent(
        StringBuilder sb, ChildSegment segment,
        int depth)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            RenderFireAndForget(sb, segment, depth);
            return;
        }

        var indent = new string(' ', depth * 2);
        sb.Append(indent);
        sb.AppendLine(ConcurrentLabel(segment.Nodes));
        RenderNodes(sb, segment.Nodes, depth + 1);
    }

    private static void RenderFireAndForget(
        StringBuilder sb, ChildSegment segment, int depth)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent).AppendLine("In the background:");
        var body = FireAndForgetSection.Body(segment.Nodes);
        if (body.Count == 0)
        {
            var inner = new string(' ', (depth + 1) * 2);
            sb.Append(inner)
                .AppendLine("(launched, result not captured).");
        }
        else
        {
            RenderNodes(sb, body, depth + 1);
        }
    }

    private static string ConcurrentLabel(
        IReadOnlyList<TraceNode> nodes)
    {
        var result =
            SequentialAsyncDetector.Analyze(nodes);
        if (!result.IsSequential)
        {
            return "Concurrently:";
        }

        var savings = result.TotalMs
            - result.ParallelizableMs;
        return savings > 0
            ? $"Concurrently (awaited sequentially"
              + $" \u2014 could save {savings}ms):"
            : "Concurrently (awaited sequentially):";
    }

    private static void AppendSentence(
        StringBuilder sb, TraceNode node)
    {
        var className = HumanizeClassName(
            node.Signature.ClassName);
        var methodName = CamelCaseSplitter.ToPhrase(
            node.Signature.MethodName);

        sb.Append("The ");
        sb.Append(ControlEscape.Sanitize(className));
        sb.Append(' ');
        sb.Append(ControlEscape.Sanitize(methodName));
        AppendParameters(sb, node.Signature.Parameters);
        AppendOutcome(sb, node);
        AppendNarration(sb, node);
    }

    private static void AppendNarration(
        StringBuilder sb, TraceNode node)
    {
        if (node.Outcome is Threw)
        {
            return;
        }

        if (node.Signature.Narration is { } narration)
        {
            sb.Append(" — ");
            sb.Append(ControlEscape.Sanitize(narration));
        }
    }

    private static string HumanizeClassName(string name)
    {
        return CamelCaseSplitter.ToPhrase(name);
    }

    private static void AppendParameters(
        StringBuilder sb,
        IReadOnlyList<ParameterCapture> parameters)
    {
        if (parameters.Count == 0)
        {
            return;
        }

        sb.Append(" with ");
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(ControlEscape.Sanitize(parameters[i].Name));
            sb.Append(": `");
            sb.Append(parameters[i].DisplayValue());
            sb.Append('`');
        }
    }

    private static void AppendOutcome(
        StringBuilder sb, TraceNode node)
    {
        switch (node.Outcome)
        {
            case Returned { RenderedValue: { } value }:
                sb.Append(" and returns ");
                sb.Append(value);
                break;
            case Threw { Error: { } ex }:
                sb.Append(" but throws ");
                sb.Append(ControlEscape.Sanitize(ex.GetType().Name));
                sb.Append(": ");
                sb.Append(ExceptionMessage.Text(ex));
                AppendErrorContext(sb, node.Signature);
                break;
            case Incomplete:
                sb.Append(" but is incomplete");
                break;
        }
    }

    private static void AppendErrorContext(
        StringBuilder sb, MethodSignature sig)
    {
        if (sig.ErrorContext is { } context)
        {
            sb.Append(" (");
            sb.Append(ControlEscape.Sanitize(context));
            sb.Append(')');
        }
    }
}
