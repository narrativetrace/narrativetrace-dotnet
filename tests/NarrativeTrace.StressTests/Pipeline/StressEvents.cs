// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Core;

namespace NarrativeTrace.StressTests.Pipeline;

/// <summary>
/// Builds identity-tagged <see cref="EnterEvent"/>s for the pipeline stress
/// races. The .NET mirror of Java's <c>StressEvents</c>.
/// </summary>
/// <remarks>
/// Every event shares one static <see cref="MethodSignature"/> and
/// <see cref="TraceId"/> so per-call allocation stays cheap across the many
/// thousands of trials a stress test runs — the ring/store under test is what
/// is being measured, not the fixture. The tag rides in
/// <see cref="TraceEvent.TimestampTicks"/>, unused for identity by any
/// production code, exactly as Java repurposes its own timestamp field.
/// Events must be built before actors start (in a trial's setup, never inside
/// an actor delegate) — allocating inside the race window would widen the
/// window under test.
/// </remarks>
internal static class StressEvents
{
    private static readonly TraceId Trace =
        new("4bf92f3577b34da6a3ce929d0e0e4736");

    private static readonly MethodSignature Signature =
        new("StressTarget", "Publish", []);

    /// <summary>The span id a given tag maps to — stable and distinct per tag.</summary>
    internal static SpanId SpanIdFor(long tag) =>
        new((tag + 1).ToString("x16", CultureInfo.InvariantCulture));

    /// <summary>One event carrying <paramref name="tag"/> as its identity.</summary>
    internal static EnterEvent Tagged(long tag) =>
        new(new SpanContext(Trace, SpanIdFor(tag), null), tag, Signature);

    /// <summary>A trial's whole publication plan: <paramref name="count"/> events tagged 0..count-1, in order.</summary>
    internal static EnterEvent[] Sequence(int count)
    {
        var events = new EnterEvent[count];
        for (var i = 0; i < count; i++)
        {
            events[i] = Tagged(i);
        }

        return events;
    }

    /// <summary>The tag an event carries, or -1 for a null/foreign event — never throws.</summary>
    /// <remarks>
    /// A torn or null read is one of the outcomes under test; turning it into
    /// an exception would hide the finding instead of reporting it.
    /// </remarks>
    internal static long TagOf(TraceEvent? traceEvent) =>
        traceEvent?.TimestampTicks ?? -1;
}
