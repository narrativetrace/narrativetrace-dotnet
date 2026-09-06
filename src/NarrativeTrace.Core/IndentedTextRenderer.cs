// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Renders a trace as plain indented text, for terminals and log sinks that
/// render no markup.
/// </summary>
/// <remarks>
/// The same call hierarchy <see cref="MarkdownRenderer"/> produces, with
/// indentation carrying the nesting instead of Markdown bullets — so it stays
/// readable when pasted into a console, a log line, or a plain-text ticket.
/// Takes no options.
/// </remarks>
public static class IndentedTextRenderer
{
    /// <summary>Renders a trace as indented plain text.</summary>
    /// <param name="tree">The finished trace; an empty tree yields an empty string.</param>
    /// <returns>The text rendering, nesting shown by indentation. Never <see langword="null"/>.</returns>
    public static string Render(TraceTree tree)
    {
        var sb = new StringBuilder();
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        RenderNodes(sb, TreeWalk.Bound(tree.Roots), "");
        return sb.ToString();
    }

    private static void RenderNodes(
        StringBuilder sb,
        IReadOnlyList<TraceNode> nodes,
        string indent)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var isLast = i == nodes.Count - 1;
            var connector = isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
            var childIndent = indent
                + (isLast ? "    " : "\u2502   ");

            sb.Append(indent);
            sb.Append(connector);
            AppendSignature(sb, nodes[i].Signature);
            AppendOutcome(sb, nodes[i]);
            AppendDuration(sb, nodes[i]);
            sb.AppendLine();

