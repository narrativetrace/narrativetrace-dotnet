// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Skills.Catalogue;

namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// Tier A2 executors for <c>add-narrativetrace-clarity</c>: the real <c>clarity-scan</c> and
/// <c>clarity-check</c> verbs run against the clarity example's built assembly, writing into a
/// throwaway directory so the checked-in example is never touched. The example is poorly named on
/// purpose, so the gate is expected to FAIL on it; what replay proves is that the verb is decisive
/// (exit 0 or 1, never a usage error or a crash) and names what it found.
/// </summary>
internal static class ClarityReplay
{
    internal readonly record struct ScanOutcome(bool Succeeded, string? ResultsPath, string? OutputDirectory);

    /// <summary>Scans and discards the throwaway reports — for a step that only needs to know it worked.</summary>
    internal static bool ScanSucceeds(string repoRoot, string fixtureDir)
    {
        var scan = Scan(repoRoot, fixtureDir);
        Discard(scan);
        return scan.Succeeded;
    }

    internal static string? FixtureAssembly(string fixtureDir)
    {
        var name = new DirectoryInfo(fixtureDir).Name;
        var candidate = Path.Combine(fixtureDir, "bin", SkillReplayRegistry.BuildConfiguration, "net10.0", name + ".dll");
        return File.Exists(candidate) ? candidate : null;
    }

    internal static ScanOutcome Scan(string repoRoot, string fixtureDir)
    {
        var assembly = FixtureAssembly(fixtureDir);
        if (assembly is null)
        {
            return new ScanOutcome(false, null, null);
        }

        var output = Path.Combine(Path.GetTempPath(), "nt-clarity-replay-" + Guid.NewGuid().ToString("N"));
        var result = Cli(repoRoot, $"clarity-scan --assembly \"{assembly}\" --output-dir \"{output}\"");
        var json = Path.Combine(output, "clarity-scan-results.json");
        var markdown = Path.Combine(output, "clarity-scan-report.md");
        var written = result.ExitCode == 0 && HasScenarios(json) && NonEmpty(markdown);
        return new ScanOutcome(written, written ? json : null, output);
    }

    internal static bool CheckIsDecisive(string repoRoot, string fixtureDir)
    {
        var scan = Scan(repoRoot, fixtureDir);
        if (scan.ResultsPath is null)
        {
            return false;
        }

        var check = Cli(repoRoot, GateArguments(scan.ResultsPath));
        Discard(scan);
        return check.ExitCode == 0
            || (check.ExitCode == 1 && check.StdOut.Contains("HIGH issues", StringComparison.Ordinal));
    }

    internal static bool MsBuildGateIsDeclared(string repoRoot)
    {
        var targets = File.ReadAllText(Path.Combine(
            repoRoot, "src", "NarrativeTrace.MSBuild", "buildTransitive", "NarrativeTrace.MSBuild.targets"));
        var props = File.ReadAllText(Path.Combine(
            repoRoot, "src", "NarrativeTrace.MSBuild", "buildTransitive", "NarrativeTrace.MSBuild.props"));
        return targets.Contains("<Target Name=\"ClarityCheck\"", StringComparison.Ordinal)
            && props.Contains("<ClarityMaxHighIssues", StringComparison.Ordinal);
    }

    private static void Discard(ScanOutcome scan)
    {
        if (scan.OutputDirectory is not null && Directory.Exists(scan.OutputDirectory))
        {
            Directory.Delete(scan.OutputDirectory, recursive: true);
        }
    }

    /// <summary>The skill's own gate command, minus the tool prefix, pointed at the results this replay wrote.</summary>
    private static string GateArguments(string resultsPath) =>
        ClarityCommands.Check
            .Replace(ToolPrefix, string.Empty, StringComparison.Ordinal)
            .Replace(ClarityCommands.JsonReportPath, $"\"{resultsPath}\"", StringComparison.Ordinal);

    private const string ToolPrefix = "dotnet tool run dotnet-narrativetrace ";

    /// <summary>A report with no scenario is a scan of nothing, which the skill's verify does not accept.</summary>
    private static bool HasScenarios(string path)
    {
        if (!NonEmpty(path))
        {
            return false;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("scenarios", out var scenarios)
            && scenarios.ValueKind == JsonValueKind.Array
            && scenarios.GetArrayLength() > 0;
    }

    private static bool NonEmpty(string path) => File.Exists(path) && new FileInfo(path).Length > 0;

    private static SkillReplayRegistry.ProcessResult Cli(string repoRoot, string verb)
    {
        var cliProject = Path.Combine(repoRoot, "src", "NarrativeTrace.Cli");
        return SkillReplayRegistry.RunProcess(
            "dotnet",
            $"run --project \"{cliProject}\" --configuration {SkillReplayRegistry.BuildConfiguration} --no-build -- {verb}",
            repoRoot);
    }
}
