// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Drives Build → ClarityCheck through real MSBuild, which nothing did before:
/// the gate's wiring was only ever exercised by invoking the target by hand.
/// </summary>
/// <remarks>
/// The CLI is a separately installed global tool, so these tests substitute
/// <c>echo</c> for it via the private <c>_NarrativeTraceCli</c> property. That
/// keeps the assertions on what this repo owns — whether the gate joins the
/// build and with which arguments — rather than on the tool's behavior, which
/// the CLI's own tests cover.
/// </remarks>
public class MSBuildClarityGateTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Configured_threshold_makes_the_gate_run_during_build()
    {
        var result = BuildProbe("-p:ClarityMinScore=0.5");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("clarity-check", result.Output, StringComparison.Ordinal);
        Assert.Contains("--min-score 0.5", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_max_high_issues_limit_also_arms_the_gate()
    {
        var result = BuildProbe("-p:ClarityMaxHighIssues=0");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("--max-high-issues 0", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Default_thresholds_leave_the_build_untouched()
    {
        var result = BuildProbe();

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("clarity-check", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_gate_can_be_forced_off_despite_a_threshold()
    {
        var result = BuildProbe(
            "-p:ClarityMinScore=0.5", "-p:NarrativeTraceClarityGate=false");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("clarity-check", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_gate_can_be_forced_on_without_a_threshold()
    {
        var result = BuildProbe("-p:NarrativeTraceClarityGate=true");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("clarity-check", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_invalid_gate_value_fails_the_build()
    {
        var result = BuildProbe("-p:NarrativeTraceClarityGate=yes");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(
            "invalid NarrativeTraceClarityGate", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_scan_source_gates_on_the_scan_specific_results_file()
    {
        var result = BuildProbe("-p:NarrativeTraceClarityGate=true");

        // clarity-scan and the test run write different files on purpose, so
        // the gate has to read the one its selected producer actually wrote.
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("clarity-scan-results.json", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_runtime_source_gates_on_the_aggregated_results_file()
    {
        var result = BuildProbe(
            "-p:NarrativeTraceClarityGate=true", "-p:NarrativeTraceClaritySource=runtime");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("clarity-aggregate", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("clarity-scan-results.json", result.Output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output) BuildProbe(params string[] properties)
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var project = Path.Combine(dir, "probe.csproj");
            File.WriteAllText(project, ProjectXml());
            return RunMsBuild(project, properties);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string ProjectXml()
    {
        return $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <Import Project="{BuildTransitive("NarrativeTrace.MSBuild.props")}" />
              <Import Project="{BuildTransitive("NarrativeTrace.MSBuild.targets")}" />
            </Project>
            """;
    }

    private static string BuildTransitive(string file)
    {
        return Path.Combine(
            RepoRoot, "src", "NarrativeTrace.MSBuild", "buildTransitive", file);
    }

    private static (int ExitCode, string Output) RunMsBuild(
        string project, string[] properties)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("msbuild");
        psi.ArgumentList.Add(project);
        psi.ArgumentList.Add("-restore");
        psi.ArgumentList.Add("-t:Build");
        // Substitutes a harmless command for the global tool under test.
        psi.ArgumentList.Add("-p:_NarrativeTraceCli=echo");
        foreach (var property in properties)
        {
            psi.ArgumentList.Add(property);
        }

        psi.ArgumentList.Add("-nologo");
        return Capture(psi);
    }

    private static (int ExitCode, string Output) Capture(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet msbuild");
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
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { /* already exited */ }
            throw new TimeoutException("dotnet msbuild timed out.");
        }

        return (process.ExitCode, output.ToString());
    }
}
