// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Text;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Maps <see cref="TraceEvent"/> instances to the canonical flat
/// <see cref="CanonicalEntry"/> schema. Single source of truth for the
/// event-to-schema mapping: each supported event type produces one entry
/// with the appropriate <c>nt.eventType</c>, level, and outcome fields.
/// </summary>
/// <remarks>
/// Covers method <see cref="EnterEvent"/> / <see cref="ExitEvent"/> pairs and
/// the three concurrency signals — <see cref="ForkCreatedEvent"/>,
/// <see cref="MergeEvent"/> and <see cref="FireAndForgetEvent"/> — which map
/// to the <c>fork</c>, <c>join</c> and <c>async_dispatch</c> entry types every
/// NarrativeTrace port emits. Concurrency is <em>also</em> carried as node
/// metadata (<see cref="ConcurrencyInfo"/>) for tree rendering; the entries
/// here are the flat event-stream view of the same thing. The wall-clock
/// formatter is injectable for deterministic testing.
/// </remarks>
public sealed class CanonicalEntryMapper
{
    /// <summary>
    /// Service name stamped on entries when the host pins none.
    /// </summary>
    /// <remarks>
    /// <c>service</c> is required by <c>entry.schema.json</c>, so it must never be absent
    /// or blank. This is OpenTelemetry's convention for "nobody said": <c>unknown_service:</c>
    /// plus the runtime name. Cross-port contract (owner decision, 2026-08-28): every
    /// NarrativeTrace port emits <c>unknown_service:&lt;runtime&gt;</c> with its own fixed
    /// suffix — <c>:java</c>, <c>:node</c>, <c>:python</c>, <c>:dotnet</c>, <c>:swift</c>.
    /// The suffix is a literal, not a lookup of the running executable, so the value stays
    /// deterministic across restarts, replicas and deployments; conformance fixtures
    /// normalise the suffix away before comparing goldens across platforms.
    /// </remarks>
    public const string UnknownService = "unknown_service:dotnet";

    private readonly Func<long, string> _formatTimestamp;

    /// <summary>
    /// Returns the pinned service name, or <see cref="UnknownService"/> when the host pinned none.
    /// </summary>
    // serviceName! : on netstandard2.0 string.IsNullOrWhiteSpace carries no
    // [NotNullWhen(false)] annotation, so the compiler cannot narrow the
    // non-empty branch and reports CS8603. The branch is provably non-null.
    public static string ServiceNameOrUnknown(string? serviceName)
        => string.IsNullOrWhiteSpace(serviceName) ? UnknownService : serviceName!;

    /// <summary>Creates a mapper that formats timestamps as ISO 8601 via the monotonic clock.</summary>
    public CanonicalEntryMapper()
        : this(MonotonicClock.ToIso8601)
    {
    }

    /// <summary>Creates a mapper with an injected timestamp formatter.</summary>
    /// <param name="formatTimestamp">
    /// Converts an event's raw tick reading to the string written into the
    /// canonical entry. The seam for deterministic output in tests and
    /// conformance fixtures.
    /// </param>
    public CanonicalEntryMapper(Func<long, string> formatTimestamp)
    {
        _formatTimestamp = formatTimestamp;
    }

    /// <summary>Maps a supported <see cref="TraceEvent"/> to a canonical entry.</summary>
    public CanonicalEntry FromEvent(TraceEvent traceEvent)
    {
        return traceEvent switch
        {
            EnterEvent e => FromEnter(e),
            ExitEvent e => FromExit(e),
            ForkCreatedEvent e => FromForkCreated(e),
            MergeEvent e => FromMerge(e),
            FireAndForgetEvent e => FromFireAndForget(e),
            _ => throw new ArgumentException(
                "No canonical entry for event type: "
                    + traceEvent.GetType().Name,
                nameof(traceEvent)),
        };
    }

    /// <summary>Maps a <see cref="ForkCreatedEvent"/> to a <c>fork</c> entry.</summary>
    private CanonicalEntry FromForkCreated(ForkCreatedEvent e)
    {
        return ConcurrencyEntry(e.TimestampTicks, "fork", e.GroupId);
    }

    /// <summary>Maps a <see cref="MergeEvent"/> to a <c>join</c> entry.</summary>
    /// <remarks>
    /// The event's member count and wall time stay off the entry: the canonical
    /// schema has no field for them and every other port emits the group id
    /// alone, so adding them here would fork the shared format. They remain
    /// available on the node metadata for renderers.
    /// </remarks>
    private CanonicalEntry FromMerge(MergeEvent e)
    {
        return ConcurrencyEntry(e.TimestampTicks, "join", e.GroupId);
    }

    /// <summary>Maps a <see cref="FireAndForgetEvent"/> to an <c>async_dispatch</c> entry.</summary>
    private CanonicalEntry FromFireAndForget(FireAndForgetEvent e)
    {
        return ConcurrencyEntry(e.TimestampTicks, "async_dispatch", e.GroupId);
    }

