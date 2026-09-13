// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.TestingXunit;

/// <summary>
/// Per-test xUnit fixture: owns a narrative context, prints the story when a
/// test fails, and optionally writes trace artifacts to disk.
/// </summary>
/// <remarks>
/// <para>
/// Register it as a class fixture (<c>IClassFixture&lt;NarrativeFixture&gt;</c>)
/// or construct one per test with <c>new NarrativeFixture()</c> (environment
/// config) or <see cref="Create(NarrativeTraceConfig)"/> (explicit config). It
/// holds a single
/// <see cref="SyncNarrativeContext"/>, so <b>one fixture serves one test at a
/// time</b> — sharing an instance across tests that xUnit runs in parallel
/// merges their spans into one trace. A class fixture is safe because xUnit does
/// not parallelize within a class.
/// </para>
/// <para>
/// Artifact writing is <b>on by default</b> (owner ruling, 2026-09-11):
/// <see cref="WriteArtifacts(string, string, bool)"/> writes to the ephemeral,
/// gitignored <see cref="TestArtifactSettings.DefaultDirectory"/> unless
/// <c>NARRATIVETRACE_OUTPUT=false</c> opts out.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public sealed class OrderTests(NarrativeFixture trace) : IClassFixture&lt;NarrativeFixture&gt;
/// {
///     [Fact]
///     public void Places_an_order() =>
///         trace.Run("places an order", ctx => new OrderService(ctx).Place(42));
/// }
/// </code>
/// </example>
public sealed class NarrativeFixture : IDisposable
{
    private static readonly TraceArtifactRenderers Renderers = new(
        SequenceDiagramRenderers.Mermaid,
        SequenceDiagramRenderers.PlantUml,
        JsonExporter.Export,
        CanonicalEntryArrayExporter.Canonical,
        CanonicalEntryArrayExporter.Structural);

    private readonly SyncNarrativeContext _context;
    private readonly TestArtifactSettings _output;

    /// <summary>
    /// Creates a fixture configured from the <c>NARRATIVETRACE_*</c> environment.
    /// </summary>
    /// <remarks>The constructor xUnit calls when the fixture is registered.</remarks>
    public NarrativeFixture()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal NarrativeFixture(Func<string, string?> readEnv)
        : this(
            new NarrativeTraceConfig(ConfigResolver.Resolve(readEnv).Level),
            readEnv)
    {
    }

    /// <summary>Creates a fixture with an explicit configuration, ignoring the environment.</summary>
    /// <param name="config">The level and service identity to capture under.</param>
    /// <remarks>
    /// A factory rather than a constructor: xUnit's <c>IClassFixture&lt;T&gt;</c>
    /// activator requires a fixture type to declare exactly one public
    /// constructor, so <see cref="NarrativeFixture()"/> has to be the only one.
    /// Artifact writing is disabled on this path regardless of
    /// <c>NARRATIVETRACE_OUTPUT</c>, since no environment is consulted — use it
    /// when a test must pin its own tracing level directly (not as a class
    /// fixture).
    /// </remarks>
    public static NarrativeFixture Create(NarrativeTraceConfig config)
    {
        return new NarrativeFixture(config, _ => null);
    }

    private NarrativeFixture(
        NarrativeTraceConfig config, Func<string, string?> readEnv)
    {
        _context = new SyncNarrativeContext(config);
        _output = TestArtifactSettings.Resolve(readEnv);
    }

    /// <summary>
    /// The context to instrument against. Pass it to the code under test.
    /// </summary>
    /// <remarks>
    /// Live for the fixture's lifetime and reset on <see cref="Dispose"/>.
    /// Prefer <see cref="Run(string, Action{INarrativeContext})"/>, which hands
    /// you the same context and additionally prints the narrative when the test
    /// fails.
    /// </remarks>
    public INarrativeContext Context => _context;

    /// <summary>
    /// Writes this test's captured trace to disk in the resolved format and
    /// directory. On by default; a no-op only when
    /// <c>NARRATIVETRACE_OUTPUT=false</c> opts out.
    /// </summary>
    /// <returns>
    /// The scenario's structural delta against its last green <c>.nt</c>
    /// artifact, when the Markdown path produced one; <see langword="null"/>
    /// otherwise (including when output is disabled).
    /// </returns>
    public ScenarioDelta? WriteArtifacts(
        string testClass, string testMethod, bool failed)
    {
        return WriteArtifacts(
            ArtifactIdentity.OfMethod(testClass, testMethod), testMethod, failed, Console.Out);
    }

