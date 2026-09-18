// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Testing.NUnit.Tests;

/// <summary>
/// Structural guard so the parallel-scope hole cannot reopen: any source file in
/// this project that begins a <c>NarrativeSuiteScope</c> must also join
/// <see cref="SuiteScopeCollection"/>, which is what keeps those classes from
/// running concurrently and overwriting each other's report.
/// </summary>
/// <remarks>
/// Derived from the tree rather than a hand-maintained list (release rule 5): a
/// third scope-using class added tomorrow fails here until it joins, instead of
/// flaking on whichever machine happens to interleave it.
/// </remarks>
public sealed class SuiteScopeIsolationTests
{
    private static string ProjectDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null
            && !File.Exists(Path.Combine(dir, "NarrativeTrace.Testing.NUnit.Tests.csproj")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException(
            $"could not find the test project file above {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Every_file_that_begins_a_suite_scope_joins_the_serialized_collection()
    {
        var offenders = Directory
            .EnumerateFiles(ProjectDirectory(), "*.cs", SearchOption.TopDirectoryOnly)
            .Select(file => (File: file, Text: File.ReadAllText(file)))
            .Where(source => source.Text.Contains("NarrativeSuiteScope.Begin", StringComparison.Ordinal))
            .Where(source => !source.Text.Contains(
                "[Collection(SuiteScopeCollection.Name)]", StringComparison.Ordinal))
            .Select(source => Path.GetFileName(source.File))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "these files begin a NarrativeSuiteScope without joining the serialized collection, "
            + "so xUnit may run them in parallel and let one overwrite the other's report: "
            + string.Join(", ", offenders));
    }
}
