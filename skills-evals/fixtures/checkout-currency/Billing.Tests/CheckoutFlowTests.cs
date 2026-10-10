// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Billing;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Billing.Tests;

/// <summary>The checkout as production wires it, every collaborator traced.</summary>
public sealed class CheckoutFlowTests
{
    [Fact]
    public void Customer_checks_out_in_euro()
    {
        using var trace = new NarrativeFixture();
        var failed = true;
        try
        {
            var app = Checkout.Compose(new TracingWrapper(trace.Context));

            var invoiceId = app.Checkout.Checkout(new Order("ORD-12", "C-1001", 4500));

            Assert.Equal("INV-ORD-12", invoiceId);
            Assert.Equal([("C-1001", 4500L, "EUR")], app.Payments.Charged);
            failed = false;
        }
        finally
        {
            trace.WriteArtifacts(nameof(CheckoutFlowTests), nameof(Customer_checks_out_in_euro), failed);
        }
    }
}
