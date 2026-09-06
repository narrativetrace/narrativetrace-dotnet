// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Runtime;
using NUnit.Framework;
using NUnit.Framework.Interfaces;

namespace NarrativeTrace.TestingNUnit;

/// <summary>
/// Base class that gives each NUnit test a fresh narrative context and, when the
/// test fails, prints the captured narrative of what the code did to the test
/// output.
/// </summary>
public abstract class NarrativeTestBase
{
    private static readonly TraceArtifactRenderers Renderers = new(
        MermaidSequenceRenderer.Render,
        PlantUmlSequenceRenderer.Render,
        JsonExporter.Export,
        CanonicalEntryArrayExporter.Canonical,
        CanonicalEntryArrayExporter.Structural);

    private SyncNarrativeContext _context = null!;
    private TestArtifactSettings _output = null!;

    /// <summary>
    /// This test's narrative context. Pass it to the code under test.
    /// </summary>
    /// <remarks>
    /// Replaced with a fresh context before every test, so each test gets an
    /// isolated trace and nothing leaks between them. Only valid from
    /// <c>[SetUp]</c> onwards — reading it in a constructor or a
    /// <c>[OneTimeSetUp]</c> throws, because no context exists yet.
    /// </remarks>
    public INarrativeContext Context => _context;

    /// <summary>Creates the fresh per-test context and resolves artifact settings.</summary>
    /// <remarks>
    /// Invoked by NUnit before each test; do not call it directly. Override
    /// <see cref="CreateConfig"/> to change the tracing level rather than
    /// overriding this.
    /// </remarks>
    [SetUp]
    public void SetUpTrace()
    {
        _context = new SyncNarrativeContext(CreateConfig());
        _output = TestArtifactSettings.Resolve(ReadEnvironment);
    }

    /// <summary>
    /// Builds the config for each test. Defaults to the level resolved from the
    /// <c>NARRATIVETRACE_*</c> environment; override to inject a fixed config.
    /// </summary>
    protected virtual NarrativeTraceConfig CreateConfig()
    {
        return new NarrativeTraceConfig(ConfigResolver.Resolve(ReadEnvironment).Level);
    }

    /// <summary>
    /// Reads a configuration variable — the process environment by default;
    /// override to inject configuration in tests.
    /// </summary>
    protected virtual string? ReadEnvironment(string key)
    {
        return Environment.GetEnvironmentVariable(key);
    }

    /// <summary>
    /// Captures the trace and, when the test failed, prints its narrative to the
    /// test output; writes artifacts if output is enabled.
    /// </summary>
    /// <remarks>
    /// Invoked by NUnit after each test; do not call it directly. It reads the
    /// test's outcome from <c>TestContext</c>, which is what lets a failing test
    /// print the story of what the code actually did.
    /// </remarks>
    [TearDown]
    public void TearDownTrace()
    {
        var tree = _context.CaptureTrace();
        var current = TestContext.CurrentContext;
        var failed = current.Result.Outcome.Status == TestStatus.Failed;
        PrintFailure(current.Test.Name, failed, tree, TestContext.Out);
        PrintTemplateWarnings(tree, TestContext.Out);
        WriteArtifacts(
            current.Test.ClassName ?? string.Empty,
            current.Test.MethodName ?? current.Test.Name, failed, TestContext.Out);
        NarrativeSuiteScope.Current?.Record(current.Test.Name, tree);
        OnTraceComplete(tree);
        _context.Reset();
    }

    internal void WriteArtifacts(
        string testClass, string testMethod, bool failed, TextWriter console)
    {
        if (!_output.Enabled)
        {
            return;
        }

        TraceArtifactWriter.Write(
            _context.CaptureTrace(), testClass, testMethod, testMethod, failed,
            _output.Directory, _output.Format, Renderers, console,
            _output.EntryArtifacts);
    }

    internal static void PrintTemplateWarnings(
        TraceTree tree, TextWriter output)
    {
        var block = TemplateWarningCollector.Format(
            TemplateWarningCollector.Collect(tree));
        if (block.Length > 0)
        {
            output.Write(block);
        }
    }

    internal static void PrintFailure(
        string testName, bool failed, TraceTree tree, TextWriter output)
    {
        if (!failed)
        {
            return;
        }

        var report = NarrativeFailureReport.Build(testName, tree);
        if (report.Length > 0)
        {
            output.Write(report);
        }
    }

    /// <summary>
    /// Extension point invoked after each test's trace has been captured.
    /// </summary>
    /// <param name="tree">The finished trace for the test that just ran.</param>
    /// <remarks>
    /// Override to contribute the trace somewhere else — a suite report, a
    /// custom assertion, an external sink. Empty by default. Called during
    /// tear-down for both passing and failing tests, so it must not assume
    /// success; an exception thrown here surfaces as a tear-down failure and
    /// masks the test's own result.
    /// </remarks>
    protected virtual void OnTraceComplete(
        TraceTree tree)
    {
    }
}
