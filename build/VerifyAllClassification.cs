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
/// Finds every test class that declares at least one FsCheck <c>[Property]</c> test, by scanning
/// source rather than reflecting over a built assembly — the same "read the repo, not the runtime"
/// shape Java's buildSrc <c>PropertyTestClassifier</c> uses for <c>@Property</c>.
/// </summary>
/// <remarks>
/// Identifies the owning class by file name, not by parsing the class declaration out of the
/// source: every property-test file surveyed in this repo while writing this declares exactly one
/// test class, named after its file — the same convention <c>TestProjectSelection</c> and this
/// build's own metrics helpers already lean on. A file that ever broke this convention would only
/// blur <c>test_classes</c>' count, never a row's pass/fail — that still comes from the real
/// <c>.trx</c> outcome, matched by the same name.
/// </remarks>
public static class PropertyTestScanner
{
    private static readonly Regex PropertyAttribute = new(@"^\s*\[Property(\(|\])", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Every <c>(project, className)</c> pair under <paramref name="testsRoot"/> (the repo's
    /// <c>tests/</c> directory) whose file carries at least one <c>[Property(...)]</c> or bare
    /// <c>[Property]</c> attribute — deliberately not the fully-qualified <c>[FsCheck.Xunit.Property]</c>
    /// form, which no file in this repo uses, so a plain <c>\[Property\]</c> match without that
    /// prefix already covers every real usage.
    /// </summary>
    public static IReadOnlySet<(string Project, string ClassName)> FindPropertyClasses(string testsRoot)
    {
        var found = new HashSet<(string Project, string ClassName)>();
        if (!Directory.Exists(testsRoot))
            return found;

        foreach (var projectDir in Directory.EnumerateDirectories(testsRoot))
        {
            var project = Path.GetFileName(projectDir);
            foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                    continue;
                if (PropertyAttribute.IsMatch(File.ReadAllText(file)))
                    found.Add((project, Path.GetFileNameWithoutExtension(file)));
            }
        }
        return found;
    }
}

/// <summary>
/// Partitions one repo-wide <c>dotnet test</c> sweep's <c>.trx</c> results into the schema's
/// test-derived rows (<c>unit-tests</c>, <c>property</c>, <c>fuzz-tier-a</c>, <c>architecture</c>,
/// <c>conformance</c>, <c>stress-short</c>) — the .NET analog of Java's <c>PropertyTestClassifier
/// .matching</c>. Every slice here is a <em>subset</em> of the same run <c>unit-tests</c> reports as
/// its own grand total; only <c>architecture</c> also draws on one further, separate, real
/// invocation (<c>CouplingReport</c>, the JDepend-equivalent half) — <c>conformance</c> is fully
/// derived, 0 additional invocations, unlike the Java precedent's separate GradleTestKit
/// <c>functionalTest</c> run (see <see cref="BuildScriptTestsProject"/> for why).
/// </summary>
public static class VerifyAllTestSlices
{
    /// <summary>
    /// The whole project is Tier A by this repo's own documented architecture (see
    /// <c>documentation/security-testing.md</c>: "Tier A is what runs on every commit", the whole
    /// <c>NarrativeTrace.SecurityTests</c> suite) — not only its <c>[Property]</c> classes, so
    /// fuzz-tier-a claims the whole project and <see cref="Property"/> below excludes it entirely,
    /// rather than the two rows double-counting its property tests.
    /// </summary>
    public const string SecurityTestsProject = "NarrativeTrace.SecurityTests";

    public const string ArchTestsProject = "NarrativeTrace.ArchTests";
    public const string StressTestsProject = "NarrativeTrace.StressTests";
    public const string CoreTestsProject = "NarrativeTrace.Core.Tests";

