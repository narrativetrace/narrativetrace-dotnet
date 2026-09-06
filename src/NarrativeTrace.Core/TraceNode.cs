// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// One call in a finished trace, with its children — the unit renderers and
/// exporters walk.
/// </summary>
/// <remarks>
/// Immutable and already filtered to the <see cref="TracingLevel"/> that was in
/// effect when <see cref="INarrativeContext.CaptureTrace"/> ran, so a node tree
/// is safe to hand across threads and to hold indefinitely. Built by
/// <see cref="TraceTreeBuilder"/> from the event stream rather than constructed
/// directly by instrumentation.
/// </remarks>
/// <param name="Signature">Who was called, with what, and how it should read.</param>
/// <param name="Outcome">How the call ended.</param>
/// <param name="Children">
/// Nested calls: spans parented directly to this one in the order they were
/// entered, followed by any subtrees grafted in from a concurrent branch. Empty
/// for a leaf; never <see langword="null"/>. Because grafts are appended rather
/// than interleaved, this list is not globally sorted by start time — use
/// <see cref="ChildSegment.Partition"/> to recover the concurrent groupings, and
/// <see cref="StartTimestamp"/> if you need true chronological order.
/// </param>
/// <param name="DurationTicks">
/// Elapsed time in <see cref="TimeSpan"/> ticks (100 ns units), so
/// <c>TimeSpan.FromTicks</c> is correct and dividing by
/// <see cref="TimeSpan.TicksPerMillisecond"/> gives milliseconds. The underlying
/// clock is <see cref="System.Diagnostics.Stopwatch"/>, but
/// <see cref="StopwatchTicks"/> normalizes the platform-dependent Stopwatch
/// scale away when the tree is built — do <b>not</b> divide by
/// <see cref="System.Diagnostics.Stopwatch.Frequency"/> again. Zero for a span
/// that never completed.
/// </param>
/// <param name="StartTimestamp">
/// Entry time, also normalized to <see cref="TimeSpan"/> ticks. Derived from the
/// monotonic <see cref="System.Diagnostics.Stopwatch"/> clock, so it is
/// meaningful only for ordering and for differences within a single process —
/// it is not wall-clock time and does not compare across machines or restarts.
/// </param>
/// <param name="Concurrency">
/// Set when this node ran as part of a fork-join or fire-and-forget group;
/// <see langword="null"/> for ordinary sequential calls.
/// </param>
/// <param name="SpanContext">
/// Request-, user- and service-tier metadata stamped onto the span, or
/// <see langword="null"/> when none was set.
/// </param>
public sealed record TraceNode(
    MethodSignature Signature,
    TraceOutcome Outcome,
    IReadOnlyList<TraceNode> Children,
    long DurationTicks,
    long StartTimestamp = 0,
    ConcurrencyInfo? Concurrency = null,
    SpanContext? SpanContext = null)
{
    /// <summary>
    /// Whether any node in the given forest threw, at any depth.
    /// </summary>
    /// <param name="nodes">The roots to search. An empty list yields <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if at least one node's outcome is <see cref="Threw"/>.</returns>
    /// <remarks>
    /// A full traversal of the whole forest, bounded and cycle-safe
    /// (<see cref="TreeWalk"/>) rather than plain recursion — <see cref="Children"/>
    /// is a type, not a guarantee of acyclicity — so cache the result rather
    /// than calling it per render pass. Note it takes the <b>children</b> of a
    /// node, not the node: to test a single node including itself, check its
    /// own <see cref="Outcome"/> as well.
    /// </remarks>
    public static bool HasAnyError(IReadOnlyList<TraceNode> nodes)
    {
        return TreeWalk.Any(nodes, n => n.Outcome is Threw);
    }
}
