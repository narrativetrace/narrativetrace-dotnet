// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ValueReferenceIndexTests
{
    [Fact]
    public void An_unreferenced_value_displays_unchanged()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned("ok"), [], 0),
        ]);

        var index = ValueReferenceIndex.Build(tree);

        Assert.Equal("ok", index.Display("ok"));
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. Build must terminate rather than recurse the call stack
    // forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void Build_does_not_hang_on_a_cyclic_tree()
    {
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned("ok"), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned("ok"), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        var index = ValueReferenceIndex.Build(new TraceTree([a]));

        Assert.Equal("unrelated", index.Display("unrelated"));
    }
}
