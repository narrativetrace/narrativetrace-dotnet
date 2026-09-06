// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Serializes clarity results to the Java-compatible <c>clarity-results.json</c>
/// contract: a <c>{"version","scenarios":[…]}</c> envelope whose scenarios carry
/// <c>name</c>/<c>overallScore</c>-style keys, scores formatted to two decimal
/// places, and upper-case issue severities. See CLARITY-3.
/// </summary>
public static class ClarityJsonExporter
{
    /// <summary>Schema version stamped into the envelope; matches the Java exporter.</summary>
    public const string Version = "1.0";

    private static readonly JsonWriterOptions WriterOptions =
        new() { Indented = true };

    /// <summary>Serializes a single scenario as a Java-keyed object (per-test artifact).</summary>
    public static string Export(ClarityResult result, string scenario)
    {
        return Write(writer => WriteScenario(writer, scenario, result));
    }

    /// <summary>Serializes a suite of scenarios into the versioned envelope.</summary>
    public static string ExportReport(IReadOnlyList<ScenarioClarity> results)
    {
        return Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("version", Version);
            writer.WriteStartArray("scenarios");
            for (var i = 0; i < results.Count; i++)
            {
                WriteScenario(writer, results[i].Scenario, results[i].Result);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });
    }

    private static string Write(Action<Utf8JsonWriter> body)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        body(writer);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteScenario(
        Utf8JsonWriter writer, string scenario, ClarityResult result)
    {
        writer.WriteStartObject();
        writer.WriteString("name", scenario);
        WriteScores(writer, result);
        WriteIssues(writer, result.Issues);
        writer.WriteEndObject();
    }

    private static void WriteScores(Utf8JsonWriter writer, ClarityResult r)
    {
        WriteScore(writer, "overallScore", r.Overall);
        WriteScore(writer, "methodNameScore", r.Method);
        WriteScore(writer, "classNameScore", r.Class);
        WriteScore(writer, "parameterNameScore", r.Parameter);
        WriteScore(writer, "structuralScore", r.Structural);
        WriteScore(writer, "cohesionScore", r.Cohesion);
    }

    private static void WriteScore(Utf8JsonWriter writer, string name, double value)
    {
        writer.WritePropertyName(name);
        writer.WriteRawValue(value.ToString("F2", CultureInfo.InvariantCulture));
    }

    private static void WriteIssues(
        Utf8JsonWriter writer, IReadOnlyList<ClarityIssue> issues)
    {
        writer.WriteStartArray("issues");
        for (var i = 0; i < issues.Count; i++)
        {
            WriteIssue(writer, issues[i]);
        }

        writer.WriteEndArray();
    }

    private static void WriteIssue(Utf8JsonWriter writer, ClarityIssue issue)
    {
        writer.WriteStartObject();
        writer.WriteString("category", issue.Category);
        writer.WriteString("element", issue.Element);
        writer.WriteString("suggestion", issue.Suggestion);
        writer.WriteString("severity", issue.Severity.JsonName());
        writer.WriteNumber("occurrences", issue.Occurrences);
        WriteScore(writer, "impactScore", issue.ImpactScore);
        writer.WriteEndObject();
    }
}
