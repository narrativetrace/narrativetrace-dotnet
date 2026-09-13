// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

/// <summary>
/// Ruling item 3's proof (2026-09-13), executed rather than merely argued: the
/// run name and id never enter the structural <c>.nt</c> text, the manifest's
/// per-scenario rows, or the delta computation — only the run name differs
/// when the identical scenario runs as two separate test-suite executions.
/// </summary>
/// <remarks>
/// <see cref="RunScope"/> is process-wide, so this class shares the
/// <c>"RunScope"</c> collection with <see cref="NarrativeSuiteFixtureTests"/>
/// — never running in parallel with it — to keep the ambient value this test
/// depends on from being stomped by an unrelated suite fixture mid-assertion.
/// </remarks>
[Collection("RunScope")]
public sealed class RunNameByteIdentityTests
{
    private const string ClassName = "OrderTests";
    private const string MethodName = "PlacesOrder";

    [Fact]
    public void Two_runs_of_the_same_scenario_produce_byte_identical_structural_artifacts()
    {
        var dirOne = TempDir();
        var dirTwo = TempDir();
        try
        {
            var (structuralOne, _) = RunFixture(dirOne);
            var (structuralTwo, _) = RunFixture(dirTwo);

            Assert.Equal(structuralOne, structuralTwo);
        }
        finally
        {
            DeleteDir(dirOne);
            DeleteDir(dirTwo);
        }
    }

    [Fact]
    public void Two_runs_name_different_runs_while_the_structural_scenario_matches()
    {
        var dirOne = TempDir();
        var dirTwo = TempDir();
        try
        {
            var (_, runOne) = RunFixture(dirOne);
            var (_, runTwo) = RunFixture(dirTwo);

            Assert.NotEqual(runOne, runTwo);
        }
        finally
        {
            DeleteDir(dirOne);
            DeleteDir(dirTwo);
        }
    }

    /// <summary>
    /// A per-test <see cref="NarrativeFixture"/> — a separate object, with no
    /// reference back to the suite fixture — still names its Markdown
    /// frontmatter's <c>run:</c> field with the enclosing suite's identity,
    /// via the shared <see cref="RunScope"/> ambient (2026-09-13 ruling,
    /// item 2).
    /// </summary>
    [Fact]
    public void The_per_test_frontmatter_names_the_same_run_as_the_suite_footer()
    {
        var dir = TempDir();
        try
        {
            using var suite = new NarrativeSuiteFixture(SuiteEnv(dir), TextWriter.Null);
            using var fixture = new NarrativeFixture(FixtureEnv(dir));
            RecordOneCall(fixture);

            fixture.WriteArtifacts(ClassName, MethodName, failed: false);

            var frontmatter = File.ReadAllText(TraceFile(dir, "places_order.md"));
            Assert.Contains($"run: {suite.RunIdentity.Name}\n", frontmatter, StringComparison.Ordinal);
        }
        finally
        {
            DeleteDir(dir);
        }
    }

    /// <summary>Runs the identical scenario as one full test-suite execution; returns (structural .nt bytes, run name).</summary>
    private static (string Structural, string RunName) RunFixture(string dir)
    {
        using var suite = new NarrativeSuiteFixture(SuiteEnv(dir), TextWriter.Null);
        using var fixture = new NarrativeFixture(FixtureEnv(dir));
        RecordOneCall(fixture);

        fixture.WriteArtifacts(ClassName, MethodName, failed: false);
        suite.Record("Places order", fixture.CaptureTrace());

        var runName = suite.RunIdentity.Name;
        return (File.ReadAllText(StructuralFile(dir, "places_order.nt")), runName);
    }

    private static void RecordOneCall(NarrativeFixture fixture)
    {
        fixture.Context.EnterMethod("Svc", "PlacesOrder", []);
        fixture.Context.ExitMethodWithReturn(null);
    }

    private static Func<string, string?> SuiteEnv(string dir) => key => key switch
    {
        ConfigResolver.OutputDirKey => dir,
        NarrativeTrace.Glossary.GlossarySettings.EnvKey => NarrativeTrace.Glossary.GlossarySettings.OffValue,
        _ => null,
    };

    private static Func<string, string?> FixtureEnv(string dir) => key => key switch
    {
        ConfigResolver.OutputKey => "true",
        ConfigResolver.OutputDirKey => dir,
        _ => null,
    };

    private static string TraceFile(string dir, string file) => Path.Combine(dir, "traces", ClassName, file);

    private static string StructuralFile(string dir, string file) => Path.Combine(dir, "structural", ClassName, file);

    private static string TempDir()
    {
        return Path.Combine(Path.GetTempPath(), "nt-runname-" + Guid.NewGuid().ToString("N"));
    }

    private static void DeleteDir(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
