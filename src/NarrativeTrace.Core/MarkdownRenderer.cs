// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Renders a finished trace as Markdown — the default, review-friendly output.
/// </summary>
/// <remarks>
/// The format to reach for when the trace will be read in a diff, a merge
/// request, or a wiki, since the nesting survives as a bullet hierarchy. For a
/// terminal use <see cref="IndentedTextRenderer"/>, for flowing narrative
/// <see cref="ProseRenderer"/>, and for a value-free, byte-stable artifact
/// <see cref="StructuralTraceRenderer"/>. Stateless and safe to call from any
/// thread; rendering the same tree twice yields identical output.
/// </remarks>
public static class MarkdownRenderer
{
    private const int DefaultSlowThresholdMs = 200;

    /// <summary>Renders a trace as a Markdown document.</summary>
    /// <param name="tree">The finished trace. An empty tree renders to an empty document, not to a placeholder.</param>
    /// <param name="options">
    /// Presentation settings, or <see langword="null"/> for the defaults —
    /// which include the frontmatter block and a 200 ms slow threshold. Note the
    /// defaults applied here match <see cref="MarkdownOptions"/>'s own, so
    /// passing <see langword="null"/> and passing <c>new MarkdownOptions()</c>
    /// are equivalent.
    /// </param>
    /// <returns>The rendered Markdown. Never <see langword="null"/>.</returns>
    /// <remarks>
    /// Renders whatever survived level filtering, so a trace captured at
    /// <see cref="TracingLevel.Summary"/> renders without parameter values —
    /// the renderer cannot restore detail that capture discarded.
    /// </remarks>
    public static string Render(
        TraceTree tree, MarkdownOptions? options = null)
    {
        var safe = Bounded(tree);
        var sb = new StringBuilder();
        if (options?.IncludeFrontmatter ?? true)
        {
            RenderFrontmatter(sb, safe, options);
        }

        RenderCallFlow(sb, safe, options);
        return sb.ToString();
    }

    /// <summary>
    /// Renders the standalone document form: YAML frontmatter, a visible
    /// document header, then the call flow.
    /// </summary>
    /// <param name="tree">The finished trace. An empty tree renders frontmatter and no header.</param>
    /// <param name="metadata">
    /// Supplies the scenario name and the outcome shown in the header. The
    /// outcome is the producer's verdict, rendered with
    /// <see cref="ScenarioResultExtensions.DisplayName"/> — it is not re-derived
    /// from the trace.
    /// </param>
    /// <param name="options">
    /// Presentation settings, or <see langword="null"/> for the defaults.
    /// <see cref="MarkdownOptions.IncludeFrontmatter"/> is ignored here: the
    /// document form always carries frontmatter, matching Java's
    /// <c>renderDocument</c>.
    /// </param>
    /// <returns>The rendered Markdown document.</returns>
    /// <remarks>
    /// This is the form <c>TraceArtifactWriter</c> writes and the counterpart of
    /// Java's <c>MarkdownRenderer.renderDocument</c>. Use <see cref="Render"/>
    /// for the body alone.
    /// </remarks>
    public static string RenderDocument(
        TraceTree tree, TraceMetadata metadata,
        MarkdownOptions? options = null)
    {
        var safe = Bounded(tree);
        var sb = new StringBuilder();
        RenderFrontmatter(
            sb, safe, WithScenario(options, metadata.Scenario));
        RenderDocumentHeader(sb, safe, metadata);
        RenderCallFlow(sb, safe, options);
        return sb.ToString();
    }

    // Every recursive walk below this point assumes an acyclic, depth-bounded
    // forest — TraceNode.Children is a type, not a guarantee of either. Both
    // public entry points bound once, here, so nothing downstream (frontmatter
    // counts, the call-flow walk, ValueReferenceIndex) has to know the input
    // could otherwise be hostile. Cheap on ordinary input: TreeWalk.Bound
    // returns the original Roots list, unchanged, once it confirms there is
    // nothing to bound.
    private static TraceTree Bounded(TraceTree tree)
    {
        return tree with { Roots = TreeWalk.Bound(tree.Roots) };
    }

