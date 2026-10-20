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
/// <see cref="TierPrecondition"/>, <see cref="Platforms"/>, <see cref="RunLedgerRow"/>,
/// <see cref="CaseTurns"/>, <see cref="AgentTurns"/>, <see cref="TrialEnvironment"/>), so it
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

    /// <summary>What one case is: its paths, its fixture, its first prompt, its scripted replies and its registry.</summary>
    private sealed record TrialPlan(
        EvalRunnerArgs Args, Paths Paths, string FixtureDir, string Prompt, IReadOnlyList<string> Replies,
        IReadOnlyList<int> FreshTurns, RegistryPreStep? PreStep);

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

        return TryReadPrompt(promptPath, error, out var prompt) ? DriveTrials(args, paths, prompt, output) : 2;
    }

    /// <summary>Reads what the agent is sent for the case; a case document without a task quote is reported, not run.</summary>
    private static bool TryReadPrompt(string promptPath, TextWriter error, out string prompt)
    {
        try
        {
            prompt = CasePrompt.TaskPrompt(promptPath, File.ReadAllText(promptPath));
            return true;
        }
        catch (InvalidOperationException e)
        {
            error.WriteLine(e.Message);
            prompt = "";
            return false;
        }
    }

    private static int DriveTrials(EvalRunnerArgs args, Paths paths, string prompt, TextWriter output)
    {
        var plan = new TrialPlan(
            args, paths, Path.Combine(paths.RepoRoot, CaseFixture.FixtureFor(paths.CaseDir)), prompt,
            CaseTurns.ScriptedRepliesFor(paths.CaseDir), CaseTurns.FreshTurnsFor(paths.CaseDir), CaseRegistry.RegistryFor(paths.CaseDir));
        LogTheConversationsShape(plan, output);
        var skip = SporadicPolicy.SkipReason(args.Platform, MergedQuotaLedger(paths.QuotaPath, []));
        if (skip is not null)
        {
            output.WriteLine(skip);
            return 0;
        }

        var sessionSpend = new List<QuotaSpendRow>();
        for (var t = 1; t <= args.Trials; t++)
        {
            RunOneTrial(plan, t, sessionSpend, output);
        }

        return 0;
    }

    /// <summary>
    /// What drives one trial of this case: the platform's templates, the case's turns, and a session
    /// id of this TRIAL's own — so three trials of a multi-turn case are three conversations rather
    /// than one long one.
    /// </summary>
    private static AgentTurns TurnsFor(TrialPlan plan) =>
        AgentTurns.Of(
            plan.Args.Platform, plan.Args.Model, plan.Args.Skill, plan.Args.AgentCommand, plan.Prompt, plan.Replies,
            Guid.NewGuid().ToString(), plan.FreshTurns);

    /// <summary>The one line that tells an operator whether the case they named is the conversation they meant.</summary>
    private static void LogTheConversationsShape(TrialPlan plan, TextWriter output)
    {
        var fresh = plan.FreshTurns.Count == 0 ? "" : $" (fresh session at turn {string.Join(", ", plan.FreshTurns)})";
        output.WriteLine(plan.Replies.Count == 0
            ? "one turn: the case's prompt.md"
            : $"{plan.Replies.Count + 1} turns: the case's prompt.md, then [{string.Join(" | ", plan.Replies)}]{fresh}");
    }

    private static void RunOneTrial(TrialPlan plan, int t, List<QuotaSpendRow> sessionSpend, TextWriter output)
    {
        var (args, paths) = (plan.Args, plan.Paths);
        if (args.Platform.IsSporadic())
        {
            SporadicPolicy.BeforeTrial(
                args.Platform, paths.RepoRoot, TierPrecondition.RunViaProcess,
                MergedQuotaLedger(paths.QuotaPath, sessionSpend), DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var pass = RunInScratch(plan, t, output);
        output.WriteLine($"trial {t}/{args.Trials}: {(pass ? "pass" : "fail")}");
        if (args.Platform.IsSporadic())
        {
            sessionSpend.Add(RecordSporadicSpend(paths.QuotaPath, args));
        }
    }

    /// <summary>
    /// One trial, in two throwaway directories outside every repository tree: the scratch project
    /// the agent works in, and a work directory holding the trial's evidence, its stand-ins, the
    /// isolated vendor-tool configuration EVERY trial runs against and — for a registry case — the
    /// staged snapshot. Both are deleted when the trial ends; every trial's evidence is copied out
    /// first (<see cref="EvidenceDirectory"/>), because a red row nobody can diagnose is the one
    /// outcome a harness whose rows cost real requests cannot afford, and a green one nobody can read
    /// proves less than it claims.
    /// </summary>
    private static bool RunInScratch(TrialPlan plan, int t, TextWriter output)
    {
        var scratch = Directory.CreateTempSubdirectory("nt-eval-").FullName;
        var work = Directory.CreateTempSubdirectory("nt-eval-work-").FullName;
        try
        {
            return RunRecorded(plan, t, scratch, work, output);
        }
        finally
        {
            TryDelete(scratch);
            TryDelete(work);
        }
    }

    private static bool RunRecorded(TrialPlan plan, int t, string scratch, string work, TextWriter output)
    {
        var environment = TrialEnvironment.Under(work, plan.Paths.RepoRoot);
        SeedTheLogin(work, output);
        output.WriteLine($"transcript: {environment.Transcript()}");
        var trial = new EvalTrial(
            EvalTrial.RealProcess, () => DateTimeOffset.UtcNow, plan.Paths.RunsPath,
            DeliveryFor(plan.PreStep, work, output), environment);
        var pass = trial.Run(
            plan.Args, plan.Paths.RepoRoot, plan.FixtureDir, plan.Paths.CaseDir, scratch, TurnsFor(plan), t);
        var kept = EvidenceDirectory.For(
            plan.Paths.RepoRoot, plan.Args.Skill, plan.Args.CaseName, t, pass, DateTimeOffset.UtcNow);
        output.WriteLine($"evidence kept: {environment.KeepEvidenceUnder(kept)}");
        return pass;
    }

    /// <summary>A registry case's own delivery, in the work directory its isolated configuration lives in.</summary>
    private static RegistryDelivery DeliveryFor(RegistryPreStep? preStep, string work, TextWriter output)
    {
        if (preStep is null)
        {
            return RegistryDelivery.None();
        }

        output.WriteLine($"registry pre-step: {preStep.Value.Id()}");
        return RegistryDelivery.Through(preStep.Value, work);
    }

    /// <summary>
    /// The throwaway configuration EVERY trial runs against, carrying the one thing a fresh one
    /// cannot do without: the subscription login. Said out loud, because a trial with no login does
    /// not fail here — it fails three steps later when the agent answers "Not logged in", a long way
    /// from the cause.
    /// </summary>
    private static void SeedTheLogin(string work, TextWriter output)
    {
        var realConfig = IsolatedAgentConfig.RealConfigDir();
        var seeded = IsolatedAgentConfig.SeedLogin(realConfig, work);
        output.WriteLine(
            $"isolated agent configuration under {IsolatedAgentConfig.ConfigDir(work)}"
            + (seeded
                ? $" (subscription login seeded from {realConfig})"
                : $" (NO login found in {realConfig} — the agent may refuse to start)"));
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
