// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The Tier B trial flow: scaffold the case fixture into a scratch directory, drive the agent
/// command against the prompt, run the case's grader, and append one ledger row — factored out of
/// the entry point so it runs under a unit test with a fake process spawner, a fixed clock and a
/// temp ledger file, never a real subprocess or a real CLI.
///
/// <para>The agent command and the grader both run through the same <see cref="ProcessRunner"/>
/// seam as an argv list (built by <see cref="AgentArgv"/>) — never a shell string — so a prompt
/// containing shell metacharacters can never be reinterpreted.</para>
/// </summary>
public sealed class EvalTrial
{
    /// <summary>Spawns <paramref name="argv"/> in <paramref name="cwd"/> with extra env; returns the exit code.</summary>
    public delegate int ProcessRunner(IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env);

    private static readonly string[] ExcludedTopLevel = ["bin", "obj", ".git", ".vs", "TestResults"];

    private static readonly char[] PathSeparators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    private readonly ProcessRunner _run;
    private readonly Func<DateTimeOffset> _clock;
    private readonly string _runsPath;

    /// <summary>Wires the process seam, the clock and the ledger path this instance appends to.</summary>
    public EvalTrial(ProcessRunner run, Func<DateTimeOffset> clock, string runsPath)
    {
        _run = run ?? throw new ArgumentNullException(nameof(run));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _runsPath = runsPath ?? throw new ArgumentNullException(nameof(runsPath));
    }

    /// <summary>The production seam: a real OS process, argv passed directly — never through a shell.</summary>
    public static int RealProcess(IReadOnlyList<string> argv, string cwd, IReadOnlyDictionary<string, string> env)
    {
        var psi = new ProcessStartInfo
        {
            FileName = argv[0],
            WorkingDirectory = cwd,
            UseShellExecute = false,
        };
        foreach (var arg in argv.Skip(1))
        {
            psi.ArgumentList.Add(arg);
        }

        foreach (var (key, value) in env)
        {
            psi.Environment[key] = value;
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"failed to start: {string.Join(' ', argv)}");
        process.WaitForExit();
        return process.ExitCode;
    }

    /// <summary>
    /// Runs one trial into <paramref name="scratch"/> (created and deleted by the caller): copies
    /// the fixture in, drives <paramref name="agentCommand"/> (or skips the agent step when null),
    /// runs the case's grader, appends one ledger row, and returns whether the grader passed.
    /// </summary>
    /// <exception cref="InvalidOperationException">the agent command exited non-zero (a crash).</exception>
    public bool Run(
        EvalRunnerArgs args,
        string repoRoot,
        string fixtureDir,
        string caseDir,
        string scratch,
        string prompt,
        string? agentCommand,
        int trial)
    {
        Scaffold(repoRoot, fixtureDir, scratch);
        if (agentCommand is not null)
        {
            RunAgent(agentCommand, prompt, scratch);
        }

        var pass = RunGrader(caseDir, scratch) == 0;
        AppendLedgerRow(new RunLedgerRow(
            _clock().UtcDateTime.ToString("o"), args.Skill, args.CaseName, args.Platform, args.Model, trial, pass));
        return pass;
    }

    private static void Scaffold(string repoRoot, string fixtureDir, string scratch)
    {
        CopyRecursively(fixtureDir, scratch);
        foreach (var layout in new[] { ".agents", ".claude" })
        {
            var skills = Path.Combine(repoRoot, layout, "skills");
            if (Directory.Exists(skills))
            {
                CopyRecursively(skills, Path.Combine(scratch, layout, "skills"));
            }
        }
    }

    private void RunAgent(string agentCommand, string prompt, string scratch)
    {
        var argv = AgentArgv.Build(agentCommand, prompt);
        var exit = _run(argv, scratch, EmptyEnv);
        if (exit != 0)
        {
            throw new InvalidOperationException($"agent command exited {exit}: {string.Join(' ', argv)}");
        }
    }

    private int RunGrader(string caseDir, string scratch)
    {
        var grader = Path.GetFullPath(Path.Combine(caseDir, "graders", "verify.sh"));
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["PROJECT_DIR"] = scratch };
        return _run(["bash", grader], scratch, env);
    }

    private void AppendLedgerRow(RunLedgerRow row)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_runsPath)!);
        File.AppendAllText(_runsPath, row.ToJsonLine() + "\n");
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyEnv =
        new Dictionary<string, string>(StringComparer.Ordinal);

    private static void CopyRecursively(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (IsExcluded(relative))
            {
                continue;
            }

            var dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }
    }

    // A trial must earn its reports: a fixture's previously built output is not evidence of success.
    private static bool IsExcluded(string relative)
    {
        var segments = relative.Split(PathSeparators);
        return relative != "." && segments.Any(segment => ExcludedTopLevel.Contains(segment));
    }
}