    private static MarkdownOptions WithScenario(
        MarkdownOptions? options, string scenario)
    {
        return (options ?? new MarkdownOptions())
            with
        { ScenarioName = scenario };
    }

    private static void RenderCallFlow(
        StringBuilder sb, TraceTree tree, MarkdownOptions? options)
    {
        RenderNodes(
            sb, tree.Roots, 0,
            options?.SlowThresholdMs ?? DefaultSlowThresholdMs,
            ValueReferenceIndex.Build(tree));
    }

    // Java renderDocumentHeader: a visible header between the YAML frontmatter
    // and the call-flow bullets.
    private static void RenderDocumentHeader(
        StringBuilder sb, TraceTree tree, TraceMetadata metadata)
    {
        if (tree.Roots.Count == 0)
        {
            return;
        }

        var sig = tree.Roots[0].Signature;
        sb.Append("## Trace: ").Append(sig.ClassName)
            .Append('.').AppendLine(sig.MethodName);
        sb.AppendLine();
        AppendHeaderSummary(sb, tree, metadata);
        sb.AppendLine();
        sb.AppendLine("### Call Flow");
        sb.AppendLine();
    }

    private static void AppendHeaderSummary(
        StringBuilder sb, TraceTree tree, TraceMetadata metadata)
    {
        var ms = tree.Roots[0].DurationTicks
            / TimeSpan.TicksPerMillisecond;
        sb.Append("**Scenario:** ").AppendLine(metadata.Scenario);
        sb.Append("**Duration:** ")
            .Append(ms.ToString(CultureInfo.InvariantCulture))
            .Append("ms | **Result:** ")
            .AppendLine(metadata.Result.DisplayName());
    }

    private static void RenderFrontmatter(
        StringBuilder sb, TraceTree tree,
        MarkdownOptions? options)
    {
        sb.AppendLine("---");
        sb.AppendLine("type: trace");
        AppendScenario(sb, options);
        AppendEntryPoint(sb, tree);
        AppendTraceIdentity(sb, tree);
        AppendCounts(sb, tree);
        AppendDuration(sb, tree);
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendScenario(
        StringBuilder sb, MarkdownOptions? options)
    {
        if (options?.ScenarioName is not null)
        {
            sb.Append("scenario: ");
            sb.AppendLine(YamlEscape.Scalar(options.ScenarioName));
        }
    }

    // A class or method name reaching here from reflection or a
    // dynamically-typed caller could otherwise inject sibling YAML keys —
    // AppendScenario already escapes its value the same way, and this was
    // the one frontmatter field that did not.
    private static void AppendEntryPoint(
        StringBuilder sb, TraceTree tree)
    {
        if (tree.Roots.Count > 0)
        {
            var sig = tree.Roots[0].Signature;
            sb.Append("entry_point: ");
            sb.AppendLine(YamlEscape.Scalar(
                $"{sig.ClassName}.{sig.MethodName}"));
        }
    }

    private static void AppendTraceIdentity(
        StringBuilder sb, TraceTree tree)
    {
        if (FindRootSpanContext(tree) is { } sc)
        {
            sb.Append("trace_id: ");
            sb.AppendLine(sc.TraceId.Value);
            sb.Append("trace_name: ");
            sb.AppendLine(sc.TraceId.HumanName);
        }
    }

    private static void AppendCounts(StringBuilder sb, TraceTree tree)
    {
        sb.Append("method_count: ");
        sb.AppendLine(CountMethods(tree.Roots)
            .ToString(CultureInfo.InvariantCulture));
        sb.Append("error_count: ");
        sb.AppendLine(CountErrors(tree.Roots)
            .ToString(CultureInfo.InvariantCulture));
        sb.Append("result: ");
        sb.AppendLine(
            TraceNode.HasAnyError(tree.Roots) ? "error" : "success");
    }

    private static void AppendDuration(StringBuilder sb, TraceTree tree)
    {
        if (tree.Roots.Count > 0)
        {
            var ms = tree.Roots[0].DurationTicks
                / TimeSpan.TicksPerMillisecond;
            sb.Append("duration_ms: ");
            sb.AppendLine(ms.ToString(CultureInfo.InvariantCulture));
        }
    }

    private static SpanContext? FindRootSpanContext(TraceTree tree)
    {
        for (var i = 0; i < tree.Roots.Count; i++)
        {
            if (tree.Roots[i].SpanContext is { } sc)
            {
                return sc;
            }
        }

        return null;
    }

    private static int CountErrors(IReadOnlyList<TraceNode> nodes)
    {
        var count = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Outcome is Threw)
            {
                count++;
            }

            count += CountErrors(nodes[i].Children);
        }

