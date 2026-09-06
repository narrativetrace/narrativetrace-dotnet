// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Verifies the shipped MSBuild shim fails the build early on an invalid
/// <c>NarrativeTraceLevel</c>/<c>NarrativeTraceFormat</c> instead of forwarding a
/// typo to the test host, mirroring the Gradle plugin's validation (CLARITY-12).
/// Each case runs the <c>_NarrativeTraceValidateConfig</c> target in isolation
/// against a throwaway project that imports the real props/targets.
/// </summary>
public class MSBuildConfigValidationTests
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static string BuildTransitive(string file) => Path.Combine(
        RepoRoot, "src", "NarrativeTrace.MSBuild", "buildTransitive", file);

    [Theory]
    [InlineData("DETAIL", "markdown")]
    [InlineData("detail", "mermaid")]
    [InlineData("OFF", "plantuml")]
    [InlineData("ERRORS", "text")]
    public void Valid_level_and_format_pass(string level, string format)
    {
        var result = RunValidation(level, format);

        Assert.True(result.ExitCode == 0, result.Output);
    }

    [Fact]
    public void Invalid_level_fails_the_build()
    {
        var result = RunValidation("BOGUS", "markdown");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid NarrativeTraceLevel", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_format_fails_the_build()
    {
        var result = RunValidation("DETAIL", "yaml");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid NarrativeTraceFormat", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_clarity_source_fails_the_build()
    {
        var result = RunValidation("DETAIL", "markdown", claritySource: "guess");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("invalid NarrativeTraceClaritySource", result.Output, StringComparison.Ordinal);
    }

    private static (int ExitCode, string Output) RunValidation(
        string level, string format, string claritySource = "scan")
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-msbuild-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var project = Path.Combine(dir, "probe.csproj");
            File.WriteAllText(project, ProjectXml());
            return RunMsBuild(project, level, format, claritySource);
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

    private static (int ExitCode, string Output) RunMsBuild(
        string project, string level, string format, string claritySource)
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
        psi.ArgumentList.Add("-t:_NarrativeTraceValidateConfig");
        psi.ArgumentList.Add($"-p:NarrativeTraceLevel={level}");
        psi.ArgumentList.Add($"-p:NarrativeTraceFormat={format}");
        psi.ArgumentList.Add($"-p:NarrativeTraceClaritySource={claritySource}");
        psi.ArgumentList.Add("-nologo");
        return Capture(psi);
    }

    private static (int ExitCode, string Output) Capture(ProcessStartInfo psi)
    {
        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start dotnet msbuild");
        var output = new StringBuilder();
        output.Append(process.StandardOutput.ReadToEnd());
        output.Append(process.StandardError.ReadToEnd());
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(3).TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException("dotnet msbuild timed out.");
        }

        return (process.ExitCode, output.ToString());
    }
}
