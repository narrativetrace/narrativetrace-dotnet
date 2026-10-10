// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Billing;
using Contoso.Checkout;
using Xunit;

namespace Billing.Tests;

public sealed class CheckoutServiceTests
{
    [Fact]
    public void Checkout_returns_the_invoice_it_paid_and_confirms_the_payment()
    {
        var app = Checkout.Compose(new NoWrapping());

        Assert.Equal("INV-ORD-1", app.Checkout.Checkout(new Order("ORD-1", "C-9", 1000)));
        Assert.Equal(["AUTH-C-9"], app.Payments.Confirmed);
    }

    [Fact]
    public void Every_checkout_is_audited()
    {
        var app = Checkout.Compose(new NoWrapping());

        app.Checkout.Checkout(new Order("ORD-2", "C-9", 1000));

        Assert.Equal(["checked out ORD-2 as INV-ORD-2"], app.Audit.Entries);
    }
}
