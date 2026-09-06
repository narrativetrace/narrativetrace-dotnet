// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;

namespace NarrativeTrace.Examples.ECommerce;

/// <summary>
/// The clarity pitch: the <em>same</em> behavior written with descriptive names
/// versus cryptic ones, scored side by side by the reflection-only
/// <see cref="ClarityScanner"/>. Descriptive names score higher.
/// </summary>
public static class NamingComparison
{
    /// <summary>Returns the overall clarity score of each version.</summary>
    public static (double Clear, double Cryptic) Compare()
    {
        var clear = ClarityScanner.Scan([typeof(OrderFulfillmentService)]);
        var cryptic = ClarityScanner.Scan([typeof(Svc)]);
        return (
            clear[nameof(OrderFulfillmentService)].Overall,
            cryptic[nameof(Svc)].Overall);
    }

    /// <summary>Descriptive names — the code reads like the domain.</summary>
    public static class OrderFulfillmentService
    {
        public static decimal CalculateOrderTotal(decimal unitPrice, int quantity)
        {
            return unitPrice * quantity;
        }

        public static bool ConfirmCustomerOrder(
            string customerId, string productId, int quantity)
        {
            return quantity > 0 && customerId.Length > 0 && productId.Length > 0;
        }
    }

    /// <summary>The same behavior, cryptically named.</summary>
    public static class Svc
    {
        public static decimal Calc(decimal x, int n)
        {
            return x * n;
        }

        public static bool Do(string a, string b, int n)
        {
            return n > 0 && a.Length > 0 && b.Length > 0;
        }
    }
}
