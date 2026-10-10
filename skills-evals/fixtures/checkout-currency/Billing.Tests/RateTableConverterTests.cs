// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Billing;
using Xunit;

namespace Billing.Tests;

public sealed class RateTableConverterTests
{
    private static readonly RateTableConverter Converter = new(new DailyRates());

    [Theory]
    [InlineData(10000, "CHF", 9300)]
    [InlineData(5000, "GBP", 4300)]
    [InlineData(4500, "EUR", 4500)]
    public void Converts_with_todays_rate(long euroCents, string currency, long expected)
    {
        Assert.Equal(expected, Converter.Convert(euroCents, currency));
    }

    [Fact]
    public void A_currency_without_a_rate_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Converter.Convert(100, "JPY"));
    }
}
