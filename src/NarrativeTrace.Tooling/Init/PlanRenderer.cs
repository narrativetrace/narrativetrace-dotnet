// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Turns a plan or a report into the three things a caller needs: human text, a unified diff for
/// <c>--dry-run</c>, and JSON for whatever wraps the command.
/// </summary>
/// <remarks>
/// INTENT: the same envelope shape the doctor already emits — <c>{carrier, actions[], exitCode}</c>,
/// camelCase — so a skill that gates on one can gate on the other without learning a second format.
/// JSON is written by hand: this library takes zero dependencies. Paths are already <c>/</c>-separated
/// on every platform (normalized at action construction), so a report is the same text wherever it ran.
/// </remarks>
public static class PlanRenderer
{
    /// <summary>
    /// The whole of what a caller shows for a plan it has NOT applied: the JSON envelope, or the
    /// summary followed by the unified diff — both, because the summary alone says nothing about what
    /// would change and the diff alone says nothing when there is nothing to change.
    /// </summary>
    public static string Render(InitPlan plan, bool json) =>
        json ? RenderJson(plan) : RenderText(plan) + RenderDiff(plan);

    /// <summary>The whole of what a caller shows for a plan it has applied.</summary>
    public static string Render(ExecutionReport report, bool json) =>
        json ? RenderJson(report) : RenderText(report);

    /// <summary>One line per action, refusals marked, with the carrier and the counts at the top.</summary>
    public static string RenderText(InitPlan plan)
    {
        Require(plan);
        var output = new StringBuilder();
        output.Append("narrativetrace — ").Append(plan.Carrier).Append('\n');
        foreach (var warning in plan.Warnings)
        {
            output.Append(warning).Append('\n');
        }

        if (plan.IsEmpty)
        {
            return output.Append("nothing to do.\n").ToString();
        }

        output.Append(plan.Actions.Count).Append(" action(s), ")
            .Append(plan.Refusals().Count).Append(" refusal(s)\n\n");
        foreach (var action in plan.Actions)
        {
            output.Append(Pad(action.Kind)).Append(action.Path).Append(Reason(action)).Append('\n');
        }

        return output.ToString();
    }

    /// <summary>One line per action, each saying whether it happened.</summary>
    public static string RenderText(ExecutionReport report)
    {
        Require(report);
        var refused = Refused(report);
        var output = new StringBuilder();
        output.Append("narrativetrace — ").Append(report.Carrier).Append('\n');
        output.Append(report.Results.Count - refused).Append(" applied, ").Append(refused).Append(" refused\n\n");
        foreach (var result in report.Results)
        {
            output.Append(Pad(result.ActionStatus == ExecutionReport.Status.Applied ? "applied" : "refused"))
                .Append(Pad(result.Action.Kind))
                .Append(result.Action.Path)
                .Append(result.Detail.Length == 0 ? "" : " — " + result.Detail)
                .Append('\n');
        }

        return output.ToString();
    }

    /// <summary>The full unified diff a dry run shows: one file at a time, in plan order.</summary>
    public static string RenderDiff(InitPlan plan)
    {
        Require(plan);
        var output = new StringBuilder();
        foreach (var action in plan.Actions)
        {
            if (action is FileEdit edit)
            {
                output.Append(UnifiedDiff.Render(action.Path, edit.Before, edit.After));
            }
            else
            {
                output.Append("# ").Append(action.Path).Append(" — ").Append(Describe(action)).Append('\n');
            }
        }

        return output.ToString();
    }

    /// <summary><c>{"carrier", "actions":[{kind,path,status}], "exitCode"}</c> for a plan not yet applied.</summary>
    public static string RenderJson(InitPlan plan)
    {
        Require(plan);
        var output = new StringBuilder();
        Open(output, plan.Carrier);
        for (var i = 0; i < plan.Actions.Count; i++)
        {
            var action = plan.Actions[i];
            var status = action is Refuse ? "refused" : "planned";
            Row(output, action, status, i == plan.Actions.Count - 1);
        }

        return Close(output, plan.ExitCode);
    }

    /// <summary>The same envelope for a plan that has been applied.</summary>
    public static string RenderJson(ExecutionReport report)
    {
        Require(report);
        var output = new StringBuilder();
        Open(output, report.Carrier);
        for (var i = 0; i < report.Results.Count; i++)
        {
            var result = report.Results[i];
            var status = result.ActionStatus == ExecutionReport.Status.Applied ? "applied" : "refused";
            Row(output, result.Action, status, i == report.Results.Count - 1);
        }

        return Close(output, report.ExitCode);
    }

    private static void Open(StringBuilder output, string carrier) =>
        output.Append("{\n  \"carrier\": ").Append(Quote(carrier)).Append(",\n  \"actions\": [\n");

    private static void Row(StringBuilder output, Action action, string status, bool last) =>
        output.Append("    {\"kind\": ").Append(Quote(action.Kind))
            .Append(", \"path\": ").Append(Quote(action.Path))
            .Append(", \"status\": ").Append(Quote(status))
            .Append('}')
            .Append(last ? "\n" : ",\n");

    private static string Close(StringBuilder output, int exitCode) =>
        output.Append("  ],\n  \"exitCode\": ").Append(exitCode).Append("\n}\n").ToString();

    private static int Refused(ExecutionReport report) =>
        report.Results.Count(result => result.ActionStatus == ExecutionReport.Status.Refused);

    private static string Describe(Action action) =>
        action is Refuse refusal ? "refused: " + refusal.Reason : action.Kind;

    private static string Reason(Action action) =>
        action is Refuse refusal ? " — " + refusal.Reason : "";

    private static string Pad(string word) =>
        word.Length >= 8 ? word + " " : (word + "        ")[..8];

    private static void Require(object? rendered)
    {
        if (rendered is null)
        {
            throw new ArgumentException("there is nothing to render");
        }
    }

    /// <summary>Quotes and escapes a JSON string: the paths and reasons here are the only free text.</summary>
    private static string Quote(string value)
    {
        var output = new StringBuilder(value.Length + 2).Append('"');
        foreach (var character in value)
        {
            Escape(output, character);
        }

        return output.Append('"').ToString();
    }

    private static void Escape(StringBuilder output, char character)
    {
        if (character is '"' or '\\')
        {
            output.Append('\\').Append(character);
        }
        else if (character < 0x20)
        {
            output.Append("\\u").Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
        }
        else
        {
            output.Append(character);
        }
    }
}
