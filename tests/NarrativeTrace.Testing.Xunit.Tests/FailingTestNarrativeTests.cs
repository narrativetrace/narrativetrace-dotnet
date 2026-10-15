// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

/// <summary>
/// A failing checkout test explains itself: the fixture prints the narrative
/// where the docs say (the console writer) and the failing call is visible in it.
/// </summary>
public class FailingTestNarrativeTests
{
    [Fact]
    public void A_failing_checkout_prints_its_narrative_with_the_failing_call_visible()
    {
        using var fixture = new NarrativeFixture();
        var writer = new StringWriter();

        var ex = Record.Exception(() => fixture.Run("Checkout_declines_the_card", ctx =>
        {
            ctx.EnterMethod("InventoryService", "Reserve", []);
            ctx.ExitMethodWithReturn("true");
            ctx.EnterMethod("PaymentGateway", "Charge", []);
            var declined = new InvalidOperationException("card declined");
            ctx.ExitMethodWithException(declined);
            throw declined;
        }, writer));

        Assert.IsType<InvalidOperationException>(ex);
        var narrative = writer.ToString();
        Assert.Contains("Checkout declines the card", narrative, StringComparison.Ordinal);
        Assert.Contains("InventoryService", narrative, StringComparison.Ordinal);
        Assert.Contains("PaymentGateway", narrative, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", narrative, StringComparison.Ordinal);
    }

    [Fact]
    public void A_passing_checkout_prints_nothing()
    {
        using var fixture = new NarrativeFixture();
        var writer = new StringWriter();

        fixture.Run("Checkout_succeeds", ctx =>
        {
            ctx.EnterMethod("PaymentGateway", "Charge", []);
            ctx.ExitMethodWithReturn("true");
        }, writer);

        Assert.Equal(string.Empty, writer.ToString());
    }
}
