// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// <see cref="RunScope"/> is process-wide ambient state, so every test here
/// restores it to <see langword="null"/> in a <c>finally</c> block — leaving it
/// set would leak into whichever other test happens to read it next. No other
/// class in this assembly reads or writes <see cref="RunScope"/>, so xUnit's
/// default (tests within one class run sequentially) already keeps these
/// isolated without a named collection.
/// </summary>
public class RunScopeTests
{
    [Fact]
    public void Current_is_null_before_any_run_begins()
    {
        RunScope.End();

        Assert.Null(RunScope.Current);
    }

    [Fact]
    public void Begin_publishes_the_identity_current_returns()
    {
        var run = RunIdentity.Generate();
        try
        {
            RunScope.Begin(run);

            Assert.Same(run, RunScope.Current);
        }
        finally
        {
            RunScope.End();
        }
    }

    [Fact]
    public void End_clears_the_active_run()
    {
        RunScope.Begin(RunIdentity.Generate());

        RunScope.End();

        Assert.Null(RunScope.Current);
    }

    [Fact]
    public void End_is_safe_to_call_when_no_run_is_active()
    {
        RunScope.End();

        var exception = Record.Exception(RunScope.End);

        Assert.Null(exception);
    }

    [Fact]
    public void Begin_rejects_a_null_identity()
    {
        Assert.Throws<ArgumentNullException>(() => RunScope.Begin(null!));
    }
}
