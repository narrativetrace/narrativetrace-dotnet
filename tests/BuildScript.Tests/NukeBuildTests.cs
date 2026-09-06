// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Build.Tests;

/// <summary>
/// End-to-end tests that shell out to <c>./build.sh</c>.
/// </summary>
/// <remarks>
/// Traited <c>SpawnsBuild</c> and filtered out of the <c>Test</c> and
/// <c>Coverage</c> targets: those targets run every <c>*.Tests</c> project, so
/// a test that re-invokes the build would recurse without end. Run them
/// deliberately with <c>./build.sh BuildScriptTests</c>.
/// </remarks>
[Trait("Category", "SpawnsBuild")]
public class NukeBuildTests
{
    private static readonly string RepoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Plan_ShowsNukeExecutionHeader()
    {
        var result = RunBuild("--plan", "--target", "Compile", "--target", "Analyze", "--target", "Test", "--target", "Coverage", "--target", "Mutation", "--target", "MetricsReport");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("NUKE Execution Engine", result.Output);
    }

    [Fact]
    public void Coverage_GeneratesSummaryAndCobertura()
    {
        var result = RunBuild("Coverage");

        Assert.Equal(0, result.ExitCode);
        var coverageRoot = Path.Combine(RepoRoot, "artifacts", "coverage");
        var summary = Path.Combine(coverageRoot, "coverage-summary.txt");

        Assert.True(File.Exists(summary), "Expected coverage-summary.txt to be generated.");
        Assert.True(Directory.EnumerateFiles(coverageRoot, "*.cobertura.xml", SearchOption.AllDirectories).Any(),
            "Expected at least one cobertura XML report.");
    }

    [Fact]
    public void MetricsReport_GeneratesReportFile()
    {
        var result = RunBuild("MetricsReport");

        Assert.Equal(0, result.ExitCode);
        var report = Path.Combine(RepoRoot, "artifacts", "metrics", "metrics-report.txt");
        Assert.True(File.Exists(report), "Expected metrics-report.txt to be generated.");

        var text = File.ReadAllText(report);
        Assert.Contains("SOURCE FILE METRICS", text);
        Assert.Contains("Total files:", text);
    }

    private static (int ExitCode, string Output) RunBuild(params string[] args)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var result = RunBuildOnce(args);
            if (result.ExitCode == 0)
                return result;

            if (!result.Output.Contains("build.log", StringComparison.OrdinalIgnoreCase))
                return result;

            Thread.Sleep(750 * attempt);
        }

        return RunBuildOnce(args);
    }

    private static (int ExitCode, string Output) RunBuildOnce(params string[] args)
    {
        var joined = string.Join(' ', args);
        var psi = new ProcessStartInfo
        {
            FileName = "bash",
            Arguments = $"-lc \"./build.sh {joined}\"",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start build process");
        var output = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) output.AppendLine(e.Data);
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit((int)TimeSpan.FromMinutes(10).TotalMilliseconds))
        {
            // Best effort: the process may have exited between the timeout and
            // the kill, and the timeout below is the outcome that matters.
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already exited */ }
            throw new TimeoutException("Build process timed out after 10 minutes.");
        }

        return (process.ExitCode, output.ToString());
    }
}
