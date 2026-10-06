// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Applied = NarrativeTrace.Tooling.Init.ExecutionReport.Applied;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Boundary coverage for the two <c>Render(..., bool)</c> entry points: an EMPTY plan and a report
/// that is ALL refusals — the shapes <c>PlanRendererTest</c> exercises for the individual
/// <c>RenderText</c>/<c>RenderJson</c> methods but not through the one-decision <c>Render</c> overload
/// every real caller uses. Method names mirror the Java test.
/// </summary>
public class PlanRendererAdversarialM3Test
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private static InitPlan Plan(params NarrativeTrace.Tooling.Init.Action[] actions) =>
        new(Coordinate, false, actions);

    [Fact]
    public void RenderOfAnEmptyPlanMatchesItsOwnJsonOrTextPlusDiffEitherWay()
    {
        var empty = Plan();

        Assert.Equal(PlanRenderer.RenderText(empty) + PlanRenderer.RenderDiff(empty),
            PlanRenderer.Render(empty, false));
        Assert.Equal(PlanRenderer.RenderJson(empty), PlanRenderer.Render(empty, true));
    }

    [Fact]
    public void RenderOfAReportWithOnlyRefusalsMatchesItsOwnJsonOrText()
    {
        var allRefused = new ExecutionReport(
            Coordinate,
            [Applied.Refused(new Refuse("AGENTS.md", "two sections"), "two sections")]);

        Assert.Equal(PlanRenderer.RenderText(allRefused), PlanRenderer.Render(allRefused, false));
        Assert.Equal(PlanRenderer.RenderJson(allRefused), PlanRenderer.Render(allRefused, true));
    }
}
