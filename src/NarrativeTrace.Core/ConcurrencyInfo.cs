// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>Which concurrency shape produced a group of sibling spans.</summary>
/// <remarks>
/// Determines how a group renders and how its timing reads: a
/// <see cref="ForkJoin"/> group is bounded by the join, so its branches are all
/// accounted for, while a <see cref="FireAndForget"/> group may still be running
/// when the trace is captured.
/// </remarks>
public enum ConcurrencyKind
{
    /// <summary>Branches spawned together and awaited — the parent blocks until all complete.</summary>
    ForkJoin,

    /// <summary>Work started without being awaited; branches may outlive the span that spawned them.</summary>
    FireAndForget,

    /// <summary>
    /// Work adopted across a snapshot boundary: concurrent, but launched
    /// directly rather than through a fork or fire-and-forget helper.
    /// </summary>
    /// <remarks>
    /// Tags the <b>first</b> span a flow opens under an activated snapshot and
    /// nothing deeper — everything below it is ordinary sequential work on that
    /// flow. The group is keyed by the launching span, so every async child of
    /// one call renders as one group. Without the tag a structural artifact
    /// would pin the scheduler's dispatch order and no concurrent scenario
    /// could hold a stable baseline.
    /// </remarks>
    Async,
}

/// <summary>
/// Marks a span as part of a concurrent group and records the thread it
/// actually ran on.
/// </summary>
/// <remarks>
/// Present on a <see cref="TraceNode"/> only for spans captured inside a fork;
/// ordinary sequential calls carry <see langword="null"/>. Siblings sharing a
/// <see cref="GroupId"/> form one group, which is what
/// <see cref="ChildSegment.Partition"/> keys on to rebuild the groupings from a
/// flat child list.
/// </remarks>
/// <param name="GroupId">
/// Identifies the group. Shared by every branch of one fork and unique per fork
/// within a trace; siblings that differ here belong to different groups even if
/// they ran at the same time.
/// </param>
/// <param name="TaskLabel">A human-readable name for this branch, used as its heading in the rendered trace.</param>
/// <param name="ThreadId">
/// The managed thread id the span ran on. A diagnostic breadcrumb only — ids are
/// reused after a thread dies, so it is not a stable identifier.
/// </param>
/// <param name="ThreadName">The thread's name, or <see langword="null"/> for the unnamed pool threads that are the common case.</param>
/// <param name="IsThreadPoolThread">Whether the branch ran on a pool thread rather than a dedicated one.</param>
/// <param name="Kind">
/// How the group ran: awaited (fork-join), left running (fire-and-forget), or
/// adopted from a propagated snapshot (<see cref="ConcurrencyKind.Async"/>).
/// </param>
public sealed record ConcurrencyInfo(
    string GroupId,
    string TaskLabel,
    int ThreadId,
    string? ThreadName,
    bool IsThreadPoolThread,
    ConcurrencyKind Kind);
