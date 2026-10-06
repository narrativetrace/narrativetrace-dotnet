// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Drives Build → the <c>_NarrativeTraceRefreshSkills</c> target through real MSBuild (design D8).
/// Like <see cref="MSBuildClarityGateTests"/>, it substitutes <c>echo</c> for the global tool via the
/// private <c>_NarrativeTraceCli</c> property, so the assertions are on what this repo owns — whether
/// the refresh joins the build, with which arguments, and how it is gated — not on the CLI's behavior.
/// </summary>
public class MSBuildRefreshTargetTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Refresh_runs_on_build_when_the_sentinel_page_is_present()
    {
        var result = BuildProbe(withSentinel: true);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("init --refresh", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_passes_the_directory_holding_the_sentinel_as_dir()
    {
        var result = BuildProbe(withSentinel: true);

        Assert.Contains("--dir", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_is_skipped_when_no_install_is_present()
    {
        var result = BuildProbe(withSentinel: false);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("init --refresh", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Refresh_can_be_opted_out_despite_an_install()
    {
        var result = BuildProbe(withSentinel: true, "-p:NarrativeTraceRefreshSkills=false");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("init --refresh", result.Output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output) BuildProbe(bool withSentinel, params string[] properties)
    {
        var dir = Path.Combine(Path.GetTempPath(), "nt-refresh-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            if (withSentinel)
            {
                var sentinelDir = Path.Combine(dir, ".agents", "skills", "narrativetrace-doctor");
                Directory.CreateDirectory(sentinelDir);
                File.WriteAllText(Path.Combine(sentinelDir, "SKILL.md"), "---\nname: narrativetrace-doctor\n---\n");
            }

            File.WriteAllText(Path.Combine(dir, "probe.csproj"), ProjectXml());
            return RunMsBuild(Path.Combine(dir, "probe.csproj"), properties);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static string ProjectXml() =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <Import Project="{BuildTransitive("NarrativeTrace.MSBuild.props")}" />
          <Import Project="{BuildTransitive("NarrativeTrace.MSBuild.targets")}" />
        </Project>
        """;

    private static string BuildTransitive(string file) =>
        Path.Combine(RepoRoot, "src", "NarrativeTrace.MSBuild", "buildTransitive", file);

    private static (int ExitCode, string Output) RunMsBuild(string project, string[] properties)
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
        process.OutputDataReceived += (_, e) => Append(output, e.Data);
        process.ErrorDataReceived += (_, e) => Append(output, e.Data);
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

    private static void Append(StringBuilder output, string? data)
    {
        if (data != null)
        {
            output.AppendLine(data);
        }
    }
}
