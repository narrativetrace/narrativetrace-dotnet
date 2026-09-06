// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Core;

namespace NarrativeTrace.Observability;

/// <summary>
/// Live event-stream bridge from NarrativeTrace events to OpenTelemetry
/// <see cref="Activity"/> spans, creating spans as methods execute rather than
/// after the whole trace tree has been built. Complements the batch
/// <see cref="TraceActivityExporter"/>.
/// </summary>
/// <remarks>
/// Maintains a <see cref="PerishableMap{TKey,TValue}"/> of active spans keyed by
/// NarrativeTrace span id: enter starts a span, exit ends it. Orphaned spans
/// (enter without a matching exit) are evicted by the map's TTL and capacity
/// limits and ended with error status. Parent-child links are reconstructed
/// from explicit span-context parent ids, so interleaved enter/exit pairs from
/// concurrent traces are handled correctly.
/// <para>Register with a pipeline via
/// <c>consumer.Subscribe(listener.OnEvent)</c>.</para>
/// </remarks>
public sealed class OtelTraceEventListener
{
    private const int DefaultMaxActiveSpans = 1024;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    private readonly ActivitySource _source;
    private readonly PerishableMap<SpanId, SpanFrame> _activeSpans;

    /// <summary>Pairs an OTel activity with the signature captured at enter time.</summary>
    private readonly record struct SpanFrame(Activity Activity, MethodSignature Signature);

    /// <summary>Creates a listener with default capacity (1024) and TTL (1 hour).</summary>
    public OtelTraceEventListener(ActivitySource source)
        : this(source, DefaultMaxActiveSpans, DefaultTtl)
    {
    }

    /// <summary>
    /// Creates a listener with configurable capacity and TTL for orphan eviction.
    /// </summary>
    /// <param name="source">the activity source spans are created on.</param>
    /// <param name="maxActiveSpans">
    /// maximum active (unfinished) spans before the oldest is evicted.
    /// </param>
    /// <param name="ttl">maximum age of an active span before it is evicted as orphaned.</param>
    public OtelTraceEventListener(
        ActivitySource source, int maxActiveSpans, TimeSpan ttl)
    {
        _source = source;
        _activeSpans = new PerishableMap<SpanId, SpanFrame>(
            maxActiveSpans, ttl, EndAsOrphaned);
    }

    /// <summary>Handles one trace event, creating or ending a span.</summary>
    /// <remarks>
    /// Span structure is reconstructed from explicit span-context parent ids, not
    /// the drain thread's ambient <see cref="Activity.Current"/>. The ambient value
    /// is snapshotted and restored around each event so that spans left active
    /// between events never leak as the parent of an unrelated later span.
    /// </remarks>
    public void OnEvent(TraceEvent traceEvent)
    {
        var previousAmbient = Activity.Current;
        try
        {
            Dispatch(traceEvent);
        }
        finally
        {
            Activity.Current = previousAmbient;
        }
    }

    private void Dispatch(TraceEvent traceEvent)
    {
        switch (traceEvent)
        {
            case EnterEvent enter:
                HandleEnter(enter);
                break;
            case ExitEvent exit:
                HandleExit(exit);
                break;
        }
    }

    private void HandleEnter(EnterEvent enter)
    {
        var sig = enter.Signature;
        var sc = enter.SpanContext;
        var name = $"{sig.ClassName}.{sig.MethodName}";
        var activity = _source.StartActivity(
            name, ActivityKind.Internal, ResolveParentContext(sc),
            startTime: MonotonicClock.ToWallClock(enter.TimestampTicks));
        if (activity is null)
        {
            return;
        }

        SpanContextAttributeMapper.WriteSignature(activity, sig);
        SpanContextAttributeMapper.WriteIdentity(
            activity, sc, isRoot: sc.ParentSpanId is null);
        _activeSpans.Put(sc.SpanId, new SpanFrame(activity, sig));
    }

    private ActivityContext ResolveParentContext(SpanContext sc)
    {
        if (sc.ParentSpanId is { } parentId
            && _activeSpans.Get(parentId).Activity is { } parent)
        {
            return parent.Context;
        }

        return default;
    }

    private void HandleExit(ExitEvent exit)
    {
        var frame = _activeSpans.Remove(exit.SpanContext.SpanId);
        if (frame.Activity is null)
        {
            HandleExitWithoutEnter(exit);
            return;
        }

        SpanContextAttributeMapper.WriteOutcome(frame.Activity, exit.Outcome);
        frame.Activity.SetEndTime(
            MonotonicClock.ToWallClock(exit.TimestampTicks).UtcDateTime);
        frame.Activity.Stop();
        EmitEventOnParent(exit, frame.Signature);
    }

    // Mirrors Java emitEventOnParent: records the child completion as a timestamped
    // event on the still-active parent span, when the parent has not already exited.
    private void EmitEventOnParent(ExitEvent exit, MethodSignature signature)
    {
        if (exit.SpanContext.ParentSpanId is not { } parentId
            || _activeSpans.Get(parentId).Activity is not { } parent)
        {
            return;
        }

        SpanContextAttributeMapper.EmitCompletionEvent(
            parent, signature, exit.Outcome,
            MonotonicClock.ToWallClock(exit.TimestampTicks));
    }

    private void HandleExitWithoutEnter(ExitEvent exit)
    {
        var sc = exit.SpanContext;
        var startTime = MonotonicClock.ToWallClock(exit.TimestampTicks);
        var activity = _source.StartActivity(
            sc.SpanId.Value, ActivityKind.Internal,
            ResolveParentContext(sc), startTime: startTime);
        if (activity is null)
        {
            return;
        }

        SpanContextAttributeMapper.WriteIdentity(activity, sc, isRoot: true);
        SpanContextAttributeMapper.WriteOutcome(activity, exit.Outcome);
        activity.SetStatus(ActivityStatusCode.Error, "orphaned — enter event lost");
        activity.SetEndTime(startTime.UtcDateTime);
        activity.Stop();
    }

    private static void EndAsOrphaned(SpanFrame frame)
    {
        frame.Activity.SetStatus(
            ActivityStatusCode.Error, "orphaned — exit event lost");
        frame.Activity.Stop();
    }
}
