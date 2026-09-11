// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace NarrativeTrace.Build;

/// <summary>One category's own subprocess invocation: whether it succeeded, everything it printed,
/// how long it took, and where the full output was saved.</summary>
public sealed record CommandOutcome(int ExitCode, string Output, double Seconds, string LogFile);

/// <summary>
/// Runs one <c>VerifyAll</c> category as a fresh <c>./build.sh &lt;Target&gt;</c> subprocess and
/// captures its outcome — mirroring Java's buildSrc <c>runGradleSubprocess</c> (a fresh, non-daemon
/// <c>./gradlew</c> per category, never run concurrently: this repo's own dev container has choked
/// on concurrent heavy invocations before). A category's own NUKE target already encodes its real
/// gate (exit code); this only isolates that gate's failure from every other category's turn and
/// preserves the full console output for the category's own log file, since <c>VerifyAll</c>'s own
/// console only shows the outer process.
/// </summary>
public static class VerifyAllExec
{
    /// <summary>
    /// Runs <c>./build.sh</c> (or <c>build.cmd</c> on Windows) with <paramref name="args"/> from
    /// <paramref name="rootDir"/>, combining stdout and stderr (matching Java's
    /// <c>redirectErrorStream(true)</c> — a category's failure reasoning is usually split across
    /// both). Never throws on a non-zero exit: that is exactly the signal the caller turns into a
    /// <c>failed</c> row rather than an aborted run.
    /// </summary>
    public static CommandOutcome Run(string rootDir, string category, string logDir, params string[] args)
    {
        DeleteStaleNukeLogLock(rootDir);

        var isWindows = OperatingSystem.IsWindows();
        var fileName = isWindows ? "build.cmd" : "build.sh";
        var startInfo = new ProcessStartInfo
        {
            FileName = isWindows ? fileName : Path.Combine(rootDir, fileName),
            WorkingDirectory = rootDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        var output = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();
        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) output.AppendLine(e.Data); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();
        stopwatch.Stop();

        Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, $"{category}.log");
        File.WriteAllText(logFile, output.ToString());

        return new CommandOutcome(process.ExitCode, output.ToString(), stopwatch.Elapsed.TotalSeconds, logFile);
    }

    /// <summary>
    /// Unlinks <c>.nuke/temp/build.log</c> before starting a nested <c>./build.sh</c> — VerifyAll's
    /// own process holds that exact path open, exclusively, for its entire run (NUKE's engine opens
    /// it once at startup and never closes it until the process exits — confirmed directly against
    /// this repo's own dev container by polling <c>/proc/&lt;pid&gt;/fd</c> while a build ran), so
    /// every nested invocation's own attempt to open the SAME path fails immediately with "the
    /// process cannot access the file... because it is being used by another process", no matter
    /// how long it retries — the hold is not transient. Deleting the path first sidesteps this
    /// without touching the outer process's own handle at all: POSIX <c>unlink</c> only removes the
    /// directory entry, not the open file itself, so the outer process keeps writing to the same
    /// inode via its already-open handle exactly as before, while the nested process's own <c>open()</c>
    /// at that now-vacant path creates a brand-new, entirely unlocked inode. Verified directly: a
    /// nested build started this way while an outer one was independently confirmed still running
    /// (via <c>ps</c>) completed cleanly, and the outer build then finished cleanly too. Swallows a
    /// delete failure (e.g. a lost race with another nested invocation) — the worst case is falling
    /// back to the original "used by another process" failure this replaces, never a new one.
    /// </summary>
    private static void DeleteStaleNukeLogLock(string rootDir)
    {
        try
        {
            File.Delete(Path.Combine(rootDir, ".nuke", "temp", "build.log"));
        }
        catch
        {
            // Best-effort — see remarks above.
        }
    }

    /// <summary>The short commit <c>VerifyAll</c> ran at — <c>"unknown"</c> rather than failing the
    /// run over it, matching the schema's own explicit fallback.</summary>
    public static string ShortCommit(string rootDir)
    {
        try
        {
            var info = new ProcessStartInfo("git", "rev-parse --short HEAD")
            {
                WorkingDirectory = rootDir,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using var process = Process.Start(info)!;
            var text = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return string.IsNullOrWhiteSpace(text) ? "unknown" : text;
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary><c>&lt;hostname&gt;-&lt;arch&gt;</c> — enough to explain a timing anomaly, nothing sensitive.</summary>
    public static string HostDescriptor()
    {
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME")
            ?? Environment.GetEnvironmentVariable("COMPUTERNAME")
            ?? TryGetHostName();
        return $"{hostname}-{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}".ToLowerInvariant();
    }

    private static string TryGetHostName()
    {
        try { return System.Net.Dns.GetHostName(); }
        catch { return "unknown"; }
    }

    private static string Prefix(string? note) => note is null ? "" : $"{note}; ";

    /// <summary>Appends the saved log path to <paramref name="note"/> whenever <paramref name="status"/>
    /// is not a clean pass — a passed row stays as clean as <paramref name="note"/> already was.</summary>
    public static string? WithLogHint(string? note, CommandOutcome outcome, string status)
    {
        if (status == "passed")
            return note;
        return Prefix(note) + $"full output: {outcome.LogFile}";
    }

    /// <summary>The composite-row overload of <see cref="WithLogHint(string?, CommandOutcome, string)"/> —
    /// a row fed by more than one real invocation (e.g. <c>sca</c>'s DependencyAudit + OsvScan +
    /// VulnerablePackages) points at every one of their log files, not just the first.</summary>
    public static string? WithLogHint(string? note, string status, params CommandOutcome[] outcomes)
    {
        if (status == "passed")
            return note;
        return Prefix(note) + $"full output: {string.Join(", ", outcomes.Select(o => o.LogFile))}";
    }
}
