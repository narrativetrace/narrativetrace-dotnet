// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NarrativeTrace.Build;

/// <summary>
/// The fixed shape every <c>verifyAll</c>-equivalent report commits to across the NarrativeTrace
/// family (pro repo tracker item 35E), written once in the golden Java repo's
/// <c>reports/verification/SCHEMA.md</c> — this is this port's implementation of that contract, not
/// a second definition of it: same field names, same four statuses, same 21 category ids. A
/// category this runtime genuinely lacks still gets a row (<c>not-implemented</c>), never a missing
/// one. See <see cref="VerifyAllSupport"/> for the orchestration that fills these rows and
/// <c>Build.cs</c>'s <c>VerifyAll</c> target for the sequence.
/// </summary>
public static class VerificationSchema
{
    /// <summary>The 21 fixed category ids, in the order SCHEMA.md declares them. No more, no fewer.</summary>
    public static readonly IReadOnlyList<string> Categories =
    [
        "unit-tests", "coverage", "mutation", "property", "fuzz-tier-a", "fuzz-tier-b",
        "benchmarks", "allocation", "architecture", "stress-short", "stress-long",
        "conformance", "secrets", "sast", "sca", "lint", "format", "types", "complexity",
        "translation", "clarity",
    ];

    /// <summary>The four-value status vocabulary. Never a fifth.</summary>
    public static readonly IReadOnlyList<string> Statuses = ["passed", "failed", "skipped", "not-implemented"];
}

/// <summary>One row of <c>reports/verification/&lt;date&gt;.json</c>'s <c>categories</c> array.</summary>
/// <param name="Category">One id from <see cref="VerificationSchema.Categories"/>.</param>
/// <param name="Tool">The concrete tool and version that produced this row, or <c>"none"</c> when <paramref name="Status"/> is <c>not-implemented</c>.</param>
/// <param name="Status">One of <see cref="VerificationSchema.Statuses"/>.</param>
/// <param name="Metrics">Whatever numbers this category actually has; <c>{}</c> when there is nothing to measure.</param>
/// <param name="DurationSeconds">Wall-clock seconds this category's own command(s) took; <c>0</c> when derived from another category's run with no additional invocation.</param>
/// <param name="Note">Why a row is skipped/not-implemented, a real constraint worth recording, or <c>null</c>.</param>
public sealed record CategoryResult(
    string Category,
    string Tool,
    string Status,
    IReadOnlyDictionary<string, object> Metrics,
    double DurationSeconds,
    string? Note);

/// <summary>One full run — the in-memory model <see cref="VerificationReportSupport.WriteJson"/> serializes.</summary>
public sealed record VerificationRun(
    string Runtime,
    string Version,
    string Commit,
    string Host,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    IReadOnlyList<CategoryResult> Categories)
{
    /// <summary><c>"failed"</c> iff at least one row failed — a skipped/not-implemented row never taints it.</summary>
    public string OverallStatus => Categories.Any(c => c.Status == "failed") ? "failed" : "passed";
}

// ---------------------------------------------------------------------------------- wire DTOs

internal sealed record ReportCategoryJson(
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("tool")] string Tool,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("metrics")] IReadOnlyDictionary<string, object> Metrics,
    [property: JsonPropertyName("duration_seconds")] double DurationSeconds,
    [property: JsonPropertyName("note")] string? Note);

internal sealed record ReportJson(
    [property: JsonPropertyName("runtime")] string Runtime,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("commit")] string Commit,
    [property: JsonPropertyName("host")] string Host,
    [property: JsonPropertyName("started_at")] string StartedAt,
    [property: JsonPropertyName("ended_at")] string EndedAt,
    [property: JsonPropertyName("overall_status")] string OverallStatus,
    [property: JsonPropertyName("categories")] IReadOnlyList<ReportCategoryJson> Categories);

/// <summary>
/// Writes <c>reports/verification/&lt;date&gt;.json</c> and renders its Markdown twin — the one
/// writer and the one renderer, so the two files can never quietly disagree. Mirrors Java's
/// <c>ai.narrativetrace.build.VerificationReportSupport</c> (buildSrc) and TypeScript's
/// <c>tools/verify-all-schema.ts</c>.
/// </summary>
public static class VerificationReportSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static ReportJson ToReportJson(VerificationRun run) => new(
        run.Runtime, run.Version, run.Commit, run.Host,
        run.StartedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture),
        run.EndedAt.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", System.Globalization.CultureInfo.InvariantCulture),
        run.OverallStatus,
        run.Categories.Select(c => new ReportCategoryJson(
            c.Category, c.Tool, c.Status, c.Metrics, c.DurationSeconds, c.Note)).ToList());

    /// <summary>Writes <paramref name="path"/>, creating parent directories as needed.</summary>
    public static void WriteJson(VerificationRun run, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(ToReportJson(run), JsonOptions);
        File.WriteAllText(path, json + "\n");
    }

    /// <exception cref="JsonException">When <paramref name="path"/> is missing or not valid JSON.</exception>
    private static ReportJson ReadJson(string path) =>
        JsonSerializer.Deserialize<ReportJson>(File.ReadAllText(path))
            ?? throw new JsonException($"{path} deserialized to null");

    private static string StatusBadge(string status) => status == "failed" ? "**FAILED**" : status;

    private static string MetricsCell(IReadOnlyDictionary<string, object> metrics) =>
        metrics.Count == 0 ? "—" : string.Join("; ", metrics.Select(kv => $"{kv.Key}={FormatMetricValue(kv.Value)}"));

    private static string FormatMetricValue(object value) => value switch
    {
        JsonElement element => element.ToString(),
        double d => d.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };

    // InvariantCulture, matching FormatMetricValue's own explicit choice a few lines up: the
    // default interpolated-string ":F1" specifier binds CultureInfo.CurrentCulture, which
    // renders a comma decimal separator ("1,0") on a host whose locale uses one (sr-RS, most of
    // continental Europe, ...) -- silently corrupting the Markdown table's Duration column on
    // exactly the machines this was never run on. The JSON twin never had this bug because
    // System.Text.Json always serializes numbers culture-invariant; the Markdown renderer must
    // match it.
    private static string RenderRow(ReportCategoryJson row) =>
        $"| {row.Category} | {row.Tool} | {StatusBadge(row.Status)} | "
            + $"{row.DurationSeconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} | "
            + $"{MetricsCell(row.Metrics)} | {row.Note ?? ""} |";

    /// <summary>
    /// Renders the human table straight from <paramref name="path"/> — reads the just-written JSON
    /// back rather than taking the <see cref="VerificationRun"/> the caller already has, so the
    /// Markdown is provably a rendering of the committed JSON, never a second, independently
    /// computed account of the same run.
    /// </summary>
    public static string RenderMarkdown(string path)
    {
        var root = ReadJson(path);
        var lines = new List<string>
        {
            $"# Verification run — {root.Runtime} {root.Version}",
            "",
            $"- commit: `{root.Commit}`",
            $"- host: {root.Host}",
            $"- started: {root.StartedAt}",
            $"- ended: {root.EndedAt}",
            $"- **overall status: {root.OverallStatus}**",
            "",
            "| Category | Tool | Status | Duration (s) | Metrics | Note |",
            "|---|---|---|---|---|---|",
        };
        lines.AddRange(root.Categories.Select(RenderRow));
        return string.Join("\n", lines) + "\n";
    }
}
