// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Examples.ECommerce;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Examples.ECommerce.Tests;

public sealed class ECommerceExampleTests
{
    private static (SyncNarrativeContext Context, IOrderService Orders) Build(
        int stock, decimal approvalLimit)
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
        var orders = ECommerceExample.BuildTracedOrderService(context, stock, approvalLimit);
        return (context, orders);
    }

    [Fact]
    public void Successful_order_returns_a_result_and_records_a_nested_narrative()
    {
        var (context, orders) = Build(stock: 5, approvalLimit: 1000m);

        var result = orders.PlaceOrder("cust-1", "book-123", 2);

        Assert.Equal(2, result.Quantity);
        Assert.Equal(19.98m, result.Total);
        var root = Assert.Single(context.CaptureTrace().Roots);
        Assert.Equal("PlaceOrder", root.Signature.MethodName);
        Assert.Equal(4, root.Children.Count);
    }

    [Fact]
    public void Payment_over_the_limit_is_declined_and_the_narrative_captures_the_charge()
    {
        var (context, orders) = Build(stock: 5, approvalLimit: 10m);

        Assert.Throws<PaymentDeclinedException>(
            () => orders.PlaceOrder("cust-1", "book-123", 2));

        var root = Assert.Single(context.CaptureTrace().Roots);
        Assert.Contains(root.Children, c => c.Signature.MethodName == "Charge");
    }

    [Fact]
    public void Insufficient_stock_fails_at_reservation_before_any_payment()
    {
        var (context, orders) = Build(stock: 1, approvalLimit: 1000m);

        Assert.Throws<InvalidOperationException>(
            () => orders.PlaceOrder("cust-1", "book-123", 2));

        var root = Assert.Single(context.CaptureTrace().Roots);
        Assert.Contains(root.Children, c => c.Signature.MethodName == "Reserve");
        Assert.DoesNotContain(root.Children, c => c.Signature.MethodName == "Charge");
    }

    [Fact]
    public void Descriptive_names_score_higher_for_clarity_than_cryptic_names()
    {
        var (clear, cryptic) = NamingComparison.Compare();

        Assert.True(clear > cryptic, $"clear={clear} should beat cryptic={cryptic}");
    }
}
