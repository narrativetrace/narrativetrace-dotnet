// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the missing-binary honesty gate behind <c>SecretsScan</c>/<c>Semgrep</c>/<c>OsvScan</c> —
/// see <see cref="ScannerGateSupport"/>.
/// </summary>
public sealed class ScannerGateSupportTests : IDisposable
{
    private readonly string _reportsDir = Directory.CreateTempSubdirectory("nt-scanner-gate").FullName;

    public void Dispose() => Directory.Delete(_reportsDir, recursive: true);

    // ---------------------------------------------------------------- missing-binary decision

    [Fact]
    public void A_missing_binary_fails_outright_when_scanners_are_required()
    {
        var decision = ScannerGateSupport.OnMissingBinary("gitleaks", required: true, installHint: "https://example/install");

        Assert.True(decision.Fail);
        Assert.Contains("gitleaks", decision.Message, StringComparison.Ordinal);
        Assert.Contains("required", decision.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_binary_warns_loudly_when_scanners_are_not_required()
    {
        var decision = ScannerGateSupport.OnMissingBinary("semgrep", required: false, installHint: "https://example/install");

        Assert.False(decision.Fail);
        Assert.Contains("SKIPPED", decision.Message, StringComparison.Ordinal);
        Assert.Contains("NOT a clean scan", decision.Message, StringComparison.Ordinal);
        Assert.Contains("https://example/install", decision.Message, StringComparison.Ordinal);
    }

    // ------------------------------------------------------- ran-clean vs skipped vs never-ran

    [Fact]
    public void A_scan_that_never_ran_reports_never_ran_not_a_clean_pass()
    {
        Assert.Equal("never-ran", ScannerGateSupport.Status(_reportsDir, "gitleaks"));
    }

    [Fact]
    public void A_recorded_skip_is_distinguishable_from_a_clean_run()
    {
        ScannerGateSupport.RecordSkipped(_reportsDir, "gitleaks", "binary not on PATH");

        var status = ScannerGateSupport.Status(_reportsDir, "gitleaks");
        Assert.StartsWith("skipped", status, StringComparison.Ordinal);
        Assert.Contains("binary not on PATH", status, StringComparison.Ordinal);
    }

    [Fact]
    public void A_clean_run_is_recorded_as_ran_clean()
    {
        ScannerGateSupport.RecordRanClean(_reportsDir, "gitleaks");

        Assert.Equal("ran-clean", ScannerGateSupport.Status(_reportsDir, "gitleaks"));
    }

    [Fact]
    public void A_later_clean_run_replaces_an_earlier_skip()
    {
        ScannerGateSupport.RecordSkipped(_reportsDir, "osv-scanner", "binary not on PATH");
        ScannerGateSupport.RecordRanClean(_reportsDir, "osv-scanner");

        Assert.Equal("ran-clean", ScannerGateSupport.Status(_reportsDir, "osv-scanner"));
    }

    [Fact]
    public void Each_tool_has_its_own_status()
    {
        ScannerGateSupport.RecordRanClean(_reportsDir, "gitleaks");
        ScannerGateSupport.RecordSkipped(_reportsDir, "semgrep", "binary not on PATH");

        Assert.Equal("ran-clean", ScannerGateSupport.Status(_reportsDir, "gitleaks"));
        Assert.StartsWith("skipped", ScannerGateSupport.Status(_reportsDir, "semgrep"), StringComparison.Ordinal);
    }
}
