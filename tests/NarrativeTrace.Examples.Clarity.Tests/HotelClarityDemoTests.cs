// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Examples.Clarity.Tests;

public sealed class HotelClarityDemoTests
{
    [Fact]
    public void Report_flags_the_generic_verb_class_as_a_high_issue()
    {
        var report = HotelClarityDemo.RenderReport();

        Assert.Contains("HIGH", report, StringComparison.Ordinal);
        Assert.Contains("method-name", report, StringComparison.Ordinal);
        Assert.Contains("DataProcessor.Execute", report, StringComparison.Ordinal);
    }
}