    /// <summary>
    /// This whole project — README.md's own words, "validates build behavior itself" — is the
    /// build-tooling self-test half of <c>conformance</c>, the .NET analog of Java's dedicated
    /// <c>narrativetrace-build-tests</c> module. Deliberately <em>not</em> the separate
    /// <c>BuildScriptTests</c> NUKE target (its Category=SpawnsBuild tests, which shell out to a
    /// nested <c>./build.sh</c>): that target cannot run as a subprocess of <c>VerifyAll</c> itself
    /// — NUKE's own engine holds <c>.nuke/temp/build.log</c> open for VerifyAll's entire run, so a
    /// nested invocation started from inside it can never acquire that same path (confirmed
    /// reproducing on an unmodified checkout — see <c>tests/BuildScript.Tests/NukeBuildTests.cs</c>).
    /// This project's ORDINARY tests carry no such conflict and already ride the same
    /// <c>unit-tests</c> sweep at zero extra cost.
    /// </summary>
    public const string BuildScriptTestsProject = "BuildScript.Tests";

    private static readonly string[] ConformanceSuffixes = ["ConformanceTests", "SchemaTests"];

    public static IReadOnlyList<TrxTestResult> AllResults(
        IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> byProject) =>
        byProject.Values.SelectMany(results => results).ToList();

    public static IReadOnlyList<TrxTestResult> FuzzTierA(
        IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> byProject) =>
        byProject.GetValueOrDefault(SecurityTestsProject, []);

    /// <summary>Repo-wide <c>[Property]</c> classes, minus the whole of <see cref="SecurityTestsProject"/>
    /// (claimed by <see cref="FuzzTierA"/>) — see the class remarks for why that split, not a plain
    /// repo-wide count, is the non-overlapping partition.</summary>
    public static IReadOnlyList<TrxTestResult> Property(
        IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> byProject,
        IReadOnlySet<(string Project, string ClassName)> propertyClasses) =>
        byProject
            .Where(kv => kv.Key != SecurityTestsProject)
            .SelectMany(kv => kv.Value.Where(r => propertyClasses.Contains((kv.Key, SimpleClassName(r.ClassName)))))
            .ToList();

    public static IReadOnlyList<TrxTestResult> ArchitectureSlice(
        IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> byProject) =>
        byProject.GetValueOrDefault(ArchTestsProject, []);

    public static IReadOnlyList<TrxTestResult> StressShort(
        IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> byProject) =>
        byProject.GetValueOrDefault(StressTestsProject, []);

    /// <summary>The schema/contract-conformance classes inside <see cref="CoreTestsProject"/> (see
    /// <see cref="IsConformanceClass"/>) plus the whole of <see cref="BuildScriptTestsProject"/> —
    /// both sliced from the same <c>unit-tests</c> run, 0 additional invocations.</summary>
    public static IReadOnlyList<TrxTestResult> ConformanceSlice(
        IReadOnlyDictionary<string, IReadOnlyList<TrxTestResult>> byProject)
    {
        var schemaClasses = byProject.GetValueOrDefault(CoreTestsProject, [])
            .Where(r => IsConformanceClass(SimpleClassName(r.ClassName)));
        var buildToolingTests = byProject.GetValueOrDefault(BuildScriptTestsProject, []);
        return schemaClasses.Concat(buildToolingTests).ToList();
    }

    /// <summary>A class name ending in <c>ConformanceTests</c> (identity/tree conformance) or
    /// <c>SchemaTests</c> (canonical-artifact/chapter-tree schema conformance) — the naming
    /// convention every schema/contract test class in <c>NarrativeTrace.Core.Tests</c> already
    /// follows (<c>TraceIdentityConformanceTests</c>, <c>StructuralTraceConformanceTests</c>,
    /// <c>CanonicalArtifactSchemaTests</c>, <c>CanonicalSchemaTests</c>, <c>ChapterTreeSchemaTests</c>).</summary>
    public static bool IsConformanceClass(string simpleClassName) =>
        ConformanceSuffixes.Any(suffix => simpleClassName.EndsWith(suffix, StringComparison.Ordinal));

    private static string SimpleClassName(string fullyQualifiedOrSimple)
    {
        var lastDot = fullyQualifiedOrSimple.LastIndexOf('.');
        return lastDot < 0 ? fullyQualifiedOrSimple : fullyQualifiedOrSimple[(lastDot + 1)..];
    }
}
