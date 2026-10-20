// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace OrdersApp.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public void Placing_an_order_confirms_it_for_the_customer()
    {
        var service = new OrderService();

        Assert.Equal("confirmed:cust-1:book-123:2", service.PlaceOrder("cust-1", "book-123", 2));
    }
}
