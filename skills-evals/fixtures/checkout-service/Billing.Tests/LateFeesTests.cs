// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Billing;
using Xunit;

namespace Billing.Tests;

public sealed class LateFeesTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-2, 0)]
    [InlineData(1, 150)]
    [InlineData(3, 450)]
    public void The_fee_grows_by_150_cents_a_day(int daysLate, long fee)
    {
        Assert.Equal(fee, LateFees.FeeFor(daysLate));
    }
}
