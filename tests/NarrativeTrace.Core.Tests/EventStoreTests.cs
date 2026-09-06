// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class EventStoreTests
{
    private static EnterEvent Enter(string cls, string method)
    {
        var sc = new SpanContext(
            new TraceId("0af7651916cd43dd8448eb211c80319c"),
            new SpanId("b7ad6b7169203331"),
            null);
        return new EnterEvent(
            sc, 1000L, new MethodSignature(cls, method, []));
    }

    [Fact]
    public void Add_then_events_returns_the_event()
    {
        var store = new EventStore();
        var evt = Enter("Svc", "Run");

        store.Add(evt);

        Assert.Single(store.Events());
        Assert.Same(evt, store.Events()[0]);
    }

    [Fact]
    public void Events_returns_defensive_snapshot()
    {
        var store = new EventStore();
        store.Add(Enter("Svc", "Run"));

        var snapshot = store.Events();
        store.Add(Enter("Svc", "Again"));

        Assert.Single(snapshot);
    }

    [Fact]
    public void Clear_empties_the_store()
    {
        var store = new EventStore();
        store.Add(Enter("Svc", "Run"));

        store.Clear();

        Assert.Empty(store.Events());
    }

    [Fact]
    public async Task Concurrent_add_and_read_do_not_corrupt_the_store()
    {
        var store = new EventStore();
        using var start = new Barrier(2);
        Exception? error = null;

        var writer = Task.Run(() =>
        {
            start.SignalAndWait();
            try
            {
                for (var i = 0; i < 5000; i++)
                {
                    store.Add(Enter("Svc", "Run"));
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref error, ex, null);
            }
        });
        var reader = Task.Run(() =>
        {
            start.SignalAndWait();
            try
            {
                for (var i = 0; i < 3000; i++)
                {
                    _ = store.Events().Count;
                }
            }
            catch (Exception ex)
            {
                Interlocked.CompareExchange(ref error, ex, null);
            }
        });

        var all = Task.WhenAll(writer, reader);
        Assert.Same(all, await Task.WhenAny(all, Task.Delay(10_000)));
        await all;
        Assert.Null(error);
        Assert.Equal(5000, store.Events().Count);
    }
}
