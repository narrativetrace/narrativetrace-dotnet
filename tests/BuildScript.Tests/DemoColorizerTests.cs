// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Pins the demo colorizer (<c>examples/demo/colorize.awk</c>) against the
/// examples it styles. The colorizer rewrites each section marker into an
/// annotated label naming the renderer behind it, so a rule matching a marker
/// no example prints silently stops annotating that section: the viewer is
/// shown a bare marker where the demo promised an explanation.
/// </summary>
public sealed class DemoColorizerTests
{
    [Fact]
    public void Every_section_rule_matches_a_marker_an_example_prints()
    {
        var printed = PrintedSectionMarkers();

        var ruled = ColorizerSectionRules();

        Assert.NotEmpty(ruled);
        Assert.All(ruled, rule => Assert.Contains(rule, printed));
    }

    [Fact]
    public void Every_renderer_the_labels_name_is_a_real_type()
    {
        var named = RenderersNamedInLabels();

        Assert.NotEmpty(named);
        Assert.All(named, renderer => Assert.True(
            RendererExists(renderer),
            $"colorize.awk credits '{renderer}', which is not a type under src/"));
    }

    /// <summary>
    /// The renderer types the annotated labels credit. The demo's whole claim
    /// is that each view comes out of a named component, so a label naming a
    /// type that does not exist teaches the viewer something untrue.
    /// </summary>
    private static IReadOnlyList<string> RenderersNamedInLabels()
    {
        var awk = ReadRepositoryFile("examples/demo/colorize.awk");
        return Regex.Matches(awk, @"\b[A-Z][A-Za-z0-9]*Renderer\b")
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool RendererExists(string typeName)
    {
        return Directory
            .EnumerateFiles(Path.Combine(RepositoryPath.Root(), "src"), typeName + ".cs", SearchOption.AllDirectories)
            .Any();
    }

    /// <summary>
    /// The section markers the examples emit, read from their single source
    /// (<c>Sections</c>) rather than from the rendered output.
    /// </summary>
    private static IReadOnlyList<string> PrintedSectionMarkers()
    {
        return Markers(ReadRepositoryFile(
            "examples/NarrativeTrace.Examples.Common/Sections.cs"));
    }

    /// <summary>
    /// The markers the colorizer annotates by name. The catch-all rule
    /// (<c>^--- .* ---$</c>) is the deliberate fallback and is excluded.
    /// </summary>
    private static IReadOnlyList<string> ColorizerSectionRules()
    {
        var awk = ReadRepositoryFile("examples/demo/colorize.awk");
        return Regex.Matches(awk, @"clean ~ /\^(--- .+? ---)\$/")
            .Select(match => Unescape(match.Groups[1].Value))
            .Where(rule => !rule.Contains(".*", StringComparison.Ordinal))
            .ToList();
    }

    private static IReadOnlyList<string> Markers(string text)
    {
        return Regex.Matches(text, "--- .+? ---")
            .Select(match => match.Value)
            .ToList();
    }

    private static string Unescape(string awkPattern)
    {
        return awkPattern
            .Replace("\\(", "(", StringComparison.Ordinal)
            .Replace("\\)", ")", StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        return File.ReadAllText(Path.Combine(RepositoryPath.Root(), relativePath));
    }
}
