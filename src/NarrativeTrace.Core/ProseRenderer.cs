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
        AppendTraceHeader(tree, sb);
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        // Roots are a sibling list like any other (see RenderChildren).
        RenderChildren(sb, TreeWalk.Bound(tree.Roots), 0, null);
        return sb.ToString();
    }

    /// <summary>
    /// Opens in this renderer's own voice — <c>"The trace bold elk soars: "</c>
    /// — before the first sentence (2026-09-13 ruling, item 4) *(since 0.1.5,
    /// unreleased)*. Silent when <see cref="TraceTree.TraceId"/> is
    /// <see cref="TraceId.Empty"/>: an empty tree gets no invented name.
    /// </summary>
    /// <remarks>
    /// This is the trace's OWN name, unrelated to the test-suite run name a
    /// test-framework integration threads through the console footer and
    /// manifest — see <see cref="RunIdentity"/>. Neither ever reaches the
    /// structural <c>.nt</c> text.
    /// </remarks>
    private static void AppendTraceHeader(TraceTree tree, StringBuilder sb)
    {
        if (tree.TraceId.IsEmpty)
        {
            return;
        }

        sb.Append("The trace ").Append(tree.TraceId.HumanName).Append(":\n\n");
    }

    private static void RenderNode(
        StringBuilder sb, CitedNode cited, int depth)
    {
        var (node, id) = cited;
        var indent = new string(' ', depth * 2);
        sb.Append(indent);
        AppendSentence(sb, node, id);
        sb.AppendLine();
        RenderChildren(sb, node.Children, depth + 1, id);
    }

    private static void RenderChildren(
        StringBuilder sb,
        IReadOnlyList<TraceNode> children,
        int depth, SpanRef? parent)
    {
        if (children.Count == 0)
        {
            return;
        }

        var ids = new SpanCursor(parent?.ToString());
        if (ChildSegment.AnyConcurrent(children))
        {
            RenderSegments(sb, children, depth, ids);
        }
        else
        {
            RenderInOrder(sb, children, depth, ids);
        }
    }

    private static void RenderSegments(
        StringBuilder sb, IReadOnlyList<TraceNode> children, int depth, SpanCursor ids)
    {
        var segments = ChildSegment.Partition(children);
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsConcurrent)
            {
                RenderConcurrent(
                    sb, segments[i], depth, ids);
            }
            else
            {
                RenderNode(
                    sb, new CitedNode(segments[i].Nodes[0], ids.NextRef()), depth);
            }
        }
    }

    private static void RenderConcurrent(
        StringBuilder sb, ChildSegment segment,
        int depth, SpanCursor ids)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            RenderFireAndForget(sb, segment, depth, ids.Next());
            return;
        }

        var indent = new string(' ', depth * 2);
        sb.Append(indent);
        sb.AppendLine(ConcurrentLabel(segment.Nodes));
        RenderInOrder(sb, SpanPath.InConcurrentOrder(segment.Nodes), depth + 1, ids);
    }

    /// <summary>
    /// The launch takes one span id, named in its sentence; the launched work
    /// is numbered under it in capture order.
    /// </summary>
    private static void RenderFireAndForget(
        StringBuilder sb, ChildSegment segment, int depth, string launchId)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent).Append("In the background (").Append(launchId).AppendLine("):");
        var body = FireAndForgetSection.Body(segment.Nodes);
        if (body.Count == 0)
        {
            var inner = new string(' ', (depth + 1) * 2);
            sb.Append(inner)
                .AppendLine("(launched, result not captured).");
        }
        else
        {
            RenderInOrder(sb, body, depth + 1, new SpanCursor(launchId));
        }
    }

    /// <summary>Renders <paramref name="nodes"/> in the order given, each taking the next id.</summary>
    private static void RenderInOrder(
        StringBuilder sb, IReadOnlyList<TraceNode> nodes, int depth, SpanCursor ids)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            RenderNode(sb, new CitedNode(nodes[i], ids.NextRef()), depth);
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

    /// <summary>
    /// One call as a sentence, naming its span by id in parentheses right
    /// after the call and its arguments: <c>The order service place order
    /// with id: `1` (#1) and returns …</c>.
    /// </summary>
    private static void AppendSentence(
        StringBuilder sb, TraceNode node, SpanRef id)
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
        id.AppendTo(sb.Append(" (")).Append(')');
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
