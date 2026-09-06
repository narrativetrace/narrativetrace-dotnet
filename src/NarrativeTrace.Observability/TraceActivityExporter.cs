// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Globalization;
using NarrativeTrace.Core;

namespace NarrativeTrace.Observability;

/// <summary>
/// Replays a finished trace as <see cref="System.Diagnostics.Activity"/> spans,
/// bridging it into OpenTelemetry and any other <c>ActivitySource</c> listener.
/// </summary>
/// <remarks>
/// <para>
/// Emits under the activity source name <c>"NarrativeTrace"</c> — a listener
/// must subscribe to that name or nothing is exported. With no listener
/// attached, <see cref="System.Diagnostics.ActivitySource.StartActivity(string, ActivityKind)"/>
/// returns <see langword="null"/> and the whole export becomes a cheap
/// tree walk rather than an error.
/// </para>
/// <para>
/// A replay, not live instrumentation: spans are created after the fact from
/// captured data, so they carry the trace's own hierarchy and are not children
/// of whatever <see cref="System.Diagnostics.Activity.Current"/> happens to be.
/// </para>
/// </remarks>
public static class TraceActivityExporter
{
    private static readonly ActivitySource Source =
        new("NarrativeTrace");

    /// <summary>Replays a trace as activity spans.</summary>
    /// <param name="tree">The finished trace to export. An empty tree emits nothing.</param>
    /// <remarks>
    /// Exports captured values, exception details and timings, so the emitted
    /// spans carry the same sensitivity as the traced data — mind where the
    /// listener ships them. Nesting is preserved even across nodes that produced
    /// no activity, so a sampled-out parent does not reparent its children.
    /// </remarks>
    public static void Export(TraceTree tree)
    {
        // TraceNode.Children is a type, not a guarantee of acyclicity -
        // bound once, here, so ExportNodes' recursion below can never
        // overflow the stack or loop forever on a hand-built or replayed
        // cycle. Cheap on ordinary input: TreeWalk.Bound returns Roots
        // unchanged once it confirms there is nothing to bound.
        ExportNodes(TreeWalk.Bound(tree.Roots), isRoot: true);
    }

    private static void ExportNodes(
        IReadOnlyList<TraceNode> nodes, bool isRoot)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            ExportNode(nodes[i], isRoot);
        }
    }

    private static void ExportNode(TraceNode node, bool isRoot)
    {
        var sig = node.Signature;
        var name =
            $"{sig.ClassName}.{sig.MethodName}";

        using var activity = Source.StartActivity(
            name, ActivityKind.Internal);

        if (activity is null)
        {
            ExportNodes(node.Children, isRoot: false);
            return;
        }

        AnchorTiming(activity, node);
        SetTags(activity, node, isRoot);
        EmitChildEvents(activity, node.Children);
        ExportNodes(node.Children, isRoot: false);
    }

    private static void EmitChildEvents(
        Activity parent, IReadOnlyList<TraceNode> children)
    {
        for (var i = 0; i < children.Count; i++)
        {
            SpanContextAttributeMapper.EmitChildEvent(parent, children[i]);
        }
    }

    // Anchors the span to the captured node window so its Duration reflects the
    // measured call time rather than export wall-clock (Java sets explicit
    // start/end timestamps from node nanos).
    private static void AnchorTiming(Activity activity, TraceNode node)
    {
        var start = MonotonicClock.ToWallClockFromTimeSpanTicks(
            node.StartTimestamp);
        activity.SetStartTime(start.UtcDateTime);
        activity.SetEndTime(
            start.UtcDateTime + TimeSpan.FromTicks(node.DurationTicks));
    }

    private static void SetTags(
        Activity activity, TraceNode node, bool isRoot)
    {
        SpanContextAttributeMapper.WriteSignature(activity, node.Signature);
        activity.SetTag(
            "narrative.duration_ms",
            node.DurationTicks / (double)TimeSpan.TicksPerMillisecond);
        SpanContextAttributeMapper.WriteOutcome(activity, node.Outcome);
        SetConcurrencyTags(activity, node.Concurrency);
        SpanContextAttributeMapper.WriteIdentity(
            activity, node.SpanContext, isRoot);
    }

    // Java's setConcurrencyAttributes emits groupId/kind/threadId/threadName/virtual.
    // .NET pins groupId/kind/taskLabel/threadId/threadName: threadName is restored for
    // parity, taskLabel is the documented AsyncLocal-model substitute for the JVM's
    // per-thread identity, and `virtual` is intentionally dropped (no OS-thread/virtual
    // distinction under the AsyncLocal concurrency model).
    private static void SetConcurrencyTags(
        Activity activity,
        ConcurrencyInfo? concurrency)
    {
        if (concurrency is null)
        {
            return;
        }

        activity.SetTag(
            "narrative.concurrency.groupId",
            concurrency.GroupId);
        activity.SetTag(
            "narrative.concurrency.kind",
            concurrency.Kind.ToString());
        activity.SetTag(
            "narrative.concurrency.taskLabel",
            concurrency.TaskLabel);
        activity.SetTag(
            "narrative.concurrency.threadId",
            concurrency.ThreadId.ToString(
                CultureInfo.InvariantCulture));
        activity.SetTag(
            "narrative.concurrency.threadName",
            concurrency.ThreadName);
    }
}
