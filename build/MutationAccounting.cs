// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NarrativeTrace.Build;

/// <summary>
/// Every project in the solution must be accounted for — mutation-TESTED (a real
/// <c>stryker-config*.json</c> targets it, see <see cref="TestedProjectNames"/>), a TEST SUITE
/// (its name ends in <c>Tests</c> — <see cref="TestProjectSelection"/> — so it is never itself a
/// Stryker <c>project</c> target), or explicitly EXEMPTED with a written reason
/// (<see cref="Exemptions"/>). Mirrors <see cref="CoverageAccounting"/>'s gate-or-exemption shape
/// and the Java runtime's own PIT accounting (<c>build.gradle.kts</c>'s <c>mutationTestedModules</c> /
/// <c>mutationAgentModules</c> / <c>mutationExemptModules</c>) — three buckets
/// there too, for the identical reason: a two-bucket map cannot express "structurally can never be
/// a target" without one row per project saying so.
/// </summary>
/// <remarks>
/// Default-deny: a project present in NONE of the three buckets must fail
/// <see cref="MutationAccountingCheck"/> (wired into <c>Verify</c>, cheap — config/name checks
/// only, never runs Stryker) — adding a project must force a decision, mutate it, or write down
/// why not, instead of silently shipping unmutated forever. A project present in MORE than one
/// bucket must fail too — a stale exemption nobody removed after the project gained a real
/// stryker-config is exactly the kind of drift this exists to catch.
/// </remarks>
internal static class MutationAccounting
{
    /// <param name="Reason">
    /// Why this project is not mutation-tested — short and specific; this is what a reviewer reads
    /// two years from now, not the person who wrote it.
    /// </param>
    public sealed record Exemption(string Reason);

    /// <summary>
    /// Every <c>stryker-config*.json</c> file at the repo root, path-sorted. THE single source for
    /// which files exist — <see cref="TestedProjectNames"/> below, the <c>Mutation</c> target, and
    /// <c>VerifyAllMutationRow</c> all call this rather than each re-globbing
    /// <c>"stryker-config*.json"</c> independently, which is exactly the two-independently-hand-synced-
    /// lists shape the Java runtime's build once fell into before it single-sourced the same list.
    /// </summary>
    public static IReadOnlyList<string> ConfigFiles(string repoRoot) =>
        Directory.EnumerateFiles(repoRoot, "stryker-config*.json")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// The single source of which product projects are mutation-tested: the distinct
    /// <c>stryker-config.project</c> value read fresh out of every file <see cref="ConfigFiles"/>
    /// finds — never a second, hand-copied name list. A project earns a spot here by being the
    /// <c>project</c> target of an existing config; <c>stryker-config.valuerefs.json</c> narrows the
    /// <c>mutate</c> set on NarrativeTrace.Core, an already-tested project — it does not add one of
    /// its own, which is exactly why this is a set (dedup), not a count of config files.
    /// </summary>
    public static IReadOnlySet<string> TestedProjectNames(string repoRoot) =>
        ConfigFiles(repoRoot)
            .Select(ProjectNameFromConfig)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

