// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// A card number passed to a traced method is redacted BY VALUE, whatever the
/// parameter is called — the checkout scenario's <c>Charge</c> takes it under
/// an innocuous name, so only the value shape can catch it.
/// </summary>
public class CardNumberParameterRedactionTests
{
    private const string CardNumber = "4111111111111111";
    private const string LuhnInvalidOrderNumber = "4111111111111112";

    [Fact]
    public void A_luhn_valid_card_number_is_redacted_under_an_innocuous_parameter_name()
    {
        var tree = TraceChargeWith(CardNumber);

        var parameter = tree.Roots[0].Signature.Parameters[1];
        Assert.Equal("reference", parameter.Name);
        Assert.DoesNotContain(CardNumber, parameter.RenderedValue, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, parameter.RenderedValue, StringComparison.Ordinal);
    }

    [Fact]
    public void A_card_number_never_reaches_any_renderer()
    {
        var tree = TraceChargeWith(CardNumber);

        var outputs = new[]
        {
            IndentedTextRenderer.Render(tree),
            MarkdownRenderer.Render(tree),
            ProseRenderer.Render(tree),
            JsonExporter.Export(tree, new TraceMetadata("Card redaction", ScenarioResult.Success)),
        };

        Assert.All(outputs, output => Assert.DoesNotContain(CardNumber, output, StringComparison.Ordinal));
    }

    [Fact]
    public void A_number_that_fails_the_luhn_check_stays_visible()
    {
        var tree = TraceChargeWith(LuhnInvalidOrderNumber);

        var parameter = tree.Roots[0].Signature.Parameters[1];
        Assert.Contains(LuhnInvalidOrderNumber, parameter.RenderedValue, StringComparison.Ordinal);
    }

    private static TraceTree TraceChargeWith(string reference)
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IPaymentGateway>(new PaymentGateway(), ctx);

        proxy.Charge("ORD-1", reference);

        return ctx.CaptureTrace();
    }

    public interface IPaymentGateway
    {
        void Charge(string orderId, string reference);
    }

    private sealed class PaymentGateway : IPaymentGateway
    {
        public void Charge(string orderId, string reference)
        {
        }
    }
}
