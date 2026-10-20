// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace Checkout.Fixtures;

/// <summary>
/// The runs recipes/trace-in-production-without-the-cost.md reads at two tracing levels: the declined
/// checkout, whose artifact the page compares at <c>Detail</c> and at <c>Errors</c>, and a checkout
/// that succeeds, which must leave an artifact at <c>Detail</c> and none at <c>Errors</c>. The level
/// is the <c>NARRATIVETRACE_LEVEL</c> the run is started under, so one class serves both columns of
/// the page; the second test asserts both halves of the absence claim, not just the absence.
/// </summary>
public sealed class CostFixture : IDisposable
{
    // A fixed W3C traceparent, so the page's embedded artifacts name the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    // One fixture per test: xUnit builds a fresh class instance for each test, so no test sees another's spans.
    private readonly NarrativeFixture _trace = new();
    private readonly InMemoryInventory _stock = new("SKU-KB", 5);
    private readonly IOrderService _checkout;

    public CostFixture()
    {
        _trace.Context.AdoptTraceparent(Traceparent.Parse(DemoTraceparent));
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(_stock, _trace.Context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), _trace.Context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(new InMemoryOrderRepository(), _trace.Context);
        _checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), _trace.Context);
    }

    public void Dispose() => _trace.Dispose();

    private static bool AtErrors =>
        string.Equals(Environment.GetEnvironmentVariable("NARRATIVETRACE_LEVEL"), "Errors", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void A_declined_card_at_the_configured_level()
    {
        _trace.Run("A declined card at the configured level", _ =>
        {
            Assert.Throws<PaymentDeclinedException>(
                () => _checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardPaymentService.DecliningCard));
            Assert.Equal(5, _stock.AvailableUnits("SKU-KB"));
        });
        _trace.WriteArtifacts(nameof(CostFixture), nameof(A_declined_card_at_the_configured_level), failed: false);

        var artifact = ArtifactOf(nameof(A_declined_card_at_the_configured_level));
        Assert.True(File.Exists(artifact), "the failed run's artifact exists at every level");
        var written = File.ReadAllText(artifact);
        Assert.Contains("[REDACTED]", written, StringComparison.Ordinal);
        Assert.DoesNotContain(CardPaymentService.DecliningCard, written, StringComparison.Ordinal);
    }

    [Fact]
    public void A_successful_checkout_at_the_configured_level()
    {
        _trace.Run("A successful checkout at the configured level", _ =>
            Assert.Equal("ORD-C-1234-SKU-KB-2", _checkout.PlaceOrder("C-1234", "SKU-KB", 2, "4242424242424242")));
        _trace.WriteArtifacts(nameof(CostFixture), nameof(A_successful_checkout_at_the_configured_level), failed: false);

        // Both halves of the claim: the path IS written when the level keeps successful runs, so the
        // absence at Errors is the level's doing and not a path that was never right.
        Assert.Equal(!AtErrors, File.Exists(ArtifactOf(nameof(A_successful_checkout_at_the_configured_level))));
    }

    private static string ArtifactOf(string method)
    {
        var directory = Environment.GetEnvironmentVariable("NARRATIVETRACE_OUTPUT_DIR");
        Assert.False(string.IsNullOrEmpty(directory), "the recipe run names its output directory");
        return Path.Combine(directory, "traces", nameof(CostFixture), method.ToLowerInvariant() + ".md");
    }
}
