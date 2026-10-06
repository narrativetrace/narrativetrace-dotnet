// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Action = NarrativeTrace.Tooling.Init.Action;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The contract <see cref="Refuse"/> already holds — a refusal carries a reason — held on the other
/// side too, where a refusal is recorded rather than planned. Found by the adversarial pass: a
/// hand-built refusal with no detail rendered as an empty parenthesis nobody could act on. The answer
/// is that a refusal nobody can explain must not exist. Method names mirror the Java test.
/// </summary>
public class ExecutionReportAdversarialM3Test
{
    private static readonly Action TheAction = new CreateFile("AGENTS.md", "# Agents\n");

    [Fact]
    public void RefusesToRecordARefusalThatCarriesNoReason()
    {
        Assert.Contains("reason",
            Assert.Throws<ArgumentException>(() =>
                new ExecutionReport.Applied(TheAction, ExecutionReport.Status.Refused, "   ")).Message,
            StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() =>
            new ExecutionReport.Applied(TheAction, ExecutionReport.Status.Refused, ""));
    }

    /// <summary>An APPLIED result has nothing to explain, so its empty detail stays legal.</summary>
    [Fact]
    public void AnAppliedResultNeedsNoDetail()
    {
        var applied = new ExecutionReport.Applied(TheAction, ExecutionReport.Status.Applied, "");

        Assert.Equal("", applied.Detail);
    }
}
