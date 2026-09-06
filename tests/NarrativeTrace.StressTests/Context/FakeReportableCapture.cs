// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Generic;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using NarrativeTrace.StressTests.Pipeline;

namespace NarrativeTrace.StressTests.Context;

/// <summary>
/// Minimal <see cref="IReportableCapture"/> for racing <see cref="AdoptionLedger"/>
/// directly, without needing a real <c>AsyncNarrativeContext</c>. The .NET
/// stress-suite mirror of jcstress's own bare <c>TraceStack</c> fixtures.
/// </summary>
internal sealed class FakeReportableCapture : IReportableCapture
{
    private readonly List<TraceEvent> _events = [];

    internal FakeReportableCapture(int ceiling = AdoptionLedger.DefaultCeiling)
    {
        Ledger = new AdoptionLedger(this, ceiling);
    }

    internal AdoptionLedger Ledger { get; }

    /// <summary>Gives this capture <paramref name="count"/> of its own spans.</summary>
    internal void PublishSpans(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _events.Add(StressEvents.Tagged(i));
        }
    }

    public void CollectReportable(List<TraceEvent> into)
    {
        into.AddRange(_events);
        Ledger.CollectReportable(into);
    }
}