    /// <summary>
    /// A concurrency entry: timestamp, type and group id only.
    /// </summary>
    /// <remarks>
    /// These signals belong to a fork group rather than to any one span, so
    /// they deliberately carry no span, code or outcome identity — the
    /// surrounding method entries hold that.
    /// </remarks>
    private CanonicalEntry ConcurrencyEntry(
        long timestampTicks, string eventType, string forkId)
    {
        return new CanonicalEntry(
            Timestamp: _formatTimestamp(timestampTicks),
            Level: "trace",
            Message: $"{eventType} [{forkId}]",
            Service: UnknownService,
            Environment: null,
            TraceId: null,
            SpanId: null,
            ParentSpanId: null,
            CodeNamespace: null,
            CodeFunction: null,
            NtEventType: eventType,
            NtForkId: forkId);
    }

    private CanonicalEntry FromEnter(EnterEvent e)
    {
        var sc = e.SpanContext;
        var sig = e.Signature;
        return new CanonicalEntry(
            Timestamp: _formatTimestamp(e.TimestampTicks),
            Level: "trace",
            Message: FormatEnterMessage(sig),
            Service: ServiceNameOrUnknown(sc.ServiceName),
            Environment: sc.Environment,
            TraceId: sc.TraceId.Value, SpanId: sc.SpanId.Value,
            ParentSpanId: sc.ParentSpanId?.Value,
            CodeNamespace: sig.ClassName, CodeFunction: sig.MethodName,
            NtEventType: "method_enter",
            NtTraceName: NullIfEmpty(sc.TraceId.HumanName),
            NtStoryId: sc.StoryId, NtChapterId: sc.ChapterId,
            NtParameters: MapParameters(sig),
            NtNarrationTemplate: sig.NarrationTemplate,
            NtPackage: sig.Namespace, NtReturnType: sig.ReturnType);
    }

    private CanonicalEntry FromExit(ExitEvent e)
    {
        var sc = e.SpanContext;
        var outcome = e.Outcome;
        var (className, methodName) = SplitSpanName(sc.SpanName);
        return new CanonicalEntry(
            Timestamp: _formatTimestamp(e.TimestampTicks),
            Level: outcome is Threw ? "error" : "trace",
            Message: FormatExitMessage(sc, outcome),
            Service: ServiceNameOrUnknown(sc.ServiceName), Environment: sc.Environment,
            TraceId: sc.TraceId.Value, SpanId: sc.SpanId.Value,
            ParentSpanId: sc.ParentSpanId?.Value,
            CodeNamespace: className, CodeFunction: methodName,
            NtEventType: "method_exit",
            NtTraceName: NullIfEmpty(sc.TraceId.HumanName),
            NtStoryId: sc.StoryId, NtChapterId: sc.ChapterId,
            NtOutcome: MapOutcome(outcome),
            NtReturnValue: (outcome as Returned)?.RenderedValue,
            ExceptionType: (outcome as Threw)?.Error?.GetType().Name,
            ExceptionMessage: ExceptionMessage.Of((outcome as Threw)?.Error),
            NtExceptionPackage: (outcome as Threw)?.Error?.GetType().Namespace);
    }

    private static string FormatEnterMessage(MethodSignature sig)
    {
        var sb = new StringBuilder();
        sb.Append('→').Append(' ')
            .Append(sig.ClassName).Append('.')
            .Append(sig.MethodName).Append('(');
        for (var i = 0; i < sig.Parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            var p = sig.Parameters[i];
            sb.Append(p.Name).Append(": ")
                .Append(p.Redacted ? "[REDACTED]" : p.RenderedValue);
        }

        return sb.Append(')').ToString();
    }

    private static string FormatExitMessage(
        SpanContext sc, TraceOutcome outcome)
    {
        if (outcome is Threw t)
        {
            return "!! " + t.Error?.GetType().Name
                + ": " + ExceptionMessage.Text(t.Error);
        }

        var name = sc.SpanName ?? "method";
        if (outcome is Returned { RenderedValue: { } value })
        {
            return "← " + name + " returned " + value;
        }

        return outcome is Incomplete
            ? "← " + name + " incomplete"
            : "← " + name + " returned";
    }

    private static string? MapOutcome(TraceOutcome outcome)
    {
        return outcome switch
        {
            Returned => "success",
            Threw => "failure",
            Incomplete => "incomplete",
            _ => null,
        };
    }

    private static (string, string) SplitSpanName(string? spanName)
    {
        var name = spanName ?? "";
        var dot = name.IndexOf('.');
        return dot > 0
            ? (name.Substring(0, dot), name.Substring(dot + 1))
            : ("", name);
    }

    private static IReadOnlyList<ParameterCapture>? MapParameters(
        MethodSignature sig)
    {
        if (sig.Parameters.Count == 0)
        {
            return null;
        }

        var list = new List<ParameterCapture>(sig.Parameters.Count);
        foreach (var p in sig.Parameters)
        {
            list.Add(
                p.Redacted
                    ? p with { RenderedValue = "[REDACTED]" }
                    : p);
        }

        return list;
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
