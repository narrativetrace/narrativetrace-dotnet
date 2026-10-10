// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace Billing;

/// <summary>Converts with today's rate table, keeping the amount exact until the last step.</summary>
public sealed class RateTableConverter(IExchangeRates rates) : ICurrencyConverter
{
    public long Convert(long euroCents, string currency)
    {
        var rate = rates.RateFor(currency);
        var units = Math.Round(euroCents / 100m * rate, MidpointRounding.AwayFromZero);
        return (long)(units * 100);
    }
}
