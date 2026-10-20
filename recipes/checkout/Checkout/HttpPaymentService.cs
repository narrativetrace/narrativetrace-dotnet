// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

/// <summary>
/// The checkout's payment collaborator when the payment service is a different process: an ordinary
/// HTTP client call. It knows nothing about tracing — the client it is given carries the
/// <c>traceparent</c> header through a delegating handler, so this class stays the code a project
/// already has.
/// </summary>
public sealed class HttpPaymentService(HttpClient client, Uri endpoint) : IPaymentService
{
    private const int DeclinedStatus = 402;

    public string Charge(string customerId, int amountCents, string card)
    {
        using var body = new StringContent($"{customerId},{amountCents},{card}");
        // SendAsync, not the synchronous Send: a handler's SendAsync override only runs on this path.
        using var response = client.PostAsync(endpoint, body).GetAwaiter().GetResult();
        var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        return (int)response.StatusCode == DeclinedStatus ? throw new PaymentDeclinedException(text) : text;
    }
}
