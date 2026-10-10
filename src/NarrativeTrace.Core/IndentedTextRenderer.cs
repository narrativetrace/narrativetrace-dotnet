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
        AppendTraceHeader(tree, sb);
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        // Roots are a sibling list like any other: a fork, an async group or a fire-and-forget launch
        // at the root renders as one, and its ids are the structural trace's.
        RenderChildren(sb, TreeWalk.Bound(tree.Roots), "", null);
        return sb.ToString();
    }

    /// <summary>
    /// Opens with <c>trace: bold elk soars (a1b2c3d)</c> — the trace's own
    /// three-word phrase plus the first 7 hex characters of its id — so a
    /// console reader can name and locate the trace without cross-referencing
    /// a separate identifier line (2026-09-13 ruling, item 4) *(since 0.1.5,
    /// unreleased)*. Silent when <see cref="TraceTree.TraceId"/> is
    /// <see cref="TraceId.Empty"/> (an empty tree): nothing here is invented.
    /// </summary>
    /// <remarks>
    /// This is the trace's OWN name, unrelated to the test-suite run name a
    /// test-framework integration threads through the console footer and
    /// manifest — see <see cref="RunIdentity"/>. Neither ever reaches the
    /// structural <c>.nt</c> text.
    /// </remarks>
    private static void AppendTraceHeader(TraceTree tree, StringBuilder sb)
    {
        var traceId = tree.TraceId;
        if (traceId.IsEmpty)
        {
            return;
        }

        sb.Append("trace: ").Append(traceId.HumanName)
            .Append(" (").Append(traceId.Value, 0, 7).Append(")\n\n");
    }

    private static void RenderNodes(
        StringBuilder sb,
        IReadOnlyList<CitedNode> nodes,
        string indent)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var isLast = i == nodes.Count - 1;
            var connector = isLast ? "\u2514\u2500\u2500 " : "\u251c\u2500\u2500 ";
            var childIndent = indent
                + (isLast ? "    " : "\u2502   ");
            var (node, id) = nodes[i];

            sb.Append(indent);
            sb.Append(connector);
            AppendEntry(sb, node, id);

            AppendNarration(sb, node.Signature, childIndent);
            RenderChildren(sb, node.Children, childIndent, id);
        }
    }

    /// <summary>One call's line after its connector: signature, outcome, duration, then its span id.</summary>
    private static void AppendEntry(StringBuilder sb, TraceNode node, SpanRef id)
    {
        AppendSignature(sb, node.Signature);
        AppendOutcome(sb, node);
        AppendDuration(sb, node);
        id.AppendTo(sb.Append(' ')).AppendLine();
    }

    private static void RenderChildren(
        StringBuilder sb,
        IReadOnlyList<TraceNode> children,
        string indent,
        SpanRef? parent)
    {
        if (children.Count == 0)
        {
            return;
        }

        var ids = new SpanCursor(parent?.ToString());
        if (!ChildSegment.AnyConcurrent(children))
        {
            // No concurrent group: every sibling is one batch, and no segment needs allocating.
            RenderNodes(sb, Cite(children, ids), indent);
            return;
        }

        RenderSegments(sb, children, indent, ids);
    }

    private static List<CitedNode> Cite(IReadOnlyList<TraceNode> children, SpanCursor ids)
    {
        var cited = new List<CitedNode>(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            cited.Add(new CitedNode(children[i], ids.NextRef()));
        }

        return cited;
    }

    private static void RenderSegments(
        StringBuilder sb, IReadOnlyList<TraceNode> children, string indent, SpanCursor ids)
    {
        var batch = new List<CitedNode>();
        var segments = ChildSegment.Partition(children);
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsConcurrent)
            {
                FlushBatch(sb, batch, indent);
                RenderConcurrentSegment(
                    sb, segments[i], indent, ids);
            }
            else
            {
                batch.Add(new CitedNode(segments[i].Nodes[0], ids.NextRef()));
            }
        }

        FlushBatch(sb, batch, indent);
    }

    private static void FlushBatch(
        StringBuilder sb, List<CitedNode> batch,
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
        string indent, SpanCursor ids)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            RenderFireAndForget(sb, segment, indent, ids.Next());
        }
        else
        {
            RenderForkJoin(sb, segment, indent, ids);
        }
    }

    /// <summary>
    /// The launch takes one span id, printed at the end of its marker line;
    /// the launched work is numbered under it in capture order.
    /// </summary>
    private static void RenderFireAndForget(
        StringBuilder sb, ChildSegment segment,
        string indent, string launchId)
    {
        sb.Append(indent);
        sb.Append("\u2514\u2500\u2500 \u2933 fire-and-forget");
        AppendLauncherThread(sb, segment);
        sb.Append(' ').Append(launchId).AppendLine();

        var body = FireAndForgetSection.Body(segment.Nodes);
        if (body.Count == 0)
        {
            sb.Append(indent)
                .AppendLine("        [launched, result not captured]");
            return;
        }

        var launched = new SpanCursor(launchId);
        for (var i = 0; i < body.Count; i++)
        {
            RenderForkMember(
                sb, body[i], indent + "    ", launched.NextRef());
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
        string indent, SpanCursor ids)
    {
        sb.Append(indent);
        sb.Append("\u251c\u2500\u2500 \u2442 fork [");
        sb.Append(segment.Nodes.Count);
        sb.Append(" tasks]");
        AppendSequentialWarning(sb, segment.Nodes);
        sb.AppendLine();

        var sorted = SpanPath.InConcurrentOrder(segment.Nodes);
        for (var i = 0; i < sorted.Count; i++)
        {
            RenderForkMember(
                sb, sorted[i], indent + "\u2502   ", ids.NextRef());
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
        string indent, SpanRef id)
    {
        sb.Append(indent);
        sb.Append("\u21a6 ");
        AppendEntry(sb, node, id);
        RenderChildren(
            sb, node.Children, indent + "    ", id);
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
