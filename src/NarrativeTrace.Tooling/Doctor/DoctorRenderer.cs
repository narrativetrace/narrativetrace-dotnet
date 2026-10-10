// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Tooling.Doctor;

/// <summary>
/// Renders a <see cref="DoctorReport"/> as human-readable text or as JSON.
/// </summary>
public static class DoctorRenderer
{
    private const string FixIndent = "            ";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>Renders one line per finding, then a pass/fail summary line.</summary>
    public static string RenderHuman(DoctorReport report)
    {
        var builder = new StringBuilder();
        foreach (var finding in report.Findings)
        {
            AppendFinding(builder, finding);
        }

        var failed = report.Findings.Count(f => !f.Passed);
        builder.Append(
            CultureInfo.InvariantCulture,
            $"{report.Findings.Count - failed}/{report.Findings.Count} checks passed.");
        return builder.ToString();
    }

    private static void AppendFinding(StringBuilder builder, DoctorFinding finding)
    {
        var mark = finding.Passed ? "PASS" : "FAIL";
        builder.AppendLine(
            CultureInfo.InvariantCulture, $"[{mark}] {finding.Id}: {finding.Message}");
        if (!finding.Passed)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"       fix: {IndentContinuation(finding.Fix)}");
            if (finding.Skill is not null)
            {
                builder.AppendLine(CultureInfo.InvariantCulture, $"       skill: {finding.Skill}");
            }

            builder.AppendLine(CultureInfo.InvariantCulture, $"       see: {finding.DocUrl}");
        }
    }

    /// <summary>
    /// A multi-line fix (a framework fix ends in its wiring snippet) with every continuation line
    /// indented under the label and a trailing newline dropped; a blank or whitespace-only line renders empty.
    /// </summary>
    private static string IndentContinuation(string fix)
    {
        var lines = fix.TrimEnd('\n').Split('\n');
        return string.Join('\n', lines.Select((line, i) => ContinuationLine(line, isFirst: i == 0)));
    }

    private static string ContinuationLine(string line, bool isFirst)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return string.Empty;
        }

        return isFirst ? line : FixIndent + line;
    }

    /// <summary>Renders the report as indented JSON.</summary>
    public static string RenderJson(DoctorReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }
}
