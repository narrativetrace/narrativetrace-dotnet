// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>clarity-aggregate</c> verb: merges the per-test <c>*.clarity.json</c>
/// artifacts written at runtime into a single <see cref="ResultsFileName"/>
/// envelope, so the build gate can score real captured traces (call depth and
/// nesting) instead of the depth-1 static reflection scan. See CLARITY-13.
/// </summary>
public static class ClarityAggregateCommand
{
    /// <summary>Name of the aggregated results file the gate consumes.</summary>
    public const string ResultsFileName = "clarity-results.json";

    private const string PerTestPattern = "*.clarity.json";

    /// <summary>Aggregates the per-test clarity files under <paramref name="inputDir"/>; returns the exit code.</summary>
    public static int Run(
        string inputDir, string outputDir, TextWriter output, TextWriter error)
    {
        if (!Directory.Exists(inputDir))
        {
            error.WriteLine($"error: input directory not found: {inputDir}");
            return 1;
        }

        var files = Directory
            .EnumerateFiles(inputDir, PerTestPattern)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        var envelope = ClarityResultsAggregator.Aggregate(files.Select(File.ReadAllText));
        var path = WriteResults(envelope, outputDir);
        output.WriteLine($"Aggregated {files.Count} scenario(s) -> {path}");
        return 0;
    }

    private static string WriteResults(string envelope, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        var path = Path.Combine(outputDir, ResultsFileName);
        File.WriteAllText(path, envelope);
        return path;
    }
}
