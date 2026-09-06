// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.StressTests.Context;

/// <summary>
/// Invariant 7 — the adoption/item-44 seams under concurrency: a live
/// child's spans are reportable through exactly one route (never neither,
/// never both) while its scope closes, and a ceiling refusal is always
/// all-or-nothing, never a partial batch. The .NET mirror of Java's
/// <c>LiveChildHandOverTest</c> and <c>AdoptionCeilingTest</c>, run directly
/// against <see cref="NarrativeTrace.Runtime.AdoptionLedger"/> — the
/// registry both <c>AsyncNarrativeContext</c> and <c>SyncNarrativeContext</c>
/// share for this seam.
/// </summary>
/// <remarks>
/// <b>Expected safe, verified rather than assumed.</b> Unlike Java's
/// <c>TraceStack</c>, where <c>adopt()</c> and <c>unregisterLiveChild()</c>
/// are two separate synchronized calls with a hand-over window between them
/// that jcstress's <c>LiveChildHandOverTest</c> exists to police,
/// <see cref="NarrativeTrace.Runtime.AdoptionLedger.Adopt"/> already
/// performs the release-from-live and add-to-adopted steps under one lock
/// in one call — there is no window to race into by construction. These
/// tests hold that claim to real concurrent contention rather than trusting
/// the single-threaded reading of the source.
/// </remarks>
[Trait("Category", "Stress")]
public sealed class AdoptionSeamStressTests
{
    [Fact]
    public void Live_child_is_always_reportable_exactly_once_across_adoption()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            var origin = new FakeReportableCapture();
            var child = new FakeReportableCapture();
            child.PublishSpans(1);
            var registration = origin.Ledger.Register(child);
            var observed = new ConcurrentBag<int>();
            var done = 0;

            StressRace.RunOnce(
                () =>
                {
                    origin.Ledger.Adopt(child, registration);
                    Volatile.Write(ref done, 1);
                },
                () =>
                {
                    while (Volatile.Read(ref done) == 0)
                    {
                        observed.Add(ReportableCount(origin));
                    }
                });

            Assert.All(observed, count => Assert.Equal(1, count));
            Assert.Equal(1, ReportableCount(origin));
        }
    }

    [Fact]
    public void Ceiling_refusal_is_all_or_nothing_never_partial()
    {
        for (var trial = 0; trial < StressIterations.Count; trial++)
        {
            var origin = new FakeReportableCapture(ceiling: 4);
            var first = new FakeReportableCapture();
            first.PublishSpans(3);
            var second = new FakeReportableCapture();
            second.PublishSpans(3);
            var firstRegistration = origin.Ledger.Register(first);
            var secondRegistration = origin.Ledger.Register(second);

            StressRace.RunOnce(
                () => origin.Ledger.Adopt(first, firstRegistration),
                () => origin.Ledger.Adopt(second, secondRegistration));

            var adopted = ReportableCount(origin);
            Assert.True(adopted == 3, $"trial {trial}: adopted {adopted} spans, expected exactly one whole batch (3), never a partial one");
            Assert.Equal(1, origin.Ledger.RefusedScopeCount);
            Assert.Equal(3, origin.Ledger.RefusedSpanCount);
        }
    }

    private static int ReportableCount(FakeReportableCapture origin)
    {
        var events = new List<TraceEvent>();
        origin.Ledger.CollectReportable(events);
        return events.Count;
    }
}
