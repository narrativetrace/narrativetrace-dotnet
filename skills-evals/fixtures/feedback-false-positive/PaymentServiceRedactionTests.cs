// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.TestingXunit;
using Xunit;

/// <summary>
/// This project's redaction proof, and it passes: the deny-listed parameter is hidden, the ordinary
/// one survives. It asserts the marker through the library's own public constant rather than
/// retyping its text, so the value a renderer writes and the value this test expects cannot drift
/// apart. That is the whole reason to reference a constant.
/// </summary>
public sealed class PaymentServiceRedactionTests
{
    [Fact]
    public void The_auth_token_is_redacted_and_the_other_arguments_survive()
    {
        using var narrative = new NarrativeFixture();

        narrative.Run("charge a customer", context =>
        {
            var payments = NarrativeTraceProxy.Create<IPaymentService>(new PaymentService(), context);
            payments.Charge("C-1234", "tok_live_9a8b7c6d5e4f", "42.00");
        });

        var rendered = IndentedTextRenderer.Render(narrative.CaptureTrace());
        Assert.Contains(RedactionPolicy.Marker, rendered);
        Assert.DoesNotContain("tok_live_9a8b7c6d5e4f", rendered);
        Assert.Contains("C-1234", rendered);
    }
}