    /// <summary>
    /// The same write, for one invocation of a test method that runs more
    /// than once (a <c>[Theory]</c> case) — pass the case's own 1-based
    /// index and a readable label so each invocation gets its own files
    /// instead of overwriting the previous one's.
    /// </summary>
    /// <remarks>
    /// xUnit exposes no invocation ordinal to a fixture the way a test
    /// framework with its own extension point can auto-detect one (see the
    /// NUnit integration) — the test body already has its own theory data in
    /// scope, so it is the natural place to supply this explicitly.
    /// </remarks>
    /// <param name="invocationIndex">1-based, in whatever order the theory's own data enumerates.</param>
    /// <param name="invocationLabel">A readable label for the case, e.g. the argument under test.</param>
    public ScenarioDelta? WriteArtifacts(
        string testClass, string testMethod, bool failed, int invocationIndex, string invocationLabel)
    {
        return WriteArtifacts(
            ArtifactIdentity.OfInvocation(testClass, testMethod, invocationIndex, invocationLabel),
            testMethod, failed, Console.Out);
    }

    internal ScenarioDelta? WriteArtifacts(
        string testClass, string testMethod, bool failed, TextWriter console)
    {
        return WriteArtifacts(ArtifactIdentity.OfMethod(testClass, testMethod), testMethod, failed, console);
    }

    /// <summary>
    /// Writes the full artifact set for one invocation, keyed by its
    /// <see cref="ArtifactIdentity"/>: verifies approval mode (when enabled)
    /// before the last-green write, so a rejected structure never advances
    /// the baseline, then rethrows the rejection so the caller's test fails
    /// with the diff.
    /// </summary>
    /// <exception cref="NarrativeApprovalException">
    /// Approval mode is on, <paramref name="failed"/> is <see langword="false"/>, and the traced
    /// structure has no approved trace yet or differs from one.
    /// </exception>
    internal ScenarioDelta? WriteArtifacts(
        ArtifactIdentity identity, string displayName, bool failed, TextWriter console)
    {
        if (!_output.Enabled)
        {
            return null;
        }

        var rejection = ApprovalRejection(identity, displayName, failed);
        var delta = TraceArtifactWriter.Write(
            _context.CaptureTrace(), identity, displayName, failed || rejection is not null,
            _output.Directory, _output.Format, Renderers, console,
            _output.EntryArtifacts, RunScope.Current?.Name);

        if (rejection is not null)
        {
            throw rejection;
        }

        return delta;
    }

    /// <summary>
    /// Approval mode runs only on a run not already failed — a red test
    /// already has the developer's attention, and its mid-flight structure
    /// must not churn the received traces.
    /// </summary>
    private NarrativeApprovalException? ApprovalRejection(
        ArtifactIdentity identity, string displayName, bool failed)
    {
        var trace = _context.CaptureTrace();
        if (failed || !_output.ApprovalEnabled || trace.IsEmpty)
        {
            return null;
        }

        try
        {
            NarrativeApproval.Verify(
                trace, identity.StructuralScenario(displayName),
                NarrativeApproval.ApprovedFile(_output.ApprovedDir, identity));
            return null;
        }
        catch (NarrativeApprovalException ex)
        {
            return ex;
        }
    }

    /// <summary>Captures the trace recorded so far, for asserting on it directly.</summary>
    /// <returns>
    /// The finished trace. A snapshot that does not clear state, so calling it
    /// twice returns the accumulated trace both times.
    /// </returns>
    public TraceTree CaptureTrace()
    {
        return _context.CaptureTrace();
    }

    /// <summary>
    /// Runs a test <paramref name="body"/> against the fixture context. If it
    /// throws, the captured narrative of what the code did is printed (framed by
    /// <paramref name="scenario"/>) before the exception is rethrown — so a
    /// failing test shows its story. xUnit does not hand fixtures the test
    /// outcome, so this explicit wrapper is how failure reporting is triggered.
    /// </summary>
    public void Run(string scenario, Action<INarrativeContext> body)
    {
        Run(scenario, body, Console.Out);
    }

    internal void Run(
        string scenario, Action<INarrativeContext> body, TextWriter output)
    {
        try
        {
            body(_context);
        }
        catch
        {
            var report = NarrativeFailureReport.Build(scenario, _context.CaptureTrace());
            if (report.Length > 0)
            {
                output.Write(report);
            }

            throw;
        }
        finally
        {
            PrintTemplateWarnings(_context.CaptureTrace(), output);
        }
    }

    private static void PrintTemplateWarnings(TraceTree tree, TextWriter output)
    {
        var block = TemplateWarningCollector.Format(
            TemplateWarningCollector.Collect(tree));
        if (block.Length > 0)
        {
            output.Write(block);
        }
    }

    /// <summary>Resets the context so the fixture can be reused with a clean trace.</summary>
    /// <remarks>
    /// Called by xUnit at the end of the fixture's lifetime. It discards the
    /// captured trace — capture anything you still need <i>before</i> disposal.
    /// </remarks>
    public void Dispose()
    {
        _context.Reset();
    }
}
