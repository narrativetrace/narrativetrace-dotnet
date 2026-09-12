// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingNUnit;
using Xunit;

namespace NarrativeTrace.Testing.NUnit.Tests;

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
