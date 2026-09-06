// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using NarrativeTrace.Clarity;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class AssemblyClarityScannerTests
{
    private static string ClarityAssemblyPath =>
        typeof(ClarityScanner).Assembly.Location;

    [Fact]
    public void Scan_analyzes_public_types_of_a_real_assembly_without_running_it()
    {
        var results = AssemblyClarityScanner.Scan(ClarityAssemblyPath);

        Assert.NotEmpty(results);
        Assert.True(results.ContainsKey(nameof(ClarityScanner)));
    }

    // Java's ClarityScanner guards scan() against private nested, anonymous,
    // local, and synthetic classes. This edition draws the same line one layer
    // up — AssemblyClarityScanner only hands public and nested-public types to
    // the scanner — so the guarantee is pinned where it is actually enforced.
    [Fact]
    public void Scan_never_reports_the_private_nested_types_of_the_assembly()
    {
        var results = AssemblyClarityScanner.Scan(ClarityAssemblyPath);

        // Both are real private nested types inside ClarityAnalyzer.
        Assert.DoesNotContain("AnalysisContext", results.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("Scores", results.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void AddAssembliesIn_ignores_a_null_directory()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);

        AssemblyClarityScanner.AddAssembliesIn(paths, null);

        Assert.Empty(paths);
    }

    [Fact]
    public void AddAssembliesIn_ignores_a_nonexistent_directory()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);

        AssemblyClarityScanner.AddAssembliesIn(
            paths, Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Empty(paths);
    }

    [Fact]
    public void AddAssembliesIn_collects_dll_paths_from_a_real_directory()
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var directory = Path.GetDirectoryName(ClarityAssemblyPath)!;

        AssemblyClarityScanner.AddAssembliesIn(paths, directory);

        Assert.Contains(paths, p => p.EndsWith("NarrativeTrace.Clarity.dll", StringComparison.Ordinal));
    }
}
