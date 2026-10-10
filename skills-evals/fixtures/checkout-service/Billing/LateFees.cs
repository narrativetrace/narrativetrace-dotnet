// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Billing;

/// <summary>What an overdue invoice costs extra.</summary>
public static class LateFees
{
    /// <summary>The late fee, in cents, for an invoice paid <paramref name="daysLate"/> days late.</summary>
    public static long FeeFor(int daysLate) => daysLate <= 0 ? 0 : 150L * daysLate;
}
