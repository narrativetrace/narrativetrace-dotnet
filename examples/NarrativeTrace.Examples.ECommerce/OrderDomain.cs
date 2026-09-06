// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.ECommerce;

/// <summary>Loyalty tier that influences discounts and service.</summary>
public enum CustomerTier
{
    Standard,
    Premium,
}

/// <summary>A customer placing an order.</summary>
public sealed record Customer(string Id, string Name, CustomerTier Tier);

/// <summary>Proof that a payment was captured.</summary>
public sealed record PaymentConfirmation(string TransactionId, decimal Amount);

/// <summary>The outcome of a successfully placed order.</summary>
public sealed record OrderResult(
    string OrderId, string TransactionId, decimal Total, int Quantity);

/// <summary>Thrown when a payment is refused by the gateway.</summary>
public sealed class PaymentDeclinedException(string message) : Exception(message);

/// <summary>Thrown when an external dependency (here, the notification gateway) is unavailable.</summary>
public sealed class ExternalServiceException(string message) : Exception(message);
