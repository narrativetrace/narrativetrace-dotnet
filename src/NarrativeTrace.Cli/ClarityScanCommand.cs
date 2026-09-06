// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;
using NarrativeTrace.Glossary;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>clarity-scan</c> verb: reflection-only scans an assembly and writes a
/// clarity report to an output directory. <c>--format</c> selects the artifacts:
/// <c>json</c> writes <see cref="ResultsFileName"/>, <c>md</c> writes
/// <see cref="ReportFileName"/>, and <c>both</c> (the default) writes both.
/// See CLARITY-4/-5.
/// </summary>
/// <remarks>
/// The <c>clarity-scan-</c> prefix is deliberate. A static scan and a test run
/// answer different questions — depth-1 reflection over an assembly's surface
/// versus real captured traces — so they must be able to coexist in one output
/// directory. Writing <c>clarity-results.json</c> here would silently destroy
/// whatever <c>NarrativeTrace.Core.SuiteReportWriter</c> or
/// <see cref="ClarityAggregateCommand"/> had already put there, which is the
/// file the <c>clarity-check</c> gate reads.
/// </remarks>
public static class ClarityScanCommand
{
    /// <summary>Name of the JSON report written into the output directory.</summary>
    public const string ResultsFileName = "clarity-scan-results.json";

    /// <summary>Name of the Markdown suite report written into the output directory.</summary>
    public const string ReportFileName = "clarity-scan-report.md";

    private static readonly string[] ValidFormats = ["both", "md", "json"];

    /// <summary>Scans <paramref name="assemblyPath"/> and writes the report(s); returns the exit code.</summary>
    public static int Run(
        string assemblyPath, string outputDir, string format,
        TextWriter output, TextWriter error)
    {
        if (Array.IndexOf(ValidFormats, format) < 0)
        {
            error.WriteLine(
                $"error: unknown --format '{format}' (expected: both, md, or json)");
            return 2;
        }

        if (!File.Exists(assemblyPath))
        {
            error.WriteLine($"error: assembly not found: {assemblyPath}");
            return 1;
        }

        var report = ScanToReport(assemblyPath, ResolveGlossaryFile());
        var primary = WriteReports(report, outputDir, format);
        output.WriteLine($"Scanned {report.Count} type(s) -> {primary}");
        return 0;
    }

    /// <summary>
    /// The repository's committed glossary, found by the same upward search the
    /// suite harvest uses, so a scan scores in the project's own vocabulary. No
    /// glossary is a normal state: the scan then uses the built-in dictionaries
    /// alone.
    /// </summary>
    private static string? ResolveGlossaryFile()
    {
        return GlossarySettings.ResolveFile(
            Environment.GetEnvironmentVariable, Directory.GetCurrentDirectory());
    }

    private static List<ScenarioClarity> ScanToReport(
        string assemblyPath, string? glossaryFilePath)
    {
        var results = AssemblyClarityScanner.Scan(assemblyPath, glossaryFilePath);
        var report = new List<ScenarioClarity>(results.Count);
        foreach (var pair in results)
        {
            report.Add(new ScenarioClarity(pair.Key, pair.Value));
        }

        return report;
    }

    private static string WriteReports(
        List<ScenarioClarity> report, string outputDir, string format)
    {
        Directory.CreateDirectory(outputDir);
        string? primary = null;
        if (format is "both" or "json")
        {
            primary = Write(outputDir, ResultsFileName, ClarityJsonExporter.ExportReport(report));
        }

        if (format is "both" or "md")
        {
            var md = Write(outputDir, ReportFileName, ClarityReportRenderer.Render(report));
            primary ??= md;
        }

        return primary!;
    }

    private static string Write(string outputDir, string fileName, string content)
    {
        var path = Path.Combine(outputDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }
}
