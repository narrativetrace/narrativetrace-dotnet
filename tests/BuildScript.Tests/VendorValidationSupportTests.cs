// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;

using Xunit;

namespace Build.Tests;

/// <summary>
/// The vendor-validation registry seam (D6, Phase 4 milestone 1): a vendor CLI's own validator
/// run against a registry artifact this repository publishes for it to read. Exercised against a
/// hand-written fake tool on an isolated <c>PATH</c> directory, mirroring the golden Java repo's
/// own <c>VendorValidationSupportTest</c>.
/// </summary>
public sealed class VendorValidationSupportTests : IDisposable
{
    private readonly string _pathDir = Path.Combine(
        Path.GetTempPath(), "nt-vendor-path-" + Guid.NewGuid().ToString("N"));

    private readonly string _artifactRoot = Path.Combine(
        Path.GetTempPath(), "nt-vendor-artifact-" + Guid.NewGuid().ToString("N"));

    public VendorValidationSupportTests()
    {
        Directory.CreateDirectory(_pathDir);
        Directory.CreateDirectory(_artifactRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_pathDir))
            Directory.Delete(_pathDir, recursive: true);
        if (Directory.Exists(_artifactRoot))
            Directory.Delete(_artifactRoot, recursive: true);
    }

    private static bool BashAvailable => !OperatingSystem.IsWindows();

    [Fact]
    public void Validate_skips_when_the_tool_is_not_on_path()
    {
        if (!BashAvailable) return;
        var check = Check();

        var result = VendorValidationSupport.Validate(check, _artifactRoot, _pathDir);

        Assert.Equal(VendorOutcome.Skipped, result.Outcome);
        Assert.Contains("not found on PATH", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_skips_when_the_tool_is_present_but_its_own_probe_fails()
    {
        if (!BashAvailable) return;
        WriteFakeTool(probeExit: 7, validateExit: 0);
        var check = Check();

        var result = VendorValidationSupport.Validate(check, _artifactRoot, _pathDir);

        Assert.Equal(VendorOutcome.Skipped, result.Outcome);
        Assert.Contains("probe failed", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_passes_when_the_validator_exits_zero()
    {
        if (!BashAvailable) return;
        WriteFakeTool(probeExit: 0, validateExit: 0);
        var check = Check();

        var result = VendorValidationSupport.Validate(check, _artifactRoot, _pathDir);

        Assert.Equal(VendorOutcome.Passed, result.Outcome);
    }

    [Fact]
    public void Validate_fails_when_the_validator_rejects_the_artifact()
    {
        if (!BashAvailable) return;
        WriteFakeTool(probeExit: 0, validateExit: 1);
        var check = Check();

        var result = VendorValidationSupport.Validate(check, _artifactRoot, _pathDir);

        Assert.Equal(VendorOutcome.Failed, result.Outcome);
        Assert.Contains("rejected", result.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new[] { VendorOutcome.Failed, VendorOutcome.Passed }, VendorOutcome.Failed)]
    [InlineData(new[] { VendorOutcome.Passed, VendorOutcome.Skipped }, VendorOutcome.Passed)]
    [InlineData(new[] { VendorOutcome.Skipped, VendorOutcome.Skipped }, VendorOutcome.Skipped)]
    [InlineData(new VendorOutcome[0], VendorOutcome.Skipped)]
    public void Aggregate_follows_failed_then_passed_then_skipped(VendorOutcome[] outcomes, VendorOutcome expected)
    {
        var results = outcomes.Select(o => new VendorCheckResult("tool", "artifact", o, "msg", "")).ToList();

        Assert.Equal(expected, VendorValidationSupport.Aggregate(results));
    }

    [Fact]
    public void Record_and_RecordedStatus_round_trip()
    {
        var reportsDir = Path.Combine(_artifactRoot, "reports");
        var result = new VendorCheckResult("claude", "artifact.json", VendorOutcome.Passed, "validated artifact.json", "");

        VendorValidationSupport.Record(reportsDir, result);

        Assert.Equal("passed: validated artifact.json", VendorValidationSupport.RecordedStatus(reportsDir, "claude"));
    }

    [Fact]
    public void RecordedStatus_is_never_ran_when_no_record_exists()
    {
        Assert.Equal("never-ran", VendorValidationSupport.RecordedStatus(_artifactRoot, "claude"));
    }

    [Fact]
    public void StageFromHead_only_stages_committed_content()
    {
        if (!BashAvailable) return;
        var repo = Path.Combine(_artifactRoot, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "keep"));
        File.WriteAllText(Path.Combine(repo, "keep", "a.txt"), "committed\n");
        Git(repo, "init", "-q", "-b", "main");
        Git(repo, "add", "-A");
        Git(repo, "-c", "user.email=fixture@example.invalid", "-c", "user.name=Fixture",
            "commit", "-q", "--no-verify", "-m", "fixture");

        File.WriteAllText(Path.Combine(repo, "keep", "a.txt"), "edited after commit\n");
        File.WriteAllText(Path.Combine(repo, "keep", "untracked.txt"), "never committed\n");

        var into = Path.Combine(_artifactRoot, "staged");
        VendorValidationSupport.StageFromHead(repo, ["keep"], into);

        Assert.Equal("committed\n", File.ReadAllText(Path.Combine(into, "keep", "a.txt")));
        Assert.False(File.Exists(Path.Combine(into, "keep", "untracked.txt")));
    }

    private static VendorCheck Check() => new(
        Tool: "faketool",
        Probe: ["--version"],
        Validate: ["plugin", "validate"],
        Artifact: "artifact.json",
        StagedPaths: ["artifact.json"],
        InstallHint: "install faketool and re-run");

    private void WriteFakeTool(int probeExit, int validateExit)
    {
        var path = Path.Combine(_pathDir, "faketool");
        File.WriteAllText(path,
            "#!/bin/sh\n" +
            "if [ \"$1\" = \"--version\" ]; then exit " + probeExit + "; fi\n" +
            "if [ \"$1\" = \"plugin\" ] && [ \"$2\" = \"validate\" ]; then echo validated; exit " + validateExit + "; fi\n" +
            "exit 1\n");
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
    }

    private static void Git(string workingDirectory, params string[] arguments)
    {
        var info = new System.Diagnostics.ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var arg in arguments)
            info.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {output}");
    }
}
