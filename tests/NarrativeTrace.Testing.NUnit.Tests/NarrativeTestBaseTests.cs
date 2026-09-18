// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingNUnit;
using Xunit;

namespace NarrativeTrace.Testing.NUnit.Tests;

[Collection(SuiteScopeCollection.Name)]
public class NarrativeTestBaseTests
{
    [Fact]
    public void Creates_active_context()
    {
        var test = new TestSubject();
        test.SetUpTrace();

        Assert.True(test.Context.IsActive);
    }

    [Fact]
    public void Base_honors_off_level_from_resolved_config()
    {
        var test = new OffSubject();
        test.SetUpTrace();

        Assert.False(test.Context.IsActive);
    }

    [Fact]
    public void Captures_trace_on_teardown()
    {
        var test = new TestSubject();
        test.SetUpTrace();
        test.Context.EnterMethod("Svc", "run", []);
        test.Context.ExitMethodWithReturn(null);
        test.TearDownTrace();

        Assert.Single(test.LastTrace.Roots);
    }

    [Fact]
    public void PrintFailure_writes_the_narrative_when_the_test_failed()
    {
        var writer = new StringWriter();

        NarrativeTestBase.PrintFailure("Places_an_order", failed: true, TreeWithNode(), writer);

        Assert.Contains("OrderService", writer.ToString());
    }

    [Fact]
    public void PrintFailure_writes_nothing_when_the_test_passed()
    {
        var writer = new StringWriter();

        NarrativeTestBase.PrintFailure("Places_an_order", failed: false, TreeWithNode(), writer);

        Assert.Equal(string.Empty, writer.ToString());
    }

    /// <summary>The delta-aware overload localizes a failing test's report to what changed since last green.</summary>
    [Fact]
    public void PrintFailure_with_a_changed_delta_localizes_to_the_delta()
    {
        var writer = new StringWriter();
        var delta = new ScenarioDelta("Places_an_order", ScenarioDeltaKind.Changed, "+1 call X.y", "diff-text");

        NarrativeTestBase.PrintFailure("Places_an_order", failed: true, TreeWithNode(), delta, writer);

        Assert.Contains("Changed since last green (+1 call X.y):", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("diff-text", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintFailure_with_a_null_delta_falls_back_to_the_whole_trace_dump()
    {
        var writer = new StringWriter();

        NarrativeTestBase.PrintFailure("Places_an_order", failed: true, TreeWithNode(), delta: null, writer);

        Assert.Contains("OrderService", writer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void PrintTemplateWarnings_reports_unresolved_placeholders()
    {
        var writer = new StringWriter();
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Do", [], "hello {typo}"),
                new Returned(null), [], 0),
        ]);

        NarrativeTestBase.PrintTemplateWarnings(tree, writer);

        Assert.Contains("Svc.Do: {typo} in narration", writer.ToString());
    }

    private static TraceTree TreeWithNode()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);
    }

    [Fact]
    public void Writes_markdown_companions_when_output_is_enabled()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var test = new OutputSubject(dir);
            test.SetUpTrace();
            test.Context.EnterMethod("Svc", "PlacesOrder", []);
            test.Context.ExitMethodWithReturn(null);

            test.EmitArtifacts("OrderTests", "PlacesOrder");

