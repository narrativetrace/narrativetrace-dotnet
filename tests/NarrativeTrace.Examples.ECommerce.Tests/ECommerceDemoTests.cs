// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Examples.ECommerce;
using Xunit;

namespace NarrativeTrace.Examples.ECommerce.Tests;

/// <summary>Pins the shape of the tutorial run the demo launcher walks.</summary>
public sealed class ECommerceDemoTests
{
    private static async Task<string> RunDemo()
    {
        using var output = new StringWriter();
        using var run = new DemoRun(new ConsoleLoggerFactory(output, LogFormat.Bare), "ecommerce");
        await ECommerceDemo.RunAsync(run);
        return output.ToString();
    }

    [Fact]
    public async Task Run_walks_the_six_scenarios_in_order_with_their_renderings()
    {
        var text = await RunDemo();

        var headers = text.Split('\n').Where(l => l.StartsWith("=== ", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            [
                "=== Scenario 1: Successful Order + Async Notification ===",
                "=== Scenario 2: Payment Failure — Inventory Leak Bug ===",
                "=== Scenario 3: Flaky External Service ===",
                "=== Scenario 4: Unknown Customer ===",
                "=== Scenario 5: Out of Stock ===",
                "=== Scenario 6: Explicit Async Trace Capture ===",
            ],
            headers);
        Assert.Equal(5, text.Split("--- Trace tree ---").Length - 1);
        Assert.Equal(5, text.Split("--- Prose ---").Length - 1);
        Assert.Equal(2, text.Split("--- Mermaid ---").Length - 1);
        Assert.Equal(1, text.Split("--- PlantUML ---").Length - 1);
    }

    [Fact]
    public async Task Container_wired_services_stream_live_with_narration_redaction_and_error_context()
    {
        var text = await RunDemo();

        Assert.Contains("→ IOrderService.PlaceOrder(customerId: \"cust-1\", productId: \"book-123\", quantity: 2)", text, StringComparison.Ordinal);
        Assert.Contains("Placing order of 2 book-123 for customer cust-1", text, StringComparison.Ordinal);
        Assert.Contains("cardToken: [REDACTED]", text, StringComparison.Ordinal);
        Assert.Contains("← returned: true", text, StringComparison.Ordinal);
        Assert.Contains("!! PaymentDeclinedException: Amount 449.97 exceeds approval limit 100 [Payment declined for customer cust-1, amount was 449.97]", text, StringComparison.Ordinal);
        Assert.Contains("IInventoryService.Release is missing", text, StringComparison.Ordinal);
        Assert.Contains("!! ExternalServiceException: External notification service unavailable (call #2) [Failed to notify customer cust-1 about order ORD-00002]", text, StringComparison.Ordinal);
        Assert.Contains("[Customer cust-unknown not found]", text, StringComparison.Ordinal);
        Assert.Contains("[Insufficient stock for usb-hub, requested 9999]", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_async_scenario_shows_the_shared_story_and_the_concurrency_groups()
    {
        var text = await RunDemo();

        var scenario6 = text[text.IndexOf("=== Scenario 6", StringComparison.Ordinal)..];
        Assert.Contains("LookupPrice(productId: \"mechanical-keyboard\")", scenario6, StringComparison.Ordinal);
        Assert.Contains("⑂ fork group created", scenario6, StringComparison.Ordinal);
        Assert.Contains("⑃ fork joined", scenario6, StringComparison.Ordinal);
        Assert.Contains("⤳ fire-and-forget launched", scenario6, StringComparison.Ordinal);
    }
}
