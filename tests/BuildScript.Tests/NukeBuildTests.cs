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

        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.Output}");
        Assert.Contains("NUKE Execution Engine", result.Output);
    }

    [Fact]
    public void Coverage_GeneratesSummaryAndCobertura()
    {
        var result = RunBuild("Coverage");

        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.Output}");
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

        Assert.True(result.ExitCode == 0, $"exit {result.ExitCode}: {result.Output}");
        var report = Path.Combine(RepoRoot, "artifacts", "metrics", "metrics-report.txt");
        Assert.True(File.Exists(report), "Expected metrics-report.txt to be generated.");

        var text = File.ReadAllText(report);
        Assert.Contains("SOURCE FILE METRICS", text);
        Assert.Contains("Total files:", text);
    }

    /// <summary>
    /// NUKE's own engine opens <c>.nuke/temp/build.log</c> once per process and keeps it open,
    /// exclusively, for that whole process's lifetime — verified directly against this project's
    /// own dev container by polling <c>/proc/&lt;pid&gt;/fd</c> while a build ran: the SAME pid held
    /// it open, continuously, start to finish. Every method in this class is itself run BY a
    /// <c>./build.sh</c> invocation (whether that is a bare <c>./build.sh BuildScriptTests</c> or
    /// one nested further under <c>VerifyAll</c>) — so the <em>outer</em> process already holds
    /// that exact path for its own entire run before any <see cref="Fact"/> here starts its nested
    /// child, and without <see cref="RunBuildOnce"/>'s delete-first step below the child could never
    /// acquire it, no matter how long it retried (a bare retry-with-backoff was tried first and
    /// confirmed NOT to help, even at 8 attempts/~29s — the hold is not transient). Confirmed
    /// reproducing on an unmodified checkout: this is the environment (a container whose
    /// <c>/workspace</c> is a host bind mount), not a defect in any target's own behavior.
    /// </summary>
    private static (int ExitCode, string Output) RunBuild(params string[] args) => RunBuildOnce(args);

    /// <summary>
    /// Unlinks <c>.nuke/temp/build.log</c> before starting the nested build, for the exact reason
    /// <see cref="RunBuild"/>'s remarks describe. POSIX <c>unlink</c> only removes the directory
    /// entry, not the file an already-open handle still references, so the OUTER process (whichever
    /// <c>./build.sh</c> invocation is running this test) keeps writing to the same inode via its
    /// existing handle exactly as before, while THIS nested process's own <c>open()</c> at the now-
    /// vacant path creates a brand-new, entirely unlocked inode. Verified directly: a nested build
    /// started this way while an outer one was independently confirmed still running (via <c>ps</c>)
    /// completed cleanly, and the outer build then finished cleanly too.
    /// </summary>
    private static (int ExitCode, string Output) RunBuildOnce(params string[] args)
    {
        try
        {
            File.Delete(Path.Combine(RepoRoot, ".nuke", "temp", "build.log"));
        }
        catch
        {
            // Best-effort — worst case is falling back to the "used by another process" failure
            // this step exists to avoid, never a new one.
        }

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
