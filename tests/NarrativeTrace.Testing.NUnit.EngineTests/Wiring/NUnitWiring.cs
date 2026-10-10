// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingNUnit;
using NUnit.Framework;

namespace Example.Orders;

// The framework table's NUnit wiring, compiled and run here under the real NUnit engine. The doctor's
// config.nunit-base fix prints the wiring region verbatim (carried by wiring-snippets.md).

// snippet:begin wiring
[TestFixture]
public sealed class OrderTraceTests : NarrativeTestBase // a fresh Context per test; the narrative prints on failure
{
    [Test]
    public void Places_an_order()
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), Context);

        Assert.That(orders.PlaceOrder("cust-1"), Is.EqualTo("confirmed:cust-1"));
        Assert.That(Context.CaptureTrace().Roots, Has.Count.EqualTo(1));
    }
}
// snippet:end wiring

/// <summary>The fixture's one service boundary.</summary>
public interface IOrderService
{
    /// <summary>Places an order for <paramref name="customerId"/>.</summary>
    string PlaceOrder(string customerId);
}

/// <summary>The fixture's implementation of <see cref="IOrderService"/>.</summary>
public sealed class OrderService : IOrderService
{
    /// <inheritdoc />
    public string PlaceOrder(string customerId) => $"confirmed:{customerId}";
}