    private static string? ProjectNameFromConfig(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));
        if (!document.RootElement.TryGetProperty("stryker-config", out var config)
            || !config.TryGetProperty("project", out var project))
            return null;
        return project.GetString() is { } path ? Path.GetFileNameWithoutExtension(path) : null;
    }

    private static readonly string[] ProjectDirectories = ["src", "examples", "tests", "benchmarks", "fuzz"];
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj"];

    /// <summary>
    /// Every project name in the solution, found by walking the same five top-level directories
    /// <c>NarrativeTrace.sln</c> organizes projects under. Used by the "against the real repo" test
    /// (tests/BuildScript.Tests) to check every real project without needing a NUKE
    /// <c>Solution</c> object in a plain xUnit test — the live <c>MutationAccountingCheck</c>
    /// target instead reads NUKE's own <c>Solution.AllProjects</c>, an independently-sourced way
    /// of discovering the same set (matching <see cref="TestProjectSelection.TestProjectFiles"/>'s
    /// own filesystem-vs-Solution split for the coverage side of this).
    /// </summary>
    public static IReadOnlyList<string> AllProjectNames(string repoRoot) =>
        ProjectDirectories
            .Select(dir => Path.Combine(repoRoot, dir))
            .Where(Directory.Exists)
            .SelectMany(Directory.EnumerateDirectories)
            .SelectMany(Directory.EnumerateFiles)
            .Where(file => ProjectExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Select(file => Path.GetFileNameWithoutExtension(file)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Every project neither mutation-tested nor a name-pattern test suite, with the one-line
    /// VERIFIED reason it carries no stryker-config of its own.
    /// </summary>
    public static readonly Dictionary<string, Exemption> Exemptions = new()
    {
        // ---- product modules: real src/ code, none yet given a stryker-config of its own -------
        ["NarrativeTrace.AspNetCore"] = new(
            "ASP.NET Core middleware/DI adapter over Core+Runtime+Proxy (732 lines) — DEFERRED, "
            + "no stryker-config targets it yet"),
        ["NarrativeTrace.Cli"] = new(
            "dotnet-narrativetrace CLI tool, PackAsTool (600 lines) — DEFERRED, no stryker-config "
            + "targets it yet"),
        ["NarrativeTrace.DependencyInjection"] = new(
            "thin ServiceCollection/DI adapter (254 lines) — DEFERRED, no stryker-config targets "
            + "it yet"),
        ["NarrativeTrace.Diagrams"] = new(
            "diagram renderer over Core (626 lines), own Diagrams.Tests already coverage-gated at "
            + "95% (CoverageAccounting) — DEFERRED, no stryker-config targets it yet"),
        ["NarrativeTrace.Legacy"] = new(
            "empty placeholder assembly (Placeholder.cs) reserving the net48 target — \"carries no "
            + "functionality\" by its own doc comment; nothing to mutate"),
        ["NarrativeTrace.Logging"] = new(
            "Microsoft.Extensions.Logging adapter (1106 lines) — DEFERRED, no stryker-config "
            + "targets it yet"),
        ["NarrativeTrace.MSBuild"] = new(
            "MSBuild integration package — buildTransitive .targets/.props only, zero .cs source; "
            + "no assertable invariants to mutate"),
        ["NarrativeTrace.Observability"] = new(
            "OpenTelemetry adapter (638 lines) — DEFERRED, no stryker-config targets it yet"),
        ["NarrativeTrace.Runtime"] = new(
            "no stryker-config of its own — Stryker's `project` field mutates only the assembly it "
            + "names, and Runtime carries no dedicated test project of its own (Core.Tests exercises "
            + "it instead, see CoverageAccounting's own note); DEFERRED"),
        ["NarrativeTrace.Testing.NUnit"] = new(
            "NUnit test-framework integration adapter (378 lines) — DEFERRED, no stryker-config "
            + "targets it yet"),
        ["NarrativeTrace.Testing.Xunit"] = new(
            "xUnit test-framework integration adapter (514 lines) — DEFERRED, no stryker-config "
            + "targets it yet"),

        // ---- demo/consumer code: proven by being executed, not by mutants ----------------------
        ["NarrativeTrace.Examples.Clarity"] = new(
            "demo/consumer code, run non-interactively by RunExamples (CI smoke run) — proven by "
            + "being executed, not by mutants"),
        ["NarrativeTrace.Examples.Common"] = new(
            "shared library referenced by every other example, not itself run by RunExamples — "
            + "exercised transitively when they run, and already coverage-gated at 100% by "
            + "NarrativeTrace.Examples.Common.Tests"),
        ["NarrativeTrace.Examples.ECommerce"] = new(
            "demo/consumer code, run non-interactively by RunExamples (CI smoke run) — proven by "
            + "being executed, not by mutants"),
        ["NarrativeTrace.Examples.Library"] = new(
            "demo/consumer code (F#), run non-interactively by RunExamples (CI smoke run) — proven "
            + "by being executed, not by mutants"),
        ["NarrativeTrace.Examples.Minecraft"] = new(
            "demo/consumer code, run non-interactively by RunExamples (CI smoke run) — proven by "
            + "being executed, not by mutants"),

        // ---- harnesses: drive an external tool, assert nothing themselves ----------------------
        ["NarrativeTrace.Benchmarks"] = new(
            "BenchmarkDotNet harness — measures latency/allocation against a baseline "
            + "(Benchmark target); no assertable invariants of its own to mutate"),
        ["NarrativeTrace.Fuzz"] = new(
            "SharpFuzz driver harness — dispatches to Core/Runtime targets for AFL instrumentation "
            + "and asserts nothing itself; the corpus-replay assertions live in "
            + "NarrativeTrace.SecurityTests"),
    };

    /// <summary>
    /// Every bucket <paramref name="projectName"/> belongs to: <c>"mutation-tested"</c> when a
    /// stryker-config names it, <c>"test suite"</c> when <see cref="TestProjectSelection.IsSelected"/>
    /// says its name ends in <c>Tests</c>, <c>"exempted"</c> when <paramref name="exempted"/>
    /// carries it. Empty means unaccounted; more than one means double-classified — see
    /// <see cref="Unaccounted"/> and <see cref="DoublyAccounted"/>.
    /// </summary>
    public static IReadOnlyList<string> Memberships(
        string projectName, IReadOnlyCollection<string> tested, IReadOnlyCollection<string> exempted)
    {
        var memberships = new List<string>();
        if (tested.Contains(projectName))
            memberships.Add("mutation-tested (stryker-config)");
        if (TestProjectSelection.IsSelected(projectName))
            memberships.Add("test suite (name ends in \"Tests\")");
        if (exempted.Contains(projectName))
            memberships.Add("MutationAccounting.Exemptions");
        return memberships;
    }

    /// <summary>
    /// Every name in <paramref name="projectNames"/> with zero <see cref="Memberships"/>, sorted
    /// for a stable failure message.
    /// </summary>
    public static IReadOnlyList<string> Unaccounted(
        IEnumerable<string> projectNames,
        IReadOnlyCollection<string> tested,
        IReadOnlyCollection<string> exempted)
    {
        return projectNames
            .Where(name => Memberships(name, tested, exempted).Count == 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every name in <paramref name="projectNames"/> with more than one <see cref="Memberships"/> —
    /// a project is mutation-tested, a test suite, or exempted, never two of those at once.
    /// </summary>
    public static IReadOnlyList<string> DoublyAccounted(
        IEnumerable<string> projectNames,
        IReadOnlyCollection<string> tested,
        IReadOnlyCollection<string> exempted)
    {
        return projectNames
            .Where(name => Memberships(name, tested, exempted).Count > 1)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }
}
