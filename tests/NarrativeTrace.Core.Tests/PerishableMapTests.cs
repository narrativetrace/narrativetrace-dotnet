// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class PerishableMapTests
{
    private sealed class FakeClock
    {
        public long Now { get; set; }

        public long Read() => Now;
    }

    private static PerishableMap<string, string> Map(
        int capacity, long ttlTicks, FakeClock clock, Action<string> onEvict)
    {
        return new PerishableMap<string, string>(
            capacity, ttlTicks, onEvict, clock.Read);
    }

    [Fact]
    public void Put_then_get_returns_the_value()
    {
        var map = Map(4, 1000, new FakeClock(), _ => { });

        map.Put("a", "va");

        Assert.Equal("va", map.Get("a"));
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void Get_absent_key_returns_null()
    {
        var map = Map(4, 1000, new FakeClock(), _ => { });

        Assert.Null(map.Get("missing"));
    }

    [Fact]
    public void Remove_returns_and_deletes_the_value()
    {
        var map = Map(4, 1000, new FakeClock(), _ => { });
        map.Put("a", "va");

        Assert.Equal("va", map.Remove("a"));
        Assert.Null(map.Get("a"));
        Assert.Equal(0, map.Count);
    }

    [Fact]
    public void Expired_entries_are_evicted_on_next_put()
    {
        var clock = new FakeClock();
        var evicted = new List<string>();
        var map = Map(4, ttlTicks: 100, clock, evicted.Add);
        map.Put("a", "va");

        clock.Now = 200;
        map.Put("b", "vb");

        Assert.Null(map.Get("a"));
        Assert.Equal("vb", map.Get("b"));
        Assert.Equal(["va"], evicted);
    }

    [Fact]
    public void Over_capacity_put_evicts_the_oldest_entry()
    {
        var clock = new FakeClock();
        var evicted = new List<string>();
        var map = Map(2, ttlTicks: long.MaxValue, clock, evicted.Add);
        clock.Now = 1;
        map.Put("a", "va");
        clock.Now = 2;
        map.Put("b", "vb");

        clock.Now = 3;
        map.Put("c", "vc");

        Assert.Null(map.Get("a"));
        Assert.Equal("vb", map.Get("b"));
        Assert.Equal("vc", map.Get("c"));
        Assert.Equal(["va"], evicted);
    }

    [Fact]
    public void Re_putting_a_key_resets_its_age()
    {
        var clock = new FakeClock();
        var evicted = new List<string>();
        var map = Map(2, ttlTicks: long.MaxValue, clock, evicted.Add);
        clock.Now = 1;
        map.Put("a", "va");
        clock.Now = 2;
        map.Put("b", "vb");

        clock.Now = 3;
        map.Put("a", "va2");
        clock.Now = 4;
        map.Put("c", "vc");

        Assert.Equal("va2", map.Get("a"));
        Assert.Null(map.Get("b"));
        Assert.Equal(["vb"], evicted);
    }

    // PIPE-7: Java's put() evicts the oldest *other* entry on every call, so a
    // re-put at capacity would evict B and fire onEvict. .NET treats a re-put as
    // a non-insertion and evicts nothing — the intentionally divergent contract.
    [Fact]
    public void Re_put_at_capacity_evicts_nothing()
    {
        var clock = new FakeClock();
        var evicted = new List<string>();
        var map = Map(2, ttlTicks: long.MaxValue, clock, evicted.Add);
        clock.Now = 1;
        map.Put("a", "va");
        clock.Now = 2;
        map.Put("b", "vb");

        clock.Now = 3;
        map.Put("a", "va2");

        Assert.Empty(evicted);
        Assert.Equal("va2", map.Get("a"));
        Assert.Equal("vb", map.Get("b"));
        Assert.Equal(2, map.Count);
    }

    [Fact]
    public void Constructor_rejects_non_positive_capacity()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new PerishableMap<string, string>(
                0, TimeSpan.FromSeconds(1), _ => { }));
    }
}
