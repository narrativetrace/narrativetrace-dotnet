// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Base for everything a context records: the append-only event stream that
/// <see cref="TraceTreeBuilder"/> later folds into a
/// <see cref="TraceTree"/>.
/// </summary>
/// <remarks>
/// Capture writes a flat, ordered event log rather than building a tree
/// directly, which is what lets spans close out of order and lets a streaming
/// consumer observe a trace before it finishes. The tree is a derived view,
/// reconstructed from span identity and parent linkage — so events are
/// self-describing and carry no positional dependency on each other.
/// Match on the concrete subtypes; the set is closed.
/// </remarks>
/// <param name="TimestampTicks">
/// When the event occurred, as a raw
/// <see cref="System.Diagnostics.Stopwatch.GetTimestamp"/> reading — in
/// <b>Stopwatch</b> units, unlike the <see cref="TimeSpan"/> ticks that
/// <see cref="TraceNode"/> exposes, because normalization happens once at tree
/// build time. Convert with <see cref="StopwatchTicks"/> before comparing
/// against anything time-like.
/// </param>
public abstract record TraceEvent(long TimestampTicks);

/// <summary>
/// A method-entry event. Carries the self-describing <see cref="SpanContext"/>
/// (span identity + parent linkage + trace correlation) instead of local
/// integer handles.
/// </summary>
/// <param name="SpanContext">Span identity, parent linkage and trace correlation for this call.</param>
/// <param name="TimestampTicks">When the call was entered, in raw Stopwatch units.</param>
/// <param name="Signature">The method entered, with its pre-rendered parameters.</param>
/// <param name="Concurrency">
/// Set only when the span opens a concurrent group at capture time — today, the
/// first span a flow opens under an activated snapshot
/// (<see cref="ConcurrencyKind.Async"/>). <see langword="null"/> for ordinary
/// sequential calls, and for fork/fire-and-forget work, which is tagged when
/// its subtree is grafted rather than when it is entered.
/// </param>
public sealed record EnterEvent(
    SpanContext SpanContext,
    long TimestampTicks,
    MethodSignature Signature,
    ConcurrencyInfo? Concurrency = null) : TraceEvent(TimestampTicks);

/// <summary>
/// A method-exit event, matched to its <see cref="EnterEvent"/> by
/// <see cref="SpanContext"/> span id. <see cref="ErrorContext"/> carries an
/// error narrative resolved at exception time against the thrown type
/// (e.g. from <c>[OnError]</c>); when present it supersedes any enter-time
/// error context on the node's signature.
/// </summary>
public sealed record ExitEvent(
    SpanContext SpanContext,
    long TimestampTicks,
    TraceOutcome Outcome,
    string? ErrorContext = null) : TraceEvent(TimestampTicks);

/// <summary>
/// Merges an externally-built subtree under a parent span. A null
/// <see cref="ParentSpanId"/> grafts at the root.
/// </summary>
public sealed record GraftEvent(
    SpanId? ParentSpanId,
    long TimestampTicks,
    TraceNode Node) : TraceEvent(TimestampTicks);

/// <summary>
/// Signals that a fork-join group was created. Carries the group id so
/// downstream stream consumers can correlate later merge/child events.
/// </summary>
public sealed record ForkCreatedEvent(
    string GroupId,
    long TimestampTicks) : TraceEvent(TimestampTicks);

/// <summary>
/// Signals that a fork-join group's forked tasks were merged back under the
/// parent span. <see cref="MemberCount"/> is the number of merged members and
/// <see cref="WallTimeTicks"/> the longest member's wall time.
/// </summary>
public sealed record MergeEvent(
    string GroupId,
    int MemberCount,
    long WallTimeTicks,
    long TimestampTicks) : TraceEvent(TimestampTicks);

/// <summary>
/// Signals that background fire-and-forget work was launched for a group.
/// </summary>
public sealed record FireAndForgetEvent(
    string GroupId,
    long TimestampTicks) : TraceEvent(TimestampTicks);
