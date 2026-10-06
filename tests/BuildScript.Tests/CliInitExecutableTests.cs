// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Proves the published <c>dotnet-narrativetrace</c> tool runs <c>init</c> from the carrier it
/// bundles, by installing the packed tool into a temp project and executing it — the .NET analogue of
/// Java's <c>CliExecutableJarTest</c>. Reading the embedded resources back with reflection (as
/// <c>CliEmbeddedCarrierTests</c> does) would not catch a carrier that is right in the build tree and
/// wrong in the packed tool, or a locator that looks for the resources where the running tool cannot
/// see them; only installing and running the tool does.
/// </summary>
/// <remarks>
/// Traited <c>SpawnsBuild</c> and run via <c>./build.sh BuildScriptTests</c> only — <c>dotnet pack</c>
/// plus a tool install is far too slow for a per-test constructor, so it happens ONCE per class run
/// via <see cref="CliInitExecutableFixture"/>.
/// </remarks>
[Trait("Category", "SpawnsBuild")]
public sealed class CliInitExecutableTests : IClassFixture<CliInitExecutableFixture>
{
    private readonly CliInitExecutableFixture _fixture;

    public CliInitExecutableTests(CliInitExecutableFixture fixture) => _fixture = fixture;

    [Fact]
    public void The_installed_tools_usage_lists_the_installer_verbs()
    {
        // `--help` is not tested through `dotnet tool run`: that driver owns --help/-h and prints
        // its own help before the tool sees them (unlike --dry-run/--json, which it forwards). An
        // unknown verb reaches the tool, which prints its usage listing every verb and exits 2.
        var run = _fixture.Run("no-such-verb");

        Assert.Equal(2, run.ExitCode);
        Assert.Contains("init", run.Output, StringComparison.Ordinal);
        Assert.Contains("uninstall", run.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void Init_dry_run_plans_an_install_from_the_bundled_carrier()
    {
        var project = _fixture.FreshProject();

        var run = _fixture.Run("init", "--dry-run", "--dir", project);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("narrativetrace — NarrativeTrace.Skills/", run.Output, StringComparison.Ordinal);
        Assert.Contains("+++ b/AGENTS.md", run.Output, StringComparison.Ordinal);
        Assert.Contains("+++ b/.agents/skills/narrativetrace-doctor/SKILL.md", run.Output, StringComparison.Ordinal);
        Assert.Contains("installed by narrativetrace init from", run.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(project, "AGENTS.md")));
        Assert.False(Directory.Exists(Path.Combine(project, ".agents")));
    }

    [Fact]
    public void Init_dry_run_json_prints_the_installer_envelope()
    {
        var project = _fixture.FreshProject();

        var run = _fixture.Run("init", "--dry-run", "--json", "--dir", project);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("\"carrier\"", run.Output, StringComparison.Ordinal);
        Assert.Contains("\"actions\"", run.Output, StringComparison.Ordinal);
        Assert.Contains("\"exitCode\": 0", run.Output, StringComparison.Ordinal);
    }
}

/// <summary>Packs <c>NarrativeTrace.Cli</c> once and installs it as a local tool in a temp manifest dir.</summary>
public sealed class CliInitExecutableFixture : IDisposable
{
    private static readonly string RepoRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private readonly string _work = Path.Combine(
        Path.GetTempPath(), "nt-cli-init-" + Guid.NewGuid().ToString("N"));

    private readonly string _toolHost;

    public CliInitExecutableFixture()
    {
        Directory.CreateDirectory(_work);
        var feed = Path.Combine(_work, "feed");
        Directory.CreateDirectory(feed);
        // A unique package version so `dotnet tool install` cannot resolve a cached NarrativeTrace.Cli
        // of the same version from the global packages folder instead of this freshly-packed one.
        var version = "0.0.0-m3test" + Guid.NewGuid().ToString("N")[..8];
        Pack(feed, version);
        _toolHost = Path.Combine(_work, "toolhost");
        Directory.CreateDirectory(_toolHost);
        Dotnet(_toolHost, "new", "tool-manifest");
        Dotnet(_toolHost, "tool", "install", "NarrativeTrace.Cli", "--version", version, "--add-source", feed);
    }

    /// <summary>A fresh empty project directory the dry run plans against.</summary>
    public string FreshProject()
    {
        var project = Path.Combine(_work, "project-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(project);
        return project;
    }

    /// <summary>Runs the installed tool with the given arguments from the manifest directory.</summary>
    public (int ExitCode, string Output) Run(params string[] toolArgs)
    {
        string[] args = ["tool", "run", "dotnet-narrativetrace", .. toolArgs];
        return Execute(_toolHost, args);
    }

    public void Dispose()
    {
        if (Directory.Exists(_work))
        {
            Directory.Delete(_work, recursive: true);
        }
    }

    private void Pack(string feed, string version)
    {
        var run = Execute(RepoRoot,
            ["pack", Path.Combine(RepoRoot, "src", "NarrativeTrace.Cli", "NarrativeTrace.Cli.csproj"),
             "-c", "Release", "-o", feed, "--nologo", "-p:PackageVersion=" + version]);
        Assert.True(run.ExitCode == 0, $"dotnet pack exited {run.ExitCode}:\n{run.Output}");
    }

    private static void Dotnet(string workingDir, params string[] args)
    {
        var run = Execute(workingDir, args);
        Assert.True(run.ExitCode == 0, $"dotnet {string.Join(' ', args)} exited {run.ExitCode}:\n{run.Output}");
    }

    private static (int ExitCode, string Output) Execute(string workingDir, string[] args)
    {
        var psi = new ProcessStartInfo { FileName = "dotnet", WorkingDirectory = workingDir, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("failed to start dotnet");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(5).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"dotnet {string.Join(' ', args)} timed out");
        }

        return (process.ExitCode, output);
    }
}
