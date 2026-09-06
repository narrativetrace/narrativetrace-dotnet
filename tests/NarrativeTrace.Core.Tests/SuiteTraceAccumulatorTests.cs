// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class SuiteTraceAccumulatorTests
{
    private static TraceTree Tree(string method)
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", method, []),
                new Returned(null), [], 0),
        ]);
    }

    [Fact]
    public void Retains_entries_in_insertion_order()
    {
        var acc = new SuiteTraceAccumulator();
        acc.Add("first", Tree("A"));
        acc.Add("second", Tree("B"));

        Assert.Equal(2, acc.Count);
        Assert.Equal("first", acc.Entries[0].Key);
        Assert.Equal("second", acc.Entries[1].Key);
    }

    [Fact]
    public void Retains_duplicate_scenario_names()
    {
        var acc = new SuiteTraceAccumulator();
        acc.Add("places order", Tree("A"));
        acc.Add("places order", Tree("B"));

        Assert.Equal(2, acc.Count);
    }
}
