// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Build;

/// <summary>
/// Backs the <c>DemoWiringCheck</c> target — keeps the demo launcher's
/// per-scenario wiring notes in step with the scenarios the examples print.
/// </summary>
/// <remarks>
/// <c>demo.sh</c> explains, for every scenario, how that scenario's trace is
/// configured (<c>AddNarrativeTracing</c> vs. a hand-rolled
/// <c>NarrativeTraceProxy</c>, which attributes are in play). That prose lives
/// in <c>examples/demo/wiring.awk</c>, keyed by the scenario's header text —
/// deliberately outside the example sources, so the examples stay
/// reference-grade code. The cost of that split is drift: renaming a
/// scenario, adding one, or deleting one leaves the launcher silently wrong.
/// This check closes that hole in both directions.
/// </remarks>
internal static class DemoWiringSupport
{
    /// <summary>Titles handed to the shared driver: <c>run.BeginScenario("Scenario 5: Out of Stock")</c>.</summary>
    private static readonly Regex ScenarioCall = new(
        @"BeginScenario\(""(.+?)""\)", RegexOptions.CultureInvariant);

    /// <summary>Header literals printed directly, such as <c>"=== Scenario 2: Out of Stock ==="</c>.</summary>
    private static readonly Regex InlineHeader = new(
        @"===\s+(.+?)\s+===", RegexOptions.CultureInvariant);

    /// <summary>Table entries of the form <c>wiring["Scenario 5: Out of Stock"] = "..."</c>.</summary>
    private static readonly Regex WiringKey = new(
        @"wiring\[""(.+?)""\]", RegexOptions.CultureInvariant);

    private const string WiringTable = "examples/demo/wiring.awk";

    private static readonly string[] SourceExtensions = [".cs", ".fs"];

    /// <summary>Every scenario title the example sources print.</summary>
    public static IReadOnlySet<string> ScenarioTitles(string repoRoot)
    {
        return ExampleSources(repoRoot)
            .SelectMany(file => TitlesIn(File.ReadAllText(file)))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Every key of the wiring table; empty when the table is missing.</summary>
    public static IReadOnlySet<string> WiringKeys(string repoRoot)
    {
        var table = Path.Combine(repoRoot, WiringTable);
        if (!File.Exists(table))
            return new HashSet<string>(StringComparer.Ordinal);
        return WiringKey.Matches(File.ReadAllText(table))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies the wiring table against the examples; returns problems
    /// sorted, empty when the two agree. A scenario the launcher cannot
    /// explain is the failure that matters: the demo would print the header
    /// and silently skip the "how this trace is configured" note.
    /// </summary>
    public static IReadOnlyList<string> Check(string repoRoot)
    {
        if (!File.Exists(Path.Combine(repoRoot, WiringTable)))
            return [$"{WiringTable} is missing — the demo launcher explains nothing without it"];

        var sources = ExampleSources(repoRoot).Select(File.ReadAllText).ToList();
        var keys = WiringKeys(repoRoot);
        var unexplained = sources
            .SelectMany(TitlesIn)
            .Distinct(StringComparer.Ordinal)
            .Where(title => !keys.Contains(title))
            .Select(title => $"scenario \"{title}\" has no wiring note — add wiring[\"{title}\"] to {WiringTable}");
        var orphaned = keys
            .Where(key => !sources.Any(source => source.Contains(key, StringComparison.Ordinal)))
            .Select(key => $"wiring note \"{key}\" matches no example scenario — renamed or deleted?");

        return unexplained.Concat(orphaned).OrderBy(p => p, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Literal titles in one source file. Comment lines are skipped (XML
    /// docs quote the <c>=== title ===</c> shape when describing it); a brace
    /// or a quote inside a header means it is built from a variable, and the
    /// real title is captured where the label string itself is written.
    /// </summary>
    private static IEnumerable<string> TitlesIn(string source)
    {
        var code = string.Join("\n", source.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
        return new[] { ScenarioCall, InlineHeader }
            .SelectMany(pattern => pattern.Matches(code).Select(m => m.Groups[1].Value))
            .Where(title => !title.Contains('{', StringComparison.Ordinal) && !title.Contains('"', StringComparison.Ordinal));
    }

    private static IEnumerable<string> ExampleSources(string repoRoot)
    {
        var examples = Path.Combine(repoRoot, "examples");
        if (!Directory.Exists(examples))
            return [];
        return Directory.EnumerateFiles(examples, "*", SearchOption.AllDirectories)
            .Where(file => SourceExtensions.Contains(Path.GetExtension(file), StringComparer.Ordinal))
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"))
            .OrderBy(file => file, StringComparer.Ordinal);
    }
}
