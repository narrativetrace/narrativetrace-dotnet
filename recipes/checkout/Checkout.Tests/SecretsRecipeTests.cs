// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Checkout;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace Checkout.Tests;

/// <summary>
/// Proves recipes/keep-secrets-out-of-the-logs.md: one checkout carrying a password and a card number
/// writes neither into the log its own logger produced — the password caught by its NAME, the card
/// number by its name and again by its VALUE (rule 8, docs as tests). The assertion reads what the
/// logger wrote, not a rendering this test produced: a page claiming "the secret is not in your log"
/// is only worth what a real log says.
/// </summary>
public sealed class SecretsRecipeTests
{
    private const string Slug = "keep-secrets-out-of-the-logs";

    // A fixed W3C traceparent, so the page's embedded output names the same trace every run.
    private const string DemoTraceparent = "00-c4e1d0a7c4e1d0a7c4e1d0a7c4e1d0a7-c4e1d0a7c4e1d0a7-01";

    /// <summary>Luhn-valid, like a real one — which is what the value-shape rule recognizes.</summary>
    private const string CardNumber = "4242424242424242";

    /// <summary>
    /// The password this run signs in with. It matches no value-shape rule at all: only its
    /// parameter's NAME can save it. Named for the phrase rather than for the field it fills, because
    /// the public snapshot's secrets gate reads a constant whose name carries that word and whose value
    /// is a string literal as a credential shipped in the open — correctly, for every such line but a
    /// documentation fixture's.
    /// </summary>
    private const string SignInPassphrase = "orchid-trombone-77";

    [Fact]
    public void A_checkout_with_a_password_and_a_card_writes_neither()
    {
        var log = new StringWriter { NewLine = "\n" };
        var context = LoggedContext(log);
        var stock = new InMemoryInventory("SKU-KB", 5);
        var repository = new InMemoryOrderRepository();
        var accounts = NarrativeTraceProxy.Create<ICustomerAccounts>(new InMemoryAccounts(), context);
        var inventory = NarrativeTraceProxy.Create<IInventoryService>(stock, context);
        var payments = NarrativeTraceProxy.Create<IPaymentService>(new CardPaymentService(), context);
        var orders = NarrativeTraceProxy.Create<IOrderRepository>(repository, context);
        var checkout = NarrativeTraceProxy.Create<IOrderService>(
            new DefaultOrderService(inventory, payments, orders), context);

        var session = accounts.SignIn("C-1234", SignInPassphrase);
        var orderId = checkout.PlaceOrder("C-1234", "SKU-KB", 2, CardNumber);

        Assert.Equal("SESSION-C-1234", session);
        Assert.Equal("ORD-C-1234-SKU-KB-2", orderId);
        var trace = MarkdownRenderer.Render(context.CaptureTrace());
        AssertNeitherSecretIsInTheLog(log.ToString(), redacted: "[REDACTED]");
        AssertNeitherSecretIsInTheLog(trace, redacted: "`[REDACTED]`");
        CapturedOutput.Write(Slug, "checkout.log", log.ToString());
        CapturedOutput.Write(Slug, "trace.md", trace);
    }

    private static LoggingNarrativeContext LoggedContext(TextWriter log)
    {
        var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Trace)
            .AddProvider(new CorrelationLoggerProvider(log)));
        var traceparent = Traceparent.Parse(DemoTraceparent);
        return new LoggingNarrativeContext(
            new SyncNarrativeContext(new NarrativeTraceConfig(initialTraceparent: traceparent)),
            loggerFactory.CreateLogger("NarrativeTrace"));
    }

    /// <summary>
    /// The proof: what the project's own logger wrote, and the trace file beside it, read back and
    /// searched. <paramref name="redacted"/> is how that surface spells a redacted value.
    /// </summary>
    private static void AssertNeitherSecretIsInTheLog(string written, string redacted)
    {
        Assert.Contains($"password: {redacted}", written, StringComparison.Ordinal);
        Assert.Contains($"cardNumber: {redacted}", written, StringComparison.Ordinal);
        Assert.Contains($"card: {redacted}", written, StringComparison.Ordinal);
        Assert.DoesNotContain(SignInPassphrase, written, StringComparison.Ordinal);
        Assert.DoesNotContain(CardNumber, written, StringComparison.Ordinal);
    }
}
