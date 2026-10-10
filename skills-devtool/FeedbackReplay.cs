// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Skills.Catalogue;

namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// Tier A2 executors for <c>narrativetrace-feedback</c>: the real <c>doctor --report</c> and
/// <c>feedback draft|url</c> verbs run against the fixture, with concrete value-free text standing in
/// for each angle-bracket placeholder the skill's literal carries. Nothing is filed and no network is
/// touched — the verb only writes two files under the fixture's ignored <c>TestResults/</c>.
/// </summary>
internal static class FeedbackReplay
{
    private const string ReportDirectory = "TestResults/narrativetrace";

    private const string StandInFlags =
        "--category doctor --step trap.redaction-proof --did \"ran the doctor\" "
        + "--happened \"the fix did not clear the finding\" --expected \"the finding to pass\" --language en";

    internal static bool DoctorReport(string repoRoot, string fixtureDir)
    {
        var result = SkillReplayRegistry.RunProcess(
            "dotnet", CliArguments(repoRoot, $"doctor --report --dir \"{fixtureDir}\""), repoRoot);
        return result.ExitCode is 0 or 1 && DoctorReportExists(fixtureDir);
    }

    internal static bool Draft(string repoRoot, string fixtureDir) =>
        DoctorReport(repoRoot, fixtureDir) && Verb(repoRoot, fixtureDir, "draft").ExitCode == 0;

    internal static bool Url(string repoRoot, string fixtureDir) =>
        DoctorReport(repoRoot, fixtureDir)
        && Verb(repoRoot, fixtureDir, "url") is { ExitCode: 0 } result
        && result.StdOut.TrimStart().StartsWith("https://github.com/narrativetrace/narrativetrace-dotnet/issues/new?", StringComparison.Ordinal);

    internal static bool DoctorReportExists(string fixtureDir)
    {
        var path = Path.Combine(fixtureDir, ReportDirectory, "doctor-report.json");
        if (!File.Exists(path))
        {
            return false;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("findings", out var findings)
            && findings.ValueKind == JsonValueKind.Array;
    }

    internal static bool DraftFilesExist(string fixtureDir) =>
        File.Exists(Path.Combine(fixtureDir, ReportDirectory, "feedback", "feedback-draft.md"))
        && File.Exists(Path.Combine(fixtureDir, ReportDirectory, "feedback", "feedback-body.md"));

    private static SkillReplayRegistry.ProcessResult Verb(string repoRoot, string fixtureDir, string channel) =>
        SkillReplayRegistry.RunProcess(
            "dotnet", CliArguments(repoRoot, $"feedback {channel} {StandInFlags}"), fixtureDir);

    private static string CliArguments(string repoRoot, string verb)
    {
        var cliProject = Path.Combine(repoRoot, "src", "NarrativeTrace.Cli");
        return $"run --project \"{cliProject}\" --configuration {SkillReplayRegistry.BuildConfiguration} "
            + $"--no-build -- {verb}";
    }
}
