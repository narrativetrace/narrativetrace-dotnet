// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// Tier B trial runner entry point (D13 as ruled, .NET milestone 0). NEVER invoked by
/// <c>./build.sh Verify</c> — Tier A (SkillsLint) and Tier A2 (SkillsReplay) ride every commit;
/// this drives the subscription CLIs, by hand or from a scheduled job, never the metered API.
/// Scaffolds each case's fixture into a scratch temp directory OUTSIDE the repo, drives the agent
/// through argv (no shell), runs the case's grader, and appends one row to
/// <c>skills-evals/ledger/runs.jsonl</c>.
///
/// <para>This class is deliberately thin — argument parsing, the trial loop, temp-directory
/// lifecycle and progress lines. Every decision worth a unit test lives in an injectable class it
/// only calls (<see cref="RepoRoot"/>, <see cref="AgentArgv"/>, <see cref="EvalTrial"/>,
/// <see cref="SporadicPolicy"/>, <see cref="CaseFixture"/>, <see cref="QuotaMarkdown"/>,
/// <see cref="TierPrecondition"/>, <see cref="Platforms"/>, <see cref="RunLedgerRow"/>), so it
/// carries no test of its own — the same reason the reference runners' entry points do not.</para>
/// </summary>
public static class EvalRunner
{
    /// <summary>Parses <paramref name="argv"/>, runs the requested trials, and returns an exit code.</summary>
    public static int Run(string[] argv, TextWriter output, TextWriter error)
    {
        EvalRunnerArgs args;
        try
        {
            args = EvalRunnerArgs.Parse(argv);
        }
        catch (ArgumentException e)
        {
            error.WriteLine(e.Message);
            return 2;
        }

        return RunTrials(args, output, error);
    }

    /// <summary>Repo-relative paths a run resolves once and threads through the trial loop.</summary>
    private sealed record Paths(string RepoRoot, string RunsPath, string QuotaPath, string CaseDir);

    private static int RunTrials(EvalRunnerArgs args, TextWriter output, TextWriter error)
    {
        var repoRoot = RepoRoot.Locate();
        var ledgerDir = Path.Combine(repoRoot, "skills-evals", "ledger");
        var paths = new Paths(
            repoRoot,
            Path.Combine(ledgerDir, "runs.jsonl"),
            Path.Combine(ledgerDir, "quota.md"),
            Path.Combine(repoRoot, "skills-evals", args.Skill, args.CaseName));
        var promptPath = Path.Combine(paths.CaseDir, "prompt.md");
        if (!File.Exists(promptPath))
        {
            error.WriteLine($"No case found at {paths.CaseDir}");
            return 2;
        }

        return DriveTrials(args, paths, File.ReadAllText(promptPath), output);
    }

    private static int DriveTrials(EvalRunnerArgs args, Paths paths, string prompt, TextWriter output)
    {
        var fixtureDir = Path.Combine(paths.RepoRoot, CaseFixture.FixtureFor(paths.CaseDir));
        var agentCommand = args.AgentCommand ?? args.Platform.PresetAgentCommand(args.Model, args.Skill);
        LogWhenNoAgentCommand(agentCommand, output);

        var trial = new EvalTrial(EvalTrial.RealProcess, () => DateTimeOffset.UtcNow, paths.RunsPath);
        var sessionSpend = new List<QuotaSpendRow>();
        for (var t = 1; t <= args.Trials; t++)
        {
            RunOneTrial(trial, args, paths, fixtureDir, prompt, agentCommand, t, sessionSpend, output);
        }

        return 0;
    }

    private static void RunOneTrial(
        EvalTrial trial, EvalRunnerArgs args, Paths paths, string fixtureDir, string prompt,
        string? agentCommand, int t, List<QuotaSpendRow> sessionSpend, TextWriter output)
    {
        if (args.Platform.IsSporadic())
        {
            SporadicPolicy.BeforeTrial(
                args.Platform, paths.RepoRoot, TierPrecondition.RunViaProcess,
                MergedQuotaLedger(paths.QuotaPath, sessionSpend), DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var scratch = Directory.CreateTempSubdirectory("nt-eval-").FullName;
        var pass = RunInScratch(trial, args, paths, fixtureDir, prompt, agentCommand, t, scratch);
        output.WriteLine($"trial {t}/{args.Trials}: {(pass ? "pass" : "fail")}");
        if (args.Platform.IsSporadic())
        {
            sessionSpend.Add(RecordSporadicSpend(paths.QuotaPath, args));
        }
    }

    private static bool RunInScratch(
        EvalTrial trial, EvalRunnerArgs args, Paths paths, string fixtureDir, string prompt,
        string? agentCommand, int t, string scratch)
    {
        try
        {
            return trial.Run(args, paths.RepoRoot, fixtureDir, paths.CaseDir, scratch, prompt, agentCommand, t);
        }
        finally
        {
            TryDelete(scratch);
        }
    }

    private static void LogWhenNoAgentCommand(string? agentCommand, TextWriter output)
    {
        if (agentCommand is null)
        {
            output.WriteLine("(no agent command — skipping the agent step, grading the fixture as-is)");
        }
    }

    private static QuotaLedger MergedQuotaLedger(string quotaPath, List<QuotaSpendRow> sessionSpend)
    {
        var onDisk = QuotaMarkdown.Parse(File.ReadAllText(quotaPath));
        return new QuotaLedger(onDisk.Allowances, [.. onDisk.Spend, .. sessionSpend]);
    }

    private static QuotaSpendRow RecordSporadicSpend(string quotaPath, EvalRunnerArgs args)
    {
        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var row = new QuotaSpendRow(
            now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
            args.Platform, args.Skill, args.CaseName, QuotaMarkdown.IsoWeek(now));
        File.AppendAllText(quotaPath, QuotaMarkdown.AppendedSpendRowLine(row));
        return row;
    }

    private static void TryDelete(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup of a scratch temp directory — never fails the trial over it.
        }
    }
}
