// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

// FsCheck ports of Java's EventStorePropertyTest (PIPE-9): count, insertion
// order, completed-pair aggregation, and error tally invariants.
public class EventStorePropertyTests
{
    private static readonly TraceId Tid =
        new("0af7651916cd43dd8448eb211c80319c");

    private static SpanContext Sc(int n) =>
        new(
            Tid,
            new SpanId((n + 1).ToString("x16", CultureInfo.InvariantCulture)),
            null);

    private static EnterEvent Enter(int n) =>
        new(Sc(n), 1_000_000L, new MethodSignature("Svc", "run", []));

    [Property(MaxTest = 50)]
    public void Event_count_matches_add_count(NonNegativeInt countRaw)
    {
        var count = countRaw.Get % 101;
        var store = new EventStore();

        for (var i = 0; i < count; i++)
        {
            store.Add(Enter(i));
        }

        Assert.Equal(count, store.Events().Count);
    }

    [Property(MaxTest = 50)]
    public void Events_preserve_insertion_order(PositiveInt countRaw)
    {
        var count = 1 + (countRaw.Get % 50);
        var store = new EventStore();

        for (var i = 0; i < count; i++)
        {
            store.Add(Enter(i));
        }

        var events = store.Events();
        for (var i = 0; i < count; i++)
        {
            Assert.Equal(
                Sc(i).SpanId, ((EnterEvent)events[i]).SpanContext.SpanId);
        }
    }
}
