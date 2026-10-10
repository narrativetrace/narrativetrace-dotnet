// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NarrativeTrace.Build;

/// <summary>
/// One vendor's own validator for one artifact this repository publishes, as DATA.
/// </summary>
/// <param name="Tool">The executable, looked up on <c>PATH</c> by this name.</param>
/// <param name="Probe">
/// Argv (after <see cref="Tool"/>) that proves the tool answers at all — a tool present but
/// unusable must skip like an absent one, never fail a gate about our own artifact.
/// </param>
/// <param name="Validate">
/// Argv (after <see cref="Tool"/>) that performs the validation; the staged artifact root is
/// appended as the last argument.
/// </param>
/// <param name="Artifact">Repo-relative path of what this row validates, for the log and the report.</param>
/// <param name="StagedPaths">
/// Repo-relative paths the validation needs staged — the artifact and whatever it points at (a
/// marketplace file is only valid together with the plugin directory its entry names), so the
/// tool sees the same tree a consumer's clone would.
/// </param>
/// <param name="InstallHint">One line telling a reader how to make <see cref="Tool"/> available.</param>
public sealed record VendorCheck(
    string Tool,
    IReadOnlyList<string> Probe,
    IReadOnlyList<string> Validate,
    string Artifact,
    IReadOnlyList<string> StagedPaths,
    string InstallHint);

/// <summary>What one row's run amounted to. A skip is never a pass: nothing was validated.</summary>
public enum VendorOutcome { Passed, Failed, Skipped }

/// <summary>One row's result: what ran, what it decided, and everything it printed.</summary>
public sealed record VendorCheckResult(
    string Tool, string Artifact, VendorOutcome Outcome, string Message, string Output);

/// <summary>
/// Backs the root <c>VendorValidate</c> target — the seam where a VENDOR's own validator checks
/// an artifact this repository publishes for that vendor to read.
/// </summary>
/// <remarks>
/// A registry, not one hard-coded invocation: the family publishes registry artifacts for
/// several agent tools, and each new one joins as a ROW (tool, probe, validate, artifact)
/// rather than as new machinery. The task stays in the heavy verification tier and never rides
/// <c>Verify</c>: a per-commit gate may not depend on a third-party CLI being installed.
///
/// The precondition lives HERE, in the row's own run, never in a CI-side exclusion list: an
/// absent tool SKIPS with one line naming it and how to install it, and a skip is recorded as a
/// skip.
/// </remarks>
public static class VendorValidationSupport
{
    /// <summary>
    /// Every vendor validation this repository knows how to run. One row today: the plugin
    /// marketplace file, validated by the agent CLI that reads it.
    /// </summary>
    public static readonly IReadOnlyList<VendorCheck> Checks =
    [
        new VendorCheck(
            Tool: "claude",
            Probe: ["--version"],
            Validate: ["plugin", "validate"],
            Artifact: ".claude-plugin/marketplace.json",
            StagedPaths: [".claude-plugin", ".claude/skills"],
            InstallHint: "install the agent CLI (npm i -g @anthropic-ai/claude-code) and re-run"),
    ];

    /// <summary>
    /// Runs one row against <paramref name="artifactRoot"/> — the staged tree holding the
    /// artifact — resolving the tool on <paramref name="pathValue"/> (the process environment's
    /// own <c>PATH</c> when null).
    /// </summary>
    /// <remarks>
    /// Three outcomes, in this order: the tool is absent or does not answer its probe → SKIPPED
    /// with a one-line reason naming it and <see cref="VendorCheck.InstallHint"/>; the validator
    /// exits zero → PASSED; anything else → FAILED, carrying everything the validator printed.
    /// </remarks>
    public static VendorCheckResult Validate(VendorCheck check, string artifactRoot, string? pathValue = null)
    {
        var executable = ExecutableOnPath(check.Tool, pathValue);
        if (executable is null)
            return Skipped(check, "not found on PATH");

        var probeExit = Execute(executable, check.Probe, artifactRoot).ExitCode;
        if (probeExit != 0)
            return Skipped(check, $"found at {executable} but its own probe failed (exit {probeExit})");

        var (exitCode, output) = Execute(executable, [.. check.Validate, artifactRoot], artifactRoot);
        return exitCode == 0 ? Passed(check, output) : Failed(check, exitCode, output);
    }

