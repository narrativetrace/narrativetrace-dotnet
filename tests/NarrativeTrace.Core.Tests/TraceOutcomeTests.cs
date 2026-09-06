// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceOutcomeTests
{
    [Fact]
    public void Returned_holds_rendered_value()
    {
        var outcome = new Returned("42");

        Assert.Equal("42", outcome.RenderedValue);
    }

    [Fact]
    public void Threw_holds_exception()
    {
        var error = new InvalidOperationException("boom");

        var outcome = new Threw(error);

        Assert.Same(error, outcome.Error);
    }

    [Fact]
    public void Incomplete_is_distinct_TraceOutcome_variant()
    {
        var outcome = new Incomplete();

        Assert.IsAssignableFrom<TraceOutcome>(outcome);
        Assert.IsNotType<Returned>(outcome);
        Assert.IsNotType<Threw>(outcome);
    }
}
