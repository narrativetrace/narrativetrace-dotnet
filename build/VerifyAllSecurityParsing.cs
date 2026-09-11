// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Structured finding counts for the security scanners <c>VerifyAll</c> runs for real — each
/// reads that scanner's own JSON report rather than re-deriving a count from console text, so a
/// parser disagreeing with the tool's own report is a parser bug to fix, not a number to trust.
/// Returns <c>null</c> (never <c>0</c>) when the report is absent or not the shape expected — the
/// schema's own rule for a metric that cannot honestly be read.
/// </summary>
public static class SecurityReportParsing
{
    /// <summary>gitleaks <c>--report-format json</c>: a top-level array, one element per finding.</summary>
    public static int? CountGitleaksFindings(string reportPath)
    {
        if (!File.Exists(reportPath))
            return null;
        var text = File.ReadAllText(reportPath).Trim();
        if (text.Length == 0)
            return 0; // gitleaks omits the file entirely on some versions when there is nothing to report
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.GetArrayLength() : null;
    }

    /// <summary>Semgrep <c>--json</c>: <c>{"results": [...], "errors": [...], ...}</c> — findings are
    /// the length of <c>results</c>; <c>errors</c> is scan-tooling errors, not code findings.</summary>
    public static int? CountSemgrepFindings(string reportPath)
    {
        if (!File.Exists(reportPath))
            return null;
        using var doc = JsonDocument.Parse(File.ReadAllText(reportPath));
        return doc.RootElement.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array
            ? results.GetArrayLength()
            : null;
    }

    /// <summary>
    /// OSV-Scanner <c>scan source --format json</c>: <c>{"results": [{"packages": [{"vulnerabilities":
    /// [...] }] }] }</c>. Findings are counted as distinct vulnerability ids across every package —
    /// the same "distinct advisory ids" shape java's own SCA row description uses, not a raw
    /// per-package-occurrence count that could double-count one advisory hit by two lockfile entries.
    /// </summary>
    public static int? CountOsvFindings(string reportPath)
    {
        if (!File.Exists(reportPath))
            return null;
        var text = File.ReadAllText(reportPath).Trim();
        if (text.Length == 0)
            return 0; // osv-scanner writes no file at all when nothing was found on some versions
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            return null;

        var ids = new HashSet<string>();
        foreach (var result in results.EnumerateArray())
        {
            if (!result.TryGetProperty("packages", out var packages))
                continue;
            foreach (var package in packages.EnumerateArray())
            {
                if (!package.TryGetProperty("vulnerabilities", out var vulns))
                    continue;
                foreach (var vuln in vulns.EnumerateArray())
                {
                    if (vuln.TryGetProperty("id", out var id) && id.GetString() is { } idText)
                        ids.Add(idText);
                }
            }
        }
        return ids.Count;
    }
}

/// <summary>
/// Counts Roslyn/SonarAnalyzer diagnostics straight from <c>dotnet build</c>'s own console text —
/// MSBuild's console logger prints one line per diagnostic (<c>path(line,col): severity RULEID:
/// message [project]</c>), which carries a real, parseable rule id even though nothing in this
/// build writes a structured SARIF/JSON diagnostics file today. Used to split one <c>Analyze</c>
/// invocation's output into the <c>lint</c>/<c>complexity</c>/<c>sast</c> rows' own finding counts.
/// </summary>
public static class AnalyzeDiagnosticsSupport
{
    private static readonly Regex DiagnosticLine = new(
        @":\s*(warning|error)\s+([A-Za-z]+[0-9]+)\s*:", RegexOptions.Compiled);

    /// <summary>Every <c>(severity, ruleId)</c> pair MSBuild printed, one per diagnostic occurrence
    /// (a rule firing on 3 lines of the same file counts 3 times, matching how PMD/SpotBugs count
    /// findings in the Java precedent).</summary>
    public static IReadOnlyList<(string Severity, string RuleId)> ParseDiagnostics(string buildOutput) =>
        DiagnosticLine.Matches(buildOutput)
            .Select(m => (m.Groups[1].Value, m.Groups[2].Value.ToUpperInvariant()))
            .ToList();

    private static readonly Regex ComplexityRule = new("^S138$", RegexOptions.Compiled);
    private static readonly Regex SecurityRule = new("^CA5[0-9]{3}$", RegexOptions.Compiled);

    public static bool IsComplexityRule(string ruleId) => ComplexityRule.IsMatch(ruleId);
    public static bool IsSecurityRule(string ruleId) => SecurityRule.IsMatch(ruleId);

    /// <summary>Every diagnostic that is neither <see cref="IsComplexityRule"/> (its own
    /// <c>complexity</c> row) nor <see cref="IsSecurityRule"/> (its own <c>sast</c> row) — the
    /// non-security, non-complexity style/correctness findings that are <c>lint</c>'s own count.</summary>
    public static int CountLintFindings(IReadOnlyList<(string Severity, string RuleId)> diagnostics) =>
        diagnostics.Count(d => !IsComplexityRule(d.RuleId) && !IsSecurityRule(d.RuleId));

    public static int CountComplexityFindings(IReadOnlyList<(string Severity, string RuleId)> diagnostics) =>
        diagnostics.Count(d => IsComplexityRule(d.RuleId));

    public static int CountSecurityFindings(IReadOnlyList<(string Severity, string RuleId)> diagnostics) =>
        diagnostics.Count(d => IsSecurityRule(d.RuleId));

    /// <summary>Whether at least one non-complexity, non-security diagnostic is at <c>error</c>
    /// severity — the actual reason a plain style/correctness finding would fail <c>Analyze</c>'s
    /// own exit code (e.g. CS1591 under <c>src/</c>), independent of whatever S138/CA5xxx also found.</summary>
    public static bool HasLintErrors(IReadOnlyList<(string Severity, string RuleId)> diagnostics) =>
        diagnostics.Any(d => d.Severity == "error" && !IsComplexityRule(d.RuleId) && !IsSecurityRule(d.RuleId));
}