            AppendNarration(sb, nodes[i].Signature, childIndent);
            RenderChildren(
                sb, nodes[i].Children, childIndent);
        }
    }

    private static void RenderChildren(
        StringBuilder sb,
        IReadOnlyList<TraceNode> children,
        string indent)
    {
        var segments = ChildSegment.Partition(children);
        var batch = new List<TraceNode>();
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsConcurrent)
            {
                FlushBatch(sb, batch, indent);
                RenderConcurrentSegment(
                    sb, segments[i], indent);
            }
            else
            {
                batch.AddRange(segments[i].Nodes);
            }
        }

        FlushBatch(sb, batch, indent);
    }

    private static void FlushBatch(
        StringBuilder sb, List<TraceNode> batch,
        string indent)
    {
        if (batch.Count == 0)
        {
            return;
        }

        RenderNodes(sb, batch, indent);
        batch.Clear();
    }

    private static void RenderConcurrentSegment(
        StringBuilder sb, ChildSegment segment,
        string indent)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            RenderFireAndForget(sb, segment, indent);
        }
        else
        {
            RenderForkJoin(sb, segment, indent);
        }
    }

    private static void RenderFireAndForget(
        StringBuilder sb, ChildSegment segment,
        string indent)
    {
        sb.Append(indent);
        sb.Append("\u2514\u2500\u2500 \u2933 fire-and-forget");
        AppendLauncherThread(sb, segment);
        sb.AppendLine();

        var body = FireAndForgetSection.Body(segment.Nodes);
        if (body.Count == 0)
        {
            sb.Append(indent)
                .AppendLine("        [launched, result not captured]");
            return;
        }

        var sorted = SortByClassName(body);
        for (var i = 0; i < sorted.Count; i++)
        {
            RenderForkMember(
                sb, sorted[i], indent + "    ");
        }
    }

    private static void AppendLauncherThread(
        StringBuilder sb, ChildSegment segment)
    {
        if (segment.Nodes.Count > 0
            && segment.Nodes[0].Concurrency is { } info)
        {
            sb.Append(" [thread: ").Append(info.ThreadId).Append(']');
        }
    }

    private static void RenderForkJoin(
        StringBuilder sb, ChildSegment segment,
        string indent)
    {
        sb.Append(indent);
        sb.Append("\u251c\u2500\u2500 \u2442 fork [");
        sb.Append(segment.Nodes.Count);
        sb.Append(" tasks]");
        AppendSequentialWarning(sb, segment.Nodes);
        sb.AppendLine();

        var sorted = SortByClassName(segment.Nodes);
        for (var i = 0; i < sorted.Count; i++)
        {
            RenderForkMember(
                sb, sorted[i], indent + "\u2502   ");
        }

        sb.Append(indent).Append("\u2514\u2500\u2500 \u2443 join");
        AppendJoinWallTime(sb, segment.Nodes);
        sb.AppendLine();
    }

    private static void AppendJoinWallTime(
        StringBuilder sb, IReadOnlyList<TraceNode> nodes)
    {
        long max = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            var ms = nodes[i].DurationTicks
                / TimeSpan.TicksPerMillisecond;
            if (ms > max)
            {
                max = ms;
            }
        }

        sb.Append(" \u2014 ").Append(max).Append("ms");
    }

    private static void RenderForkMember(
        StringBuilder sb, TraceNode node,
        string indent)
    {
        sb.Append(indent);
        sb.Append("\u21a6 ");
        AppendSignature(sb, node.Signature);
        AppendOutcome(sb, node);
        AppendDuration(sb, node);
        sb.AppendLine();
        RenderChildren(
            sb, node.Children, indent + "    ");
    }

    // Java renderOutcomeInline: this renderer targets console/failure output,
    // so exceptions render "!! Type: message | context" (ControlEscape-safe).
    private static void AppendOutcome(
        StringBuilder sb, TraceNode node)
    {
        switch (node.Outcome)
        {
            case Returned { RenderedValue: { } value }:
                sb.Append(" \u2192 ").Append(value);
                break;
            case Threw { Error: { } ex }:
                AppendException(sb, ex, node.Signature);
                break;
            case Incomplete:
                sb.Append(" \u23f3 in-flight");
                break;
        }
    }

    private static void AppendException(
        StringBuilder sb, Exception ex, MethodSignature sig)
    {
        sb.Append(" !! ")
            .Append(ControlEscape.Sanitize(ex.GetType().Name))
            .Append(": ")
            .Append(ExceptionMessage.Text(ex));
        if (sig.ErrorContext is { } context)
        {
            sb.Append(" | ").Append(ControlEscape.Sanitize(context));
        }
    }

    private static void AppendNarration(
        StringBuilder sb, MethodSignature sig, string childIndent)
    {
        if (sig.Narration is { } narration)
        {
            sb.Append(childIndent).Append("// ")
                .AppendLine(ControlEscape.Sanitize(narration));
        }
    }

    private static void AppendDuration(
        StringBuilder sb, TraceNode node)
    {
        if (node.DurationTicks <= 0)
        {
            return;
        }

        var ms = node.DurationTicks / TimeSpan.TicksPerMillisecond;
        sb.Append(" \u2014 ").Append(ms).Append("ms");
    }

    private static void AppendSequentialWarning(
        StringBuilder sb,
        IReadOnlyList<TraceNode> nodes)
    {
        var result =
            SequentialAsyncDetector.Analyze(nodes);
        if (!result.IsSequential)
        {
            return;
        }

        sb.Append(" [async, awaited sequentially]");
        var savings = result.TotalMs
            - result.ParallelizableMs;
        if (savings > 0)
        {
            sb.Append(" \u2014 could save ");
            sb.Append(savings);
            sb.Append("ms with Task.WhenAll");
        }
    }

    private static IReadOnlyList<TraceNode>
        SortByClassName(
            IReadOnlyList<TraceNode> nodes)
    {
        var sorted = new List<TraceNode>(nodes);
        sorted.Sort((a, b) =>
        {
            var cmp = string.Compare(
                a.Signature.ClassName,
                b.Signature.ClassName,
                StringComparison.Ordinal);
            return cmp != 0
                ? cmp
                : string.Compare(
                    a.Signature.MethodName,
                    b.Signature.MethodName,
                    StringComparison.Ordinal);
        });
        return sorted;
    }

    // Escapes className/methodName/parameter names the same way exception
    // messages and narration already are in this renderer (see
    // AppendException/AppendNarration): a class or parameter name is
    // ordinarily code-controlled, but reaches here from reflection or a
    // dynamically-typed caller, and the rendered VALUE beside it is already
    // ControlEscape-safe by construction (ValueRenderer.RenderString sanitizes
    // every string it renders) — leaving the metadata unescaped would be the
    // one raw seam in an otherwise-safe line.
    private static void AppendSignature(
        StringBuilder sb, MethodSignature sig)
    {
        sb.Append(ControlEscape.Sanitize(sig.ClassName));
        sb.Append('.');
        sb.Append(ControlEscape.Sanitize(sig.MethodName));
        sb.Append('(');
        for (var i = 0; i < sig.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(ControlEscape.Sanitize(sig.Parameters[i].Name));
            sb.Append(": ");
            sb.Append(sig.Parameters[i].DisplayValue());
        }

        sb.Append(')');
    }
}
