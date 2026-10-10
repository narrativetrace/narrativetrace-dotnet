// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Examples.SixtySeconds.Tests;

// The smallest test that drives the real path: the collaborators the change touched, wired as
// production wires them, each wrapped with the fixture's context. WriteArtifacts writes the shape to
// bin/<configuration>/<tfm>/TestResults/narrativetrace/structural/<TestClass>/<scenario>.nt and the
// values beside it under traces/; a red run writes the values and keeps the last green .nt.
public sealed class PlaceOrderFlowTests
{
    [Fact]
    public void Customer_places_an_order()
    {
        using var trace = new NarrativeFixture();
        var failed = true;
        try
        {
            var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), trace.Context);

            Assert.Equal("confirmed:cust-1:book-123:2", orders.PlaceOrder("cust-1", "book-123", 2));
            failed = false;
        }
        finally
        {
            trace.WriteArtifacts(nameof(PlaceOrderFlowTests), nameof(Customer_places_an_order), failed);
        }
    }
}
