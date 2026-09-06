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
/// or construct one per test. It holds a single
/// <see cref="SyncNarrativeContext"/>, so <b>one fixture serves one test at a
/// time</b> — sharing an instance across tests that xUnit runs in parallel
/// merges their spans into one trace. A class fixture is safe because xUnit does
/// not parallelize within a class.
/// </para>
/// <para>
/// Artifact writing is opt-in through <c>NARRATIVETRACE_OUTPUT</c>; with it
/// unset, <see cref="WriteArtifacts(string, string, bool)"/> does nothing, so
/// the fixture is inert in normal test runs.
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
        MermaidSequenceRenderer.Render,
        PlantUmlSequenceRenderer.Render,
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
    /// Artifact writing is disabled on this path regardless of
    /// <c>NARRATIVETRACE_OUTPUT</c>, since no environment is consulted — use it
    /// when a test must pin its own tracing level.
    /// </remarks>
    public NarrativeFixture(NarrativeTraceConfig config)
        : this(config, _ => null)
    {
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
    /// When <c>NARRATIVETRACE_OUTPUT</c> is enabled, writes this test's captured
    /// trace to disk in the resolved format and directory (a no-op otherwise).
    /// </summary>
    public void WriteArtifacts(
        string testClass, string testMethod, bool failed)
    {
        WriteArtifacts(testClass, testMethod, failed, Console.Out);
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
