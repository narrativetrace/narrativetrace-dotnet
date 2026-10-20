// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

/// <summary>Signs a customer in before the checkout charges them; the parameter is named <c>password</c> on purpose.</summary>
public sealed class InMemoryAccounts : ICustomerAccounts
{
    public string SignIn(string customerId, string password) =>
        string.IsNullOrWhiteSpace(password)
            ? throw new ArgumentException("password must not be blank", nameof(password))
            : $"SESSION-{customerId}";
}
