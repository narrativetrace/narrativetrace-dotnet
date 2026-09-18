// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards the scope of one mutation run. Stryker in solution mode ignores a
/// config's <c>test-projects</c> and runs every test project in the solution
/// that references the mutated one — so a single <c>clarity</c> mutant was
/// paying for Cli.Tests, Glossary.Tests, SecurityTests and ArchTests as well
/// (nightly 2026-09-17, F1: 3,756 mutants, 178 minutes, no module finished).
/// The scope therefore has to be readable off the invocation itself, not
/// inferred from a run nobody has the hours to finish.
/// </summary>
public sealed class MutationScopeTests
{
    private static string BuildScriptSource() =>
        File.ReadAllText(Path.Combine(RepositoryPath.Root(), "build", "Build.cs"));

    /// <summary>
    /// One of the two doors into solution mode: handing Stryker the solution.
    /// </summary>
    [Fact]
    public void The_stryker_invocation_never_passes_a_solution()
    {
        var strykerLines = BuildScriptSource()
            .Split('\n')
            .Where(line => line.Contains("dotnet-stryker", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(strykerLines);
        Assert.DoesNotContain(
            strykerLines,
            line => line.Contains("--solution", StringComparison.Ordinal)
                || line.Contains(" -s ", StringComparison.Ordinal));
    }

    /// <summary>
    /// And the other, which is the one that actually mattered: Stryker auto-detects
    /// the repository root's solution and enters solution mode from there whether or
    /// not the flag was passed. Only the working directory keeps it out — measured,
    /// on <c>valuerefs</c>: 1,671 tests and 1m42s from the test project's directory,
    /// 3,645 tests and no result in nine minutes from the root.
    /// </summary>
    [Fact]
    public void The_stryker_invocation_runs_from_the_module_test_project_not_the_repository_root()
    {
        var invocation = BuildScriptSource();
        var start = invocation.IndexOf("dotnet-stryker", StringComparison.Ordinal);
        Assert.True(start >= 0, "no Stryker invocation found in build/Build.cs");
        var block = invocation[start..Math.Min(invocation.Length, start + 600)];

        Assert.Contains("--test-project", block, StringComparison.Ordinal);
        Assert.Contains("WorkingDirectory = Path.GetDirectoryName(testProject)", block, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkingDirectory = RootDirectory", block, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the other half of the same guarantee: a config that named two test
    /// projects would re-create the fan-out with no <c>--solution</c> in sight.
    /// </summary>
    [Fact]
    public void Every_module_config_names_exactly_one_test_project()
    {
        var offenders = MutationAccounting.ConfigFiles(RepositoryPath.Root())
            .Select(config => (Config: Path.GetFileName(config), Count: TestProjectCount(config)))
            .Where(module => module.Count != 1)
            .Select(module => $"{module.Config} names {module.Count}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "every stryker-config must name exactly one test project, or one module's mutants "
            + "run another module's suite: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// The third way the fan-out hurt: two of the suites solution mode dragged in
    /// (<c>Examples.SixtySeconds.Tests</c>, <c>Cli.Tests</c>) spawn their own
    /// <c>dotnet</c> processes, so each mutant paid for a nested build. No
    /// configured suite may be one of those — asserted against the suites' own
    /// sources, so a spawn added to a mutation-tested suite tomorrow fails here.
    /// </summary>
    [Fact]
    public void No_configured_test_project_spawns_its_own_dotnet_process()
    {
        string[] spawnMarkers = ["Process.Start", "ProcessTasks", "\"dotnet\"", "dotnet run", "dotnet test"];

        var offenders = MutationAccounting.ConfigFiles(RepositoryPath.Root())
            .SelectMany(TestProjectDirectories)
            .Distinct(StringComparer.Ordinal)
            .Where(dir => Directory
                .EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Any(file => spawnMarkers.Any(marker =>
                    File.ReadAllText(file).Contains(marker, StringComparison.Ordinal))))
            .Select(Path.GetFileName)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "a mutation-tested suite spawns its own dotnet process, which every mutant would then "
            + "pay for: " + string.Join(", ", offenders));
    }

    /// <summary>
    /// And the reason one suite is enough, measured rather than assumed: the suite a module's
    /// config names is the suite that OWNS that module's coverage gate, so "one test project"
    /// is never a guess about which one. Derived from
    /// <see cref="CoverageAccounting.Thresholds"/>'s own Include filter and the config's own
    /// <c>project</c> — two existing single sources, no third hand-kept list to drift.
    /// </summary>
    /// <remarks>
    /// The measurement behind it (2026-09-18, every suite's cobertura line rate against every
    /// mutated assembly, <c>artifacts/covmatrix</c>): Clarity.Tests covers 99.05 % of
    /// NarrativeTrace.Clarity, Core.Tests 98.66 % of Core, Proxy.Tests 96.11 % of Proxy,
    /// Glossary.Tests 99.17 % of Glossary. The runners-up are far behind and add at most ~1
    /// point (Clarity: Cli.Tests 88.77 %, SecurityTests 81.78 %, Examples.Clarity.Tests
    /// 79.44 %; Proxy: Examples.ECommerce.Tests 71.02 %, Core.Tests 58.12 %) — so a wider
    /// "every suite over 5 %" set buys coverage that is already there and pays the whole
    /// fan-out for it: proxy re-run both ways scored 78.20 % in 0m39s on its one suite and
    /// 80.57 % in 13m44s on all eleven — +2.37 points for 21× the wall clock, still short of
    /// its 85 break. A low mutation score in a module is therefore a test-STRENGTH finding,
    /// never evidence that the configured suite is the wrong one.
    /// </remarks>
    [Fact]
    public void Every_module_is_mutated_by_the_suite_that_owns_its_coverage_gate()
    {
        var offenders = new List<string>();
        foreach (var config in MutationAccounting.ConfigFiles(RepositoryPath.Root()))
        {
            var module = MutatedAssemblyName(config);
            var suite = Path.GetFileNameWithoutExtension(
                MutationAccounting.TestProjectFile(RepositoryPath.Root(), config));

            if (!CoverageAccounting.Thresholds.TryGetValue(suite, out var gate))
            {
                offenders.Add($"{Path.GetFileName(config)}: {suite} carries no coverage gate");
                continue;
            }

            if (gate.Include is not null
                && !gate.Include.Contains($"[{module}]", StringComparison.Ordinal))
            {
                offenders.Add(
                    $"{Path.GetFileName(config)}: mutates {module} but {suite}'s coverage gate "
                    + $"owns '{gate.Include}'");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "a module must be mutated by the suite whose coverage gate owns its assembly — "
            + "otherwise the one configured suite is a guess: " + string.Join("; ", offenders));
    }

    private static string MutatedAssemblyName(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        return Path.GetFileNameWithoutExtension(
            document.RootElement
                .GetProperty("stryker-config")
                .GetProperty("project")
                .GetString()!);
    }

    private static IEnumerable<string> TestProjectDirectories(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        return document.RootElement
            .GetProperty("stryker-config")
            .GetProperty("test-projects")
            .EnumerateArray()
            .Select(entry => entry.GetString()!)
            .Select(relative => Path.GetDirectoryName(
                Path.Combine(RepositoryPath.Root(), relative.Replace('/', Path.DirectorySeparatorChar)))!)
            .ToList();
    }

    private static int TestProjectCount(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        return document.RootElement
            .GetProperty("stryker-config")
            .GetProperty("test-projects")
            .GetArrayLength();
    }
}