        return count;
    }

    private static int CountMethods(
        IReadOnlyList<TraceNode> nodes)
    {
        var count = nodes.Count;
        for (var i = 0; i < nodes.Count; i++)
        {
            count += CountMethods(nodes[i].Children);
        }

        return count;
    }

    private static void RenderNodes(
        StringBuilder sb,
        IReadOnlyList<TraceNode> nodes,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            RenderNode(sb, nodes[i], depth, slowThresholdMs, refs);
        }
    }

    private static void RenderNode(
        StringBuilder sb, TraceNode node,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        var indent = new string(' ', depth * 2);
        AppendMethodBullet(sb, node, indent, refs);
        if (node.Children.Count == 0)
        {
            AppendOutcome(sb, node, refs);
            CloseEntryLine(sb, node, indent, slowThresholdMs);
            return;
        }

        // A normal return reads inline on the entry line; only exceptional and
        // in-flight outcomes close after the children, chronologically where
        // they happened.
        if (node.Outcome is Returned)
        {
            AppendOutcome(sb, node, refs);
        }

        CloseEntryLine(sb, node, indent, slowThresholdMs);
        RenderChildren(
            sb, node.Children, depth + 1, slowThresholdMs, refs);
        AppendClosingOutcome(sb, node, indent);
    }

    /// <summary>
    /// Ends the entry line with its duration and follows it with the
    /// node's narration, which every entry line carries — leaf or parent.
    /// </summary>
    private static void CloseEntryLine(
        StringBuilder sb, TraceNode node,
        string indent, int slowThresholdMs)
    {
        AppendNodeDuration(sb, node, slowThresholdMs);
        sb.AppendLine();
        RenderNarration(sb, node.Signature, indent);
    }

    private static void AppendMethodBullet(
        StringBuilder sb, TraceNode node, string indent,
        ValueReferenceIndex refs)
    {
        sb.Append(indent);
        sb.Append("- **");
        AppendSignatureName(sb, node.Signature);
        sb.Append("**");
        AppendParameters(sb, node.Signature.Parameters, refs);
    }

    // The class/method name pair every heading (the top-level bullet and a
    // fork member's bullet) shows, escaped the same way exception type names
    // and parameter names in this file already are — one seam so the two
    // headings cannot drift on how much they escape.
    private static void AppendSignatureName(StringBuilder sb, MethodSignature sig)
    {
        sb.Append(MarkdownEscape.Text(sig.ClassName));
        sb.Append('.');
        sb.Append(MarkdownEscape.Text(sig.MethodName));
    }

    private static void AppendClosingOutcome(
        StringBuilder sb, TraceNode node, string indent)
    {
        switch (node.Outcome)
        {
            case Threw { Error: { } ex }:
                sb.Append(indent).Append("  - ❌ ");
                sb.Append(MarkdownEscape.Text(ex.GetType().Name));
                sb.Append(": ");
                sb.Append(MarkdownEscape.Text(ExceptionMessage.Text(ex)));
                AppendErrorContext(sb, node.Signature);
                sb.AppendLine();
                break;
            case Incomplete:
                sb.Append(indent).AppendLine("  - ⏳ in-flight");
                break;
        }
    }

    private static void RenderChildren(
        StringBuilder sb,
        IReadOnlyList<TraceNode> children,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        var segments = ChildSegment.Partition(children);
        for (var i = 0; i < segments.Count; i++)
        {
            if (segments[i].IsConcurrent)
            {
                RenderConcurrentSegment(
                    sb, segments[i], depth,
                    slowThresholdMs, refs);
            }
            else
            {
                RenderNodes(
                    sb, segments[i].Nodes, depth,
                    slowThresholdMs, refs);
            }
        }
    }

    private static void RenderConcurrentSegment(
        StringBuilder sb, ChildSegment segment,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            RenderFireAndForget(
                sb, segment, depth, slowThresholdMs, refs);
        }
        else
        {
            RenderForkJoin(
                sb, segment, depth, slowThresholdMs, refs);
        }
    }

    private static void RenderForkJoin(
        StringBuilder sb, ChildSegment segment,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        var indent = new string(' ', depth * 2);
        AppendForkMarker(sb, indent, segment);
        RenderForkMembers(
            sb, segment.Nodes, depth, slowThresholdMs, refs);
        AppendJoinMarker(sb, indent, segment);
    }

    private static void RenderFireAndForget(
        StringBuilder sb, ChildSegment segment,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        var indent = new string(' ', depth * 2);
        sb.Append(indent).Append("- \u2933 fire-and-forget");
        AppendLauncherThread(sb, segment);
        sb.AppendLine();

        var body = FireAndForgetSection.Body(segment.Nodes);
        if (body.Count == 0)
        {
            sb.Append(indent)
                .AppendLine("  [launched, result not captured]");
        }
        else
        {
            RenderForkMembers(sb, body, depth, slowThresholdMs, refs);
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

    private static void AppendForkMarker(
        StringBuilder sb, string indent,
        ChildSegment segment)
    {
        sb.Append(indent);
        sb.Append("- \u2442 fork [");
        sb.Append(segment.Nodes.Count);
        sb.Append(" tasks]");
        AppendSequentialWarning(sb, segment.Nodes);
        sb.AppendLine();
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
            sb.Append(" — could save ");
            sb.Append(savings);
            sb.Append("ms with Task.WhenAll");
        }
    }

    private static void RenderForkMembers(
        StringBuilder sb, IReadOnlyList<TraceNode> nodes,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        var sorted = SortByClassName(nodes);
        for (var i = 0; i < sorted.Count; i++)
        {
            RenderForkMember(
                sb, sorted[i], depth,
                slowThresholdMs, refs);
        }
    }

    private static void RenderForkMember(
        StringBuilder sb, TraceNode node,
        int depth, int slowThresholdMs,
        ValueReferenceIndex refs)
    {
        var indent = new string(' ', (depth + 1) * 2);
        sb.Append(indent);
        sb.Append("\u21a6 **");
        AppendSignatureName(sb, node.Signature);
        sb.Append("**");
        AppendParameters(sb, node.Signature.Parameters, refs);
        AppendOutcome(sb, node, refs);
        AppendThreadInfo(sb, node);
        sb.AppendLine();
        RenderChildren(
            sb, node.Children, depth + 2,
            slowThresholdMs, refs);
    }

    private static void AppendThreadInfo(
        StringBuilder sb, TraceNode node)
    {
        if (node.Concurrency is null)
        {
            return;
        }

        sb.Append(" [thread: ");
        sb.Append(node.Concurrency.ThreadId);
        sb.Append(']');
    }

    private static void AppendJoinMarker(
        StringBuilder sb, string indent,
        ChildSegment segment)
    {
        sb.Append(indent);
        sb.Append("- \u2443 join");
        var wallMs = ComputeWallTimeMs(segment.Nodes);
        if (wallMs > 0)
        {
            sb.Append(" \u2014 ");
            sb.Append(wallMs);
            sb.Append("ms");
        }

        AppendWaitAnalysis(sb, segment.Nodes);
        sb.AppendLine();
    }

    private static void AppendWaitAnalysis(
        StringBuilder sb, IReadOnlyList<TraceNode> nodes)
    {
        if (nodes.Count < 2)
        {
            return;
        }

        var slowest = nodes[0];
        var fastest = nodes[0];
        for (var i = 1; i < nodes.Count; i++)
        {
            if (nodes[i].DurationTicks > slowest.DurationTicks)
            {
                slowest = nodes[i];
            }

            if (nodes[i].DurationTicks < fastest.DurationTicks)
            {
                fastest = nodes[i];
            }
        }

        AppendWaitDetail(sb, slowest, fastest);
    }

    private static void AppendWaitDetail(
        StringBuilder sb, TraceNode slowest, TraceNode fastest)
    {
        var waitMs =
            (slowest.DurationTicks - fastest.DurationTicks)
            / TimeSpan.TicksPerMillisecond;
        if (waitMs <= 0)
        {
            return;
        }

        sb.Append(" (waited ");
        sb.Append(waitMs);
        sb.Append("ms for ");
        sb.Append(MarkdownEscape.Text(slowest.Signature.ClassName));
        sb.Append(" after ");
        sb.Append(MarkdownEscape.Text(fastest.Signature.ClassName));
        sb.Append(')');
    }

    private static long ComputeWallTimeMs(
        IReadOnlyList<TraceNode> nodes)
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

        return max;
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

    // Mirrors Java renderDuration: every timed node shows "\u2014 Nms"; the slow
    // marker is appended only when millis strictly exceeds the threshold.
    private static void AppendNodeDuration(
        StringBuilder sb, TraceNode node,
        int slowThresholdMs)
    {
        if (node.DurationTicks <= 0)
        {
            return;
        }

        var ms = node.DurationTicks
            / TimeSpan.TicksPerMillisecond;
        sb.Append(" \u2014 ");
        sb.Append(ms);
        sb.Append("ms");
        if (ms > slowThresholdMs)
        {
            sb.Append(" \u26a0\ufe0f slow");
        }
    }

    private static void AppendParameters(
        StringBuilder sb,
        IReadOnlyList<ParameterCapture> parameters,
        ValueReferenceIndex refs)
    {
        sb.Append('(');
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(MarkdownEscape.Text(parameters[i].Name));
            sb.Append(": ");
            sb.Append(MarkdownEscape.Code(
                DisplayOf(parameters[i], refs)));
        }

        sb.Append(')');
    }

    /// <summary>
    /// A redacted capture never enters the reference machinery: its marker is
    /// not a value, and referencing it would make the marker look like a
    /// deduplicated payload.
    /// </summary>
    private static string DisplayOf(
        ParameterCapture parameter, ValueReferenceIndex refs)
    {
        return parameter.Redacted
            ? parameter.DisplayValue()
            : refs.Display(parameter.RenderedValue)
                ?? parameter.DisplayValue();
    }

    private static void AppendOutcome(
        StringBuilder sb, TraceNode node,
        ValueReferenceIndex refs)
    {
        switch (node.Outcome)
        {
            case Returned { RenderedValue: { } value }:
                sb.Append(" \u2192 ");
                sb.Append(MarkdownEscape.Code(refs.Display(value)!));
                break;
            case Threw { Error: { } ex }:
                sb.Append(" \u274c ");
                sb.Append(MarkdownEscape.Text(ex.GetType().Name));
                sb.Append(": ");
                sb.Append(MarkdownEscape.Text(ExceptionMessage.Text(ex)));
                AppendErrorContext(sb, node.Signature);
                break;
            case Incomplete:
                sb.Append(" \u23f3 incomplete");
                break;
        }
    }

    private static void AppendErrorContext(
        StringBuilder sb, MethodSignature sig)
    {
        if (sig.ErrorContext is { } context)
        {
            sb.Append(" — ");
            sb.Append(MarkdownEscape.Text(context));
        }
    }

    private static void RenderNarration(
        StringBuilder sb,
        MethodSignature sig, string indent)
    {
        if (sig.Narration is null)
        {
            return;
        }

        sb.Append(indent);
        sb.Append("  *");
        sb.Append(MarkdownEscape.Text(sig.Narration));
        sb.AppendLine("*");
    }
}
