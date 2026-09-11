// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>Covers <see cref="PropertyTestScanner"/> and <see cref="VerifyAllTestSlices"/> — how
/// <c>VerifyAll</c> partitions one <c>./build.sh Test</c> sweep into the schema's
/// <c>property</c>/<c>fuzz-tier-a</c>/<c>architecture</c>/<c>conformance</c>/<c>stress-short</c>
/// rows without a second invocation.</summary>
public sealed class VerifyAllClassificationTests : IDisposable
{
    private readonly string _testsRoot = Directory.CreateTempSubdirectory("nt-classification").FullName;

    public void Dispose() => Directory.Delete(_testsRoot, recursive: true);

    private void WriteTestFile(string project, string fileName, string content)
    {
        var dir = Path.Combine(_testsRoot, project);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    // --------------------------------------------------------------------------- PropertyTestScanner

    [Fact]
    public void FindPropertyClasses_matches_a_bare_Property_attribute()
    {
        WriteTestFile("Foo.Tests", "SomePropertyTests.cs", "public class SomePropertyTests {\n    [Property]\n    public void P() {}\n}");

        var found = PropertyTestScanner.FindPropertyClasses(_testsRoot);

        Assert.Contains(("Foo.Tests", "SomePropertyTests"), found);
    }

    [Fact]
    public void FindPropertyClasses_matches_a_Property_attribute_with_arguments()
    {
        WriteTestFile("Foo.Tests", "ScannerPropertyTests.cs",
            "public class ScannerPropertyTests {\n    [Property(MaxTest = 100)]\n    public void P() {}\n}");

        var found = PropertyTestScanner.FindPropertyClasses(_testsRoot);

        Assert.Contains(("Foo.Tests", "ScannerPropertyTests"), found);
    }

    [Fact]
    public void FindPropertyClasses_ignores_a_file_with_no_Property_attribute()
    {
        WriteTestFile("Foo.Tests", "PlainTests.cs", "public class PlainTests { [Fact] public void F() {} }");

        var found = PropertyTestScanner.FindPropertyClasses(_testsRoot);

        Assert.Empty(found);
    }

    [Fact]
    public void FindPropertyClasses_skips_bin_and_obj_directories()
    {
        WriteTestFile("Foo.Tests/bin/Debug", "Generated.cs", "[Property] class X {}");

        var found = PropertyTestScanner.FindPropertyClasses(_testsRoot);

        Assert.Empty(found);
    }

    // ---------------------------------------------------------------------------- VerifyAllTestSlices

    private static IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> ByProject(
        params (string Project, string ClassName, string Outcome)[] entries) =>
        entries
            .GroupBy(e => e.Project)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<TrxTestResult>)g
                    .Select(e => new TrxTestResult(e.ClassName, $"{e.ClassName}.M", e.Outcome, 0.1))
                    .ToList());

    [Fact]
    public void FuzzTierA_claims_the_whole_SecurityTests_project_regardless_of_Property_attribute()
    {
        var byProject = ByProject(
            ("NarrativeTrace.SecurityTests", "ScannerPropertyTests", "Passed"),
            ("NarrativeTrace.SecurityTests", "HostileCorpusTest", "Passed"),
            ("NarrativeTrace.Core.Tests", "ValueRendererPropertyTests", "Passed"));

        var fuzzTierA = VerifyAllTestSlices.FuzzTierA(byProject);

        Assert.Equal(2, fuzzTierA.Count);
    }

    [Fact]
    public void Property_excludes_SecurityTests_even_when_one_of_its_classes_is_in_the_property_set()
    {
        var byProject = ByProject(
            ("NarrativeTrace.SecurityTests", "ScannerPropertyTests", "Passed"),
            ("NarrativeTrace.Core.Tests", "ValueRendererPropertyTests", "Passed"),
            ("NarrativeTrace.Core.Tests", "PlainTests", "Passed"));
        var propertyClasses = new HashSet<(string, string)>
        {
            ("NarrativeTrace.SecurityTests", "ScannerPropertyTests"),
            ("NarrativeTrace.Core.Tests", "ValueRendererPropertyTests"),
        };

        var property = VerifyAllTestSlices.Property(byProject, propertyClasses);

        var only = Assert.Single(property);
        Assert.Equal("ValueRendererPropertyTests", only.ClassName);
    }

    [Fact]
    public void ArchitectureSlice_and_StressShort_read_their_own_named_project_only()
    {
        var byProject = ByProject(
            ("NarrativeTrace.ArchTests", "LayerDependencyTests", "Passed"),
            ("NarrativeTrace.StressTests", "PipelineRaceTests", "Passed"),
            ("NarrativeTrace.Core.Tests", "PlainTests", "Passed"));

        Assert.Single(VerifyAllTestSlices.ArchitectureSlice(byProject));
        Assert.Single(VerifyAllTestSlices.StressShort(byProject));
    }

    [Theory]
    [InlineData("TraceIdentityConformanceTests", true)]
    [InlineData("StructuralTraceConformanceTests", true)]
    [InlineData("CanonicalArtifactSchemaTests", true)]
    [InlineData("ChapterTreeSchemaTests", true)]
    [InlineData("ValueRendererPropertyTests", false)]
    [InlineData("PlainTests", false)]
    public void IsConformanceClass_matches_the_ConformanceTests_and_SchemaTests_suffixes(string className, bool expected)
    {
        Assert.Equal(expected, VerifyAllTestSlices.IsConformanceClass(className));
    }

    [Fact]
    public void ConformanceSlice_only_reads_CoreTests_and_only_conformance_named_classes()
    {
        var byProject = ByProject(
            ("NarrativeTrace.Core.Tests", "TraceIdentityConformanceTests", "Passed"),
            ("NarrativeTrace.Core.Tests", "ValueRendererPropertyTests", "Passed"),
            ("NarrativeTrace.Proxy.Tests", "SomeConformanceTests", "Passed"));

        var slice = VerifyAllTestSlices.ConformanceSlice(byProject);

        Assert.Single(slice);
        Assert.Equal("TraceIdentityConformanceTests", slice[0].ClassName);
    }

    [Fact]
    public void AllResults_flattens_every_project()
    {
        var byProject = ByProject(
            ("A", "T1", "Passed"),
            ("B", "T2", "Failed"));

        Assert.Equal(2, VerifyAllTestSlices.AllResults(byProject).Count);
    }
}
