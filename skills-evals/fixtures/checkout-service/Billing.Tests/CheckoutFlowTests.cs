// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Billing;
using Contoso.Checkout;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Billing.Tests;

/// <summary>The checkout as production wires it, every collaborator traced.</summary>
public sealed class CheckoutFlowTests
{
    [Fact]
    public void Customer_checks_out()
    {
        using var trace = new NarrativeFixture();
        var failed = true;
        try
        {
            var app = Checkout.Compose(new TracingWrapper(trace.Context));

            var invoiceId = app.Checkout.Checkout(new Order("ORD-7", "C-1234", 4599));

            Assert.Equal("INV-ORD-7", invoiceId);
            Assert.Equal(["AUTH-C-1234"], app.Payments.Confirmed);
            failed = false;
        }
        finally
        {
            trace.WriteArtifacts(nameof(CheckoutFlowTests), nameof(Customer_checks_out), failed);
        }
    }
}
