// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Concurrent;
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
        SequenceDiagramRenderers.Mermaid,
        SequenceDiagramRenderers.PlantUml,
        JsonExporter.Export,
        CanonicalEntryArrayExporter.Canonical,
        CanonicalEntryArrayExporter.Structural);

    /// <summary>
    /// Per-(class, method) invocation counters — the engine's own execution
    /// order stands in for the invocation ordinal NUnit exposes no public
    /// accessor for. Shared across every subclass instance in the process,
    /// which is what "the suite's own order" means for a run that does not
    /// parallelize within a fixture.
    /// </summary>
    private static readonly ConcurrentDictionary<string, int> InvocationCounters = new(StringComparer.Ordinal);

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
    /// <exception cref="NarrativeApprovalException">
    /// Approval mode is on, the test otherwise passed, and the traced structure
    /// has no approved trace yet or differs from one — NUnit reports this as a
    /// tear-down failure, which is how a passing test's structure change surfaces.
    /// </exception>
    [TearDown]
    public void TearDownTrace()
    {
        var tree = _context.CaptureTrace();
        var current = TestContext.CurrentContext;
        var failed = current.Result.Outcome.Status == TestStatus.Failed;
        var identity = ResolveIdentity(current.Test);

        // Output happens before the failure print so a failing test's report can
        // speak in terms of the structural delta the write computed, localizing
        // change instead of dumping the whole trace (see PrintFailure overload).
        var rejection = ApprovalRejection(identity, current.Test.Name, tree, failed);
        var delta = WriteArtifacts(identity, current.Test.Name, failed || rejection is not null, TestContext.Out);

        PrintFailure(current.Test.Name, failed, tree, delta, TestContext.Out);
        PrintTemplateWarnings(tree, TestContext.Out);
        RecordSuiteContribution(identity, current.Test.Name, tree, delta);
        OnTraceComplete(tree);
        _context.Reset();

        if (rejection is not null)
        {
            throw rejection;
        }
    }

    /// <summary>
    /// Which artifact this invocation owns — auto-detected from
    /// <see cref="TestContext.TestAdapter.Arguments"/>: a non-empty argument
    /// array is NUnit's own signal that this is one case of a parameterized or
    /// data-driven test, since an ordinary <c>[Test]</c> always reports none.
    /// </summary>
    private static ArtifactIdentity ResolveIdentity(TestContext.TestAdapter test)
    {
        var className = test.ClassName ?? string.Empty;
        var methodName = test.MethodName ?? test.Name;
        if (test.Arguments is not { Length: > 0 })
        {
            return ArtifactIdentity.OfMethod(className, methodName);
        }

        var index = InvocationCounters.AddOrUpdate(
            className + "." + methodName, 1, (_, count) => count + 1);
        return ArtifactIdentity.OfInvocation(className, methodName, index, test.Name);
    }

    /// <summary>
    /// Approval mode runs only on a run not already failed — a red test
    /// already has the developer's attention, and its mid-flight structure
    /// must not churn the received traces.
    /// </summary>
    private NarrativeApprovalException? ApprovalRejection(
        ArtifactIdentity identity, string displayName, TraceTree tree, bool failed)
    {
        if (failed || !_output.ApprovalEnabled || tree.IsEmpty)
        {
            return null;
        }

        try
        {
            NarrativeApproval.Verify(
                tree, identity.StructuralScenario(displayName),
                NarrativeApproval.ApprovedFile(_output.ApprovedDir, identity));
            return null;
        }
        catch (NarrativeApprovalException ex)
        {
            return ex;
        }
    }

    private void RecordSuiteContribution(
        ArtifactIdentity identity, string displayName, TraceTree tree, ScenarioDelta? delta)
    {
        var scope = NarrativeSuiteScope.Current;
        if (scope is null)
        {
            return;
        }

        scope.Record(displayName, tree);
        if (delta is not null)
        {
            scope.RecordDelta(delta);
        }

        if (_output.Enabled && !tree.IsEmpty)
        {
            scope.RecordManifestEntry(
                ScenarioManifest.EntryFor(_output.Directory, identity, ScenarioFramer.Humanize(displayName)));
        }
    }

    internal ScenarioDelta? WriteArtifacts(
        string testClass, string testMethod, bool failed, TextWriter console)
    {
        return WriteArtifacts(ArtifactIdentity.OfMethod(testClass, testMethod), testMethod, failed, console);
    }

    private ScenarioDelta? WriteArtifacts(
        ArtifactIdentity identity, string displayName, bool failed, TextWriter console)
    {
        if (!_output.Enabled)
        {
            return null;
        }

        return TraceArtifactWriter.Write(
            _context.CaptureTrace(), identity, displayName, failed,
            _output.Directory, _output.Format, Renderers, console,
            _output.EntryArtifacts, RunScope.Current?.Name);
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
        PrintFailure(testName, failed, tree, delta: null, output);
    }

    /// <summary>
    /// The same failure print, localized to the scenario's structural delta
    /// against last green when one was computed (the Markdown write path) —
    /// see <see cref="NarrativeFailureReport.Build(string, TraceTree, ScenarioDelta)"/>.
    /// </summary>
    internal static void PrintFailure(
        string testName, bool failed, TraceTree tree, ScenarioDelta? delta, TextWriter output)
    {
        if (!failed)
        {
            return;
        }

        var report = delta is null
            ? NarrativeFailureReport.Build(testName, tree)
            : NarrativeFailureReport.Build(testName, tree, delta);
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
