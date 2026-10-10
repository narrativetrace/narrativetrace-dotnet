// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// Tier A2 executors for <c>narrativetrace-verify</c>'s commands, against the sixty-seconds test
/// project — the project whose files the skill's listings embed, and which runs in approval mode.
/// </summary>
internal static class VerifyReplay
{
    private static string TestProject(string repoRoot) =>
        Path.Combine(repoRoot, "tests", "NarrativeTrace.Examples.SixtySeconds.Tests");

    /// <summary>
    /// The listing's own test, run alone: it passes, and it wrote its structural trace with a span id —
    /// the artifact every later step reads.
    /// </summary>
    internal static bool RunThePathWritesTheStructuralTrace(string repoRoot)
    {
        var project = TestProject(repoRoot);
        var result = SkillReplayRegistry.RunProcess(
            "dotnet",
            $"test \"{project}\" --configuration {SkillReplayRegistry.BuildConfiguration} --no-build " +
                "--filter \"FullyQualifiedName~PlaceOrderFlowTests\"",
            repoRoot);
        var nt = Directory.EnumerateFiles(project, "customer_places_an_order.nt", SearchOption.AllDirectories)
            .FirstOrDefault(path => path.Contains($"{Path.DirectorySeparatorChar}structural{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        return result.ExitCode == 0 && nt is not null && File.ReadAllText(nt).Contains("#1 - ", StringComparison.Ordinal);
    }

    /// <summary>
    /// The approve verb, over a scratch copy of the project's baselines with one review copy planted:
    /// it promotes it and leaves no review copy — never over the committed baselines themselves.
    /// </summary>
    internal static bool ApprovePromotesAReviewCopy(string repoRoot)
    {
        var scratch = Path.Combine(Path.GetTempPath(), "nt-verify-replay-" + Guid.NewGuid().ToString("N"));
        try
        {
            var received = Path.Combine(scratch, "narratives", "PlaceOrderFlowTests", "customer_places_an_order.received.nt");
            Directory.CreateDirectory(Path.GetDirectoryName(received)!);
            File.WriteAllText(received, "scenario: Customer places an order\n");
            return RunApprove(repoRoot, scratch) == 0 && !File.Exists(received)
                && File.Exists(received.Replace(".received.nt", ".approved.nt", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(scratch))
            {
                Directory.Delete(scratch, recursive: true);
            }
        }
    }

    private static int RunApprove(string repoRoot, string dir)
    {
        var cli = Path.Combine(repoRoot, "src", "NarrativeTrace.Cli");
        return SkillReplayRegistry.RunProcess(
            "dotnet",
            $"run --project \"{cli}\" --configuration {SkillReplayRegistry.BuildConfiguration} --no-build " +
                $"-- approve --dir \"{dir}\"",
            repoRoot).ExitCode;
    }
}