            Assert.True(File.Exists(Path.Combine(
                dir, "traces", "OrderTests", "places_order.md")));
            Assert.True(File.Exists(Path.Combine(
                dir, "diagrams", "OrderTests", "places_order.mmd")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Writes_artifacts_by_default_with_no_env_var_set()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var test = new DirOnlySubject(dir);
            test.SetUpTrace();
            test.Context.EnterMethod("Svc", "PlacesOrder", []);
            test.Context.ExitMethodWithReturn(null);

            test.EmitArtifacts("OrderTests", "PlacesOrder");

            Assert.True(File.Exists(Path.Combine(
                dir, "traces", "OrderTests", "places_order.md")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Writes_nothing_when_output_is_explicitly_disabled()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var test = new DisabledOutputSubject(dir);
            test.SetUpTrace();
            test.Context.EnterMethod("Svc", "PlacesOrder", []);
            test.Context.ExitMethodWithReturn(null);

            test.EmitArtifacts("OrderTests", "PlacesOrder");

            Assert.False(Directory.Exists(dir));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void Writes_the_entry_arrays_when_their_switches_are_set()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var test = new OutputSubject(dir, entryArrays: true);
            test.SetUpTrace();
            test.Context.EnterMethod("Svc", "PlacesOrder", []);
            test.Context.ExitMethodWithReturn(null);

            test.EmitArtifacts("OrderTests", "PlacesOrder");

            Assert.True(File.Exists(Path.Combine(
                dir, "traces", "OrderTests", "places_order.canonical.json")));
            Assert.True(File.Exists(Path.Combine(
                dir, "traces", "OrderTests", "places_order.structural.json")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private sealed class OutputSubject : NarrativeTestBase
    {
        private readonly string _dir;
        private readonly bool _entryArrays;

        public OutputSubject(string dir, bool entryArrays = false)
        {
            _dir = dir;
            _entryArrays = entryArrays;
        }

        public void EmitArtifacts(string testClass, string testMethod)
        {
            WriteArtifacts(testClass, testMethod, failed: false, TextWriter.Null);
        }

        protected override string? ReadEnvironment(string key)
        {
            return key switch
            {
                ConfigResolver.OutputKey => "true",
                ConfigResolver.OutputDirKey => _dir,
                ConfigResolver.CanonicalJsonKey or ConfigResolver.StructuralJsonKey =>
                    _entryArrays ? "true" : null,
                _ => null,
            };
        }
    }

    private sealed class DirOnlySubject : NarrativeTestBase
    {
        private readonly string _dir;

        public DirOnlySubject(string dir)
        {
            _dir = dir;
        }

        public void EmitArtifacts(string testClass, string testMethod)
        {
            WriteArtifacts(testClass, testMethod, failed: false, TextWriter.Null);
        }

        protected override string? ReadEnvironment(string key)
        {
            return key == ConfigResolver.OutputDirKey ? _dir : null;
        }
    }

    private sealed class DisabledOutputSubject : NarrativeTestBase
    {
        private readonly string _dir;

        public DisabledOutputSubject(string dir)
        {
            _dir = dir;
        }

        public void EmitArtifacts(string testClass, string testMethod)
        {
            WriteArtifacts(testClass, testMethod, failed: false, TextWriter.Null);
        }

        protected override string? ReadEnvironment(string key)
        {
            return key switch
            {
                ConfigResolver.OutputKey => "false",
                ConfigResolver.OutputDirKey => _dir,
                _ => null,
            };
        }
    }

    [Fact]
    public void TearDownTrace_passes_when_the_structure_matches_the_approved_trace()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        var approvedDir = Path.Combine(Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(approvedDir, AdhocClassDir));
            File.WriteAllText(
                Path.Combine(approvedDir, AdhocClassDir, "adhoc_test_method.approved.nt"),
                "scenario: Adhoc test method\n\n- Svc.run()\n");
            var test = new ApprovalSubject(dir, approvedDir);
            test.SetUpTrace();
            test.Context.EnterMethod("Svc", "run", []);
            test.Context.ExitMethodWithReturn(null);

            var exception = Record.Exception(() => test.TearDownTrace());

            Assert.Null(exception);
        }
        finally
        {
            DeleteDirIfExists(dir);
            DeleteDirIfExists(approvedDir);
        }
    }

    [Fact]
    public void TearDownTrace_fails_and_does_not_advance_the_baseline_when_the_structure_differs()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        var approvedDir = Path.Combine(Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(approvedDir, AdhocClassDir));
            File.WriteAllText(
                Path.Combine(approvedDir, AdhocClassDir, "adhoc_test_method.approved.nt"),
                "scenario: Adhoc test method\n\n- Svc.OtherCall()\n");
            var test = new ApprovalSubject(dir, approvedDir);
            test.SetUpTrace();
            test.Context.EnterMethod("Svc", "run", []);
            test.Context.ExitMethodWithReturn(null);

            Assert.Throws<NarrativeApprovalException>(() => test.TearDownTrace());

            var ntFile = Path.Combine(dir, "structural", AdhocClassDir, "adhoc_test_method.nt");
            Assert.False(File.Exists(ntFile));
        }
        finally
        {
            DeleteDirIfExists(dir);
            DeleteDirIfExists(approvedDir);
        }
    }

    [Fact]
    public void Suite_scope_receives_the_manifest_entry_and_delta_from_teardown()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nt-nunit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var report = new NarrativeSuiteReport(
                dir, key => key == NarrativeTrace.Glossary.GlossarySettings.EnvKey
                    ? NarrativeTrace.Glossary.GlossarySettings.OffValue
                    : null);
            NarrativeSuiteScope.Begin(report);
            try
            {
                var test = new OutputSubject(dir);
                test.SetUpTrace();
                test.Context.EnterMethod("Svc", "run", []);
                test.Context.ExitMethodWithReturn(null);

                test.TearDownTrace();
            }
            finally
            {
                // Never leave the process-global scope pointing at this test's
                // report: the next test in the collection would record into it.
                NarrativeSuiteScope.End(TextWriter.Null);
            }

            Assert.True(File.Exists(Path.Combine(dir, "manifest.json")));
            Assert.Contains(
                "narrativetrace/scenario-manifest/1", File.ReadAllText(Path.Combine(dir, "manifest.json")),
                StringComparison.Ordinal);
        }
        finally
        {
            DeleteDirIfExists(dir);
        }
    }

    /// <summary>
    /// Outside real NUnit execution <c>TestContext.CurrentContext.Test</c> reports the engine's own
    /// placeholder identity — fixed and known, which is what lets these tests assert on exact paths.
    /// </summary>
    private const string AdhocClassDir = "TestExecutionContext+AdhocContext";

    private static void DeleteDirIfExists(string dir)
    {
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class ApprovalSubject : NarrativeTestBase
    {
        private readonly string _dir;
        private readonly string _approvedDir;

        public ApprovalSubject(string dir, string approvedDir)
        {
            _dir = dir;
            _approvedDir = approvedDir;
        }

        protected override string? ReadEnvironment(string key)
        {
            return key switch
            {
                ConfigResolver.OutputKey => "true",
                ConfigResolver.OutputDirKey => _dir,
                ConfigResolver.ApprovalKey => "true",
                ConfigResolver.ApprovedDirKey => _approvedDir,
                _ => null,
            };
        }
    }

    private sealed class OffSubject : NarrativeTestBase
    {
        protected override NarrativeTraceConfig CreateConfig()
        {
            return new NarrativeTraceConfig(
                ConfigResolver.Resolve(
                    key => key == ConfigResolver.LevelKey ? "OFF" : null).Level);
        }
    }

    private sealed class TestSubject : NarrativeTestBase
    {
        public TraceTree LastTrace { get; private set; }
            = new([]);

        protected override void OnTraceComplete(
            TraceTree tree)
        {
            LastTrace = tree;
        }
    }
}
