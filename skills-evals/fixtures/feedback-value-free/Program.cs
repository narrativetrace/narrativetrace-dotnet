// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

var context = new SyncNarrativeContext(new NarrativeTraceConfig());
var payments = NarrativeTraceProxy.Create<IPaymentService>(new PaymentService(), context);

payments.Charge("C-1234", "tok_live_9a8b7c6d5e4f", "42.00");

Console.WriteLine(IndentedTextRenderer.Render(context.CaptureTrace()));

public interface IPaymentService
{
    string Charge(string customerId, string authToken, string amount);
}

public sealed class PaymentService : IPaymentService
{
    public string Charge(string customerId, string authToken, string amount) => $"PAY-{customerId}";
}
