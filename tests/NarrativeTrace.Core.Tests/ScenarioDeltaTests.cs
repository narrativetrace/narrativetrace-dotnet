// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class ScenarioDeltaTests
{
    [Fact]
    public void Null_baseline_means_the_scenario_is_new()
    {
        var delta = ScenarioDelta.Of("Places an order", null, "scenario: x\n\n- A.b()\n");

        Assert.Equal(ScenarioDeltaKind.New, delta.Kind);
        Assert.Equal(string.Empty, delta.Summary);
        Assert.Equal(string.Empty, delta.Diff);
    }

    [Fact]
    public void Byte_identical_baseline_and_current_is_unchanged()
    {
        const string doc = "scenario: x\n\n- A.b()\n";

        var delta = ScenarioDelta.Of("Places an order", doc, doc);

        Assert.Equal(ScenarioDeltaKind.Unchanged, delta.Kind);
        Assert.Equal(string.Empty, delta.Summary);
    }

    [Fact]
    public void Differing_baseline_and_current_is_changed_and_carries_the_delta()
    {
        var baseline = "scenario: x\n\n- A.b()\n";
        var current = "scenario: x\n\n- A.b()\n- A.c()\n";

        var delta = ScenarioDelta.Of("Places an order", baseline, current);

        Assert.Equal(ScenarioDeltaKind.Changed, delta.Kind);
        Assert.Equal("+1 call A.c", delta.Summary);
        Assert.NotEqual(string.Empty, delta.Diff);
    }

    [Fact]
    public void Of_rejects_a_null_scenario_or_current_document()
    {
        Assert.Throws<ArgumentNullException>(() => ScenarioDelta.Of(null!, null, "x"));
        Assert.Throws<ArgumentNullException>(() => ScenarioDelta.Of("s", null, null!));
    }
}