    /// <summary>
    /// The whole run's verdict: FAILED when any row failed, PASSED when at least one row
    /// genuinely ran clean, SKIPPED otherwise — including when there are no rows at all. A run
    /// where every vendor CLI was absent validated nothing, and must never read as a pass.
    /// </summary>
    public static VendorOutcome Aggregate(IReadOnlyList<VendorCheckResult> results)
    {
        if (results.Any(r => r.Outcome == VendorOutcome.Failed))
            return VendorOutcome.Failed;
        if (results.Any(r => r.Outcome == VendorOutcome.Passed))
            return VendorOutcome.Passed;
        return VendorOutcome.Skipped;
    }

    /// <summary>
    /// Records one row's outcome under <paramref name="reportsDir"/>, so "validated" and "never
    /// validated" stay distinguishable after the fact — by a person and by <c>VerifyAll</c>,
    /// which reads these back rather than inferring a status from an exit code that a skip also
    /// leaves at zero.
    /// </summary>
    public static void Record(string reportsDir, VendorCheckResult result)
    {
        Directory.CreateDirectory(reportsDir);
        File.WriteAllText(
            StatusFile(reportsDir, result.Tool),
            $"{result.Outcome.ToString().ToLowerInvariant()}: {result.Message}\n");
    }

    /// <summary>What <paramref name="tool"/>'s last recorded run said, or <c>"never-ran"</c> when it has no record at all.</summary>
    public static string RecordedStatus(string reportsDir, string tool)
    {
        var file = StatusFile(reportsDir, tool);
        return File.Exists(file) ? File.ReadAllText(file).Trim() : "never-ran";
    }

    private static string StatusFile(string reportsDir, string tool) =>
        Path.Combine(reportsDir, $"{tool}.status");

    /// <summary>
    /// Stages <paramref name="paths"/> out of <c>HEAD</c> into <paramref name="into"/>: <c>git
    /// archive</c>, never the working tree, so what a vendor validates is what a publish would
    /// ship — uncommitted edits are invisible here exactly as they are to the publish script's
    /// own staging.
    /// </summary>
    /// <exception cref="InvalidOperationException">git or tar could not produce the tree.</exception>
    public static void StageFromHead(string repoRoot, IReadOnlyList<string> paths, string into)
    {
        Directory.CreateDirectory(into);
        var archive = Path.Combine(into, "head.tar");
        RunOrThrow(repoRoot, "git", ["archive", "--format=tar", "-o", archive, "HEAD", "--", .. paths]);
        RunOrThrow(into, "tar", ["-xf", archive, "-C", into]);
        File.Delete(archive);
    }

    private static void RunOrThrow(string workingDirectory, string fileName, IReadOnlyList<string> arguments)
    {
        var (exitCode, output) = Execute(fileName, arguments, workingDirectory);
        if (exitCode != 0)
            throw new InvalidOperationException(
                $"{fileName} {string.Join(' ', arguments)} failed (exit {exitCode}): {output}");
    }

    /// <summary>The first executable named <paramref name="name"/> on <paramref name="pathValue"/>, or null when nothing there can run.</summary>
    public static string? ExecutableOnPath(string name, string? pathValue)
    {
        var path = pathValue ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator)
            .Where(dir => !string.IsNullOrWhiteSpace(dir))
            .Select(dir => Path.Combine(dir, name))
            .FirstOrDefault(File.Exists);
    }

    private static VendorCheckResult Passed(VendorCheck check, string output) =>
        new(check.Tool, check.Artifact, VendorOutcome.Passed, $"validated {check.Artifact}", output);

    private static VendorCheckResult Failed(VendorCheck check, int exitCode, string output) =>
        new(check.Tool, check.Artifact, VendorOutcome.Failed,
            $"{check.Tool} rejected {check.Artifact} (exit {exitCode})", output);

    private static VendorCheckResult Skipped(VendorCheck check, string reason) =>
        new(check.Tool, check.Artifact, VendorOutcome.Skipped,
            $"{check.Tool} {reason} — {check.Artifact} was NOT validated; {check.InstallHint}", "");

    private static (int ExitCode, string Output) Execute(
        string executable, IReadOnlyList<string> arguments, string workingDirectory)
    {
        var info = new ProcessStartInfo(executable)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)
            ?? throw new InvalidOperationException($"could not start {executable}");
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return (process.ExitCode, output);
    }
}
