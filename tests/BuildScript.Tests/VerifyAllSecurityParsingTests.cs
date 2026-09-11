// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>Covers <see cref="SecurityReportParsing"/> and <see cref="AnalyzeDiagnosticsSupport"/> —
/// the readers behind <c>VerifyAll</c>'s <c>secrets</c>/<c>sast</c>/<c>sca</c>/<c>lint</c>/
/// <c>complexity</c> rows.</summary>
public sealed class VerifyAllSecurityParsingTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("nt-security-parsing").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, content);
        return path;
    }

    // --------------------------------------------------------------------------------- gitleaks

    [Fact]
    public void CountGitleaksFindings_counts_the_top_level_array_length()
    {
        var path = Write("gitleaks-report.json", """[{"RuleID": "aws-key"}, {"RuleID": "generic-api-key"}]""");

        Assert.Equal(2, SecurityReportParsing.CountGitleaksFindings(path));
    }

    [Fact]
    public void CountGitleaksFindings_is_zero_for_an_empty_file_not_null()
    {
        var path = Write("gitleaks-report.json", "");

        Assert.Equal(0, SecurityReportParsing.CountGitleaksFindings(path));
    }

    [Fact]
    public void CountGitleaksFindings_is_null_when_the_report_is_absent()
    {
        Assert.Null(SecurityReportParsing.CountGitleaksFindings(Path.Combine(_dir, "missing.json")));
    }

    // ---------------------------------------------------------------------------------- semgrep

    [Fact]
    public void CountSemgrepFindings_counts_the_results_array_not_errors()
    {
        var path = Write("semgrep.json", """{"results": [{"check_id": "a"}], "errors": [1, 2, 3]}""");

        Assert.Equal(1, SecurityReportParsing.CountSemgrepFindings(path));
    }

    // ------------------------------------------------------------------------------- osv-scanner

    [Fact]
    public void CountOsvFindings_counts_distinct_vulnerability_ids_across_every_package()
    {
        var path = Write("osv.json", """
            {
              "results": [
                {
                  "packages": [
                    { "vulnerabilities": [{ "id": "GHSA-1" }, { "id": "GHSA-2" }] },
                    { "vulnerabilities": [{ "id": "GHSA-1" }] }
                  ]
                }
              ]
            }
            """);

        Assert.Equal(2, SecurityReportParsing.CountOsvFindings(path));
    }

    [Fact]
    public void CountOsvFindings_is_zero_for_an_empty_file()
    {
        var path = Write("osv.json", "  ");

        Assert.Equal(0, SecurityReportParsing.CountOsvFindings(path));
    }

    // ---------------------------------------------------------------------- Analyze diagnostics

    private const string SampleBuildOutput = """
        /workspace/src/Foo.cs(10,5): warning CA1859: prefer a concrete type [Foo.csproj]
        /workspace/src/Foo.cs(20,5): error CA5351: do not use broken crypto [Foo.csproj]
        /workspace/src/Bar.cs(5,1): error S138: this method has too many lines [Bar.csproj]
        /workspace/src/Bar.cs(6,1): error CS1591: missing XML comment [Bar.csproj]
        """;

    [Fact]
    public void ParseDiagnostics_extracts_severity_and_uppercased_rule_id_from_every_line()
    {
        var diagnostics = AnalyzeDiagnosticsSupport.ParseDiagnostics(SampleBuildOutput);

        Assert.Equal(4, diagnostics.Count);
        Assert.Contains(("warning", "CA1859"), diagnostics);
        Assert.Contains(("error", "CA5351"), diagnostics);
    }

    [Fact]
    public void CountSecurityFindings_matches_only_CA5xxx()
    {
        var diagnostics = AnalyzeDiagnosticsSupport.ParseDiagnostics(SampleBuildOutput);

        Assert.Equal(1, AnalyzeDiagnosticsSupport.CountSecurityFindings(diagnostics));
    }

    [Fact]
    public void CountComplexityFindings_matches_only_S138()
    {
        var diagnostics = AnalyzeDiagnosticsSupport.ParseDiagnostics(SampleBuildOutput);

        Assert.Equal(1, AnalyzeDiagnosticsSupport.CountComplexityFindings(diagnostics));
    }

    [Fact]
    public void CountLintFindings_is_everything_that_is_neither_S138_nor_CA5xxx()
    {
        var diagnostics = AnalyzeDiagnosticsSupport.ParseDiagnostics(SampleBuildOutput);

        // CA1859 (warning) and CS1591 (error) — CA5351 and S138 have their own rows.
        Assert.Equal(2, AnalyzeDiagnosticsSupport.CountLintFindings(diagnostics));
    }

    [Fact]
    public void HasLintErrors_is_true_only_for_an_error_severity_non_complexity_non_security_diagnostic()
    {
        var diagnostics = AnalyzeDiagnosticsSupport.ParseDiagnostics(SampleBuildOutput);

        Assert.True(AnalyzeDiagnosticsSupport.HasLintErrors(diagnostics)); // CS1591 is error-severity lint

        var onlyWarnings = AnalyzeDiagnosticsSupport.ParseDiagnostics(
            "/f.cs(1,1): warning CA1859: x [f.csproj]");
        Assert.False(AnalyzeDiagnosticsSupport.HasLintErrors(onlyWarnings));

        var onlySecurityAndComplexity = AnalyzeDiagnosticsSupport.ParseDiagnostics(
            "/f.cs(1,1): error CA5351: x [f.csproj]\n/f.cs(2,1): error S138: y [f.csproj]");
        Assert.False(AnalyzeDiagnosticsSupport.HasLintErrors(onlySecurityAndComplexity));
    }
}
