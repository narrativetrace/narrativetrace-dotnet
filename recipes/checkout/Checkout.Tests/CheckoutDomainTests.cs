// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>The scenario domain itself: plain code, no tracing, so the recipes stand on a checkout that works.</summary>
public sealed class CheckoutDomainTests
{
    private readonly InMemoryInventory _stock = new("SKU-KB", 5);
    private readonly InMemoryOrderRepository _repository = new();

    private DefaultOrderService Checkout() => new(_stock, new CardPaymentService(), _repository);

    [Fact]
    public void A_paid_order_is_saved_and_the_stock_stays_reserved()
    {
        var orderId = Checkout().PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242");

        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        Assert.Equal(new Order(orderId, "C-1234", "SKU-KB", 2, 7998), Assert.Single(_repository.Saved));
        Assert.Equal(3, _stock.AvailableUnits("SKU-KB"));
    }

    [Fact]
    public void A_declined_card_releases_the_stock_and_saves_nothing()
    {
        var decline = Assert.Throws<PaymentDeclinedException>(
            () => Checkout().PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

        Assert.Equal("card declined by the issuer", decline.Message);
        Assert.Empty(_repository.Saved);
        Assert.Equal(5, _stock.AvailableUnits("SKU-KB"));
    }

    [Fact]
    public void Ordering_more_than_the_stock_fails_before_the_card_is_charged()
    {
        Assert.Throws<InvalidOperationException>(
            () => Checkout().PlaceOrder("C-1234", "SKU-KB", 6, CardPaymentService.DecliningCard));

        Assert.Empty(_repository.Saved);
        Assert.Equal(5, _stock.AvailableUnits("SKU-KB"));
    }

    [Fact]
    public void An_unknown_sku_is_out_of_stock_and_releasing_it_changes_nothing()
    {
        Assert.Equal(0, _stock.AvailableUnits("SKU-MOUSE"));
        Assert.Throws<InvalidOperationException>(() => _stock.Reserve("SKU-MOUSE", 1));

        _stock.Release("SKU-MOUSE", 3);

        Assert.Equal(5, _stock.AvailableUnits("SKU-KB"));
    }

    [Fact]
    public void The_checkout_without_a_release_path_leaves_a_declined_reservation_behind()
    {
        var checkout = new OrderServiceWithoutRelease(_stock, new CardPaymentService(), _repository);

        Assert.Throws<PaymentDeclinedException>(
            () => checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));

        Assert.Equal(3, _stock.AvailableUnits("SKU-KB"));
    }

    [Fact]
    public void The_without_release_checkout_saves_a_paid_order_like_the_real_one()
    {
        var checkout = new OrderServiceWithoutRelease(_stock, new CardPaymentService(), _repository);

        Assert.Equal("ORD-C-1234-SKU-KB-2", checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242"));
        Assert.Single(_repository.Saved);
    }

    [Fact]
    public void A_blank_password_does_not_sign_in()
    {
        Assert.Throws<ArgumentException>(() => new InMemoryAccounts().SignIn("C-1234", " "));
        Assert.Equal("SESSION-C-1234", new InMemoryAccounts().SignIn("C-1234", "anything"));
    }

    [Theory]
    [InlineData(RuleBasedFraudCheck.ReviewCeilingCents - 1, true)]
    [InlineData(RuleBasedFraudCheck.ReviewCeilingCents, false)]
    public void The_fraud_check_refuses_an_amount_that_reaches_the_review_ceiling(int amountCents, bool approved)
    {
        Assert.Equal(approved, new RuleBasedFraudCheck().Approve("C-1234", amountCents));
    }

    [Fact]
    public void A_fraud_refusal_releases_the_reservation_the_parallel_checkout_made()
    {
        var stock = new InMemoryInventory("SKU-KB", 20);
        var checkout = ParallelCheckoutOver(stock);

        var refusal = Assert.Throws<PaymentDeclinedException>(
            () => checkout.PlaceOrder("C-1234", "SKU-KB", 13, "4242424242424242"));

        Assert.Equal("the fraud check refused the order", refusal.Message);
        Assert.Empty(_repository.Saved);
        Assert.Equal(20, stock.AvailableUnits("SKU-KB"));
    }

    [Fact]
    public void A_branch_that_cannot_reserve_fails_the_parallel_checkout_before_it_charges()
    {
        var checkout = ParallelCheckoutOver(_stock);

        Assert.Throws<InvalidOperationException>(() => checkout.PlaceOrder("C-1234", "SKU-KB", 6, CardPaymentService.DecliningCard));

        Assert.Empty(_repository.Saved);
    }

    private ParallelCheckout ParallelCheckoutOver(InMemoryInventory stock) =>
        new(new SyncNarrativeContext(new NarrativeTraceConfig()), stock, new RuleBasedFraudCheck(), new CardPaymentService(), _repository);

    [Fact]
    public async Task A_declined_card_over_http_is_the_same_exception_as_in_process()
    {
        await using var paymentService = await PaymentServiceHost.StartAsync(new StringWriter());
        using var client = new HttpClient();
        var payments = new HttpPaymentService(client, paymentService.Endpoint);

        var decline = Assert.Throws<PaymentDeclinedException>(
            () => payments.Charge("C-1234", 7998, CardPaymentService.DecliningCard));

        Assert.Equal("card declined by the issuer", decline.Message);
        Assert.Null(paymentService.InboundTraceparent);
    }

    [Fact]
    public async Task A_call_made_with_no_trace_to_name_sends_no_traceparent()
    {
        await using var paymentService = await PaymentServiceHost.StartAsync(new StringWriter());
        var untraced = new SyncNarrativeContext(new NarrativeTraceConfig());
        using var client = new HttpClient(new TraceparentPropagationHandler(untraced) { InnerHandler = new HttpClientHandler() });

        var paymentId = new HttpPaymentService(client, paymentService.Endpoint).Charge("C-1234", 7998, "4242424242424242");

        Assert.Equal("PAY-C-1234-7998", paymentId);
        Assert.Null(paymentService.InboundTraceparent);
    }
}
