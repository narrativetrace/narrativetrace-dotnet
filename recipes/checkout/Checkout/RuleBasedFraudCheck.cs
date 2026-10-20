// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Checkout;

/// <summary>Approves an order unless its amount reaches the review ceiling.</summary>
public sealed class RuleBasedFraudCheck : IFraudCheckService
{
    public const int ReviewCeilingCents = 50_000;

    public bool Approve(string customerId, int amountCents) => amountCents < ReviewCeilingCents;
}
