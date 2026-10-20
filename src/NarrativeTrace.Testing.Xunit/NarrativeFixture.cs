// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Runtime.CompilerServices;
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.TestingXunit;

/// <summary>
/// Per-test xUnit fixture: owns a narrative context, prints the story when a
/// test fails, and writes each test's trace artifacts to disk when it ends.
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
/// Artifact writing is <b>on by default</b>: a test that runs its body
/// through <see cref="Run(string, Action{INarrativeContext}, string)"/> gets
/// its narrative files (<c>.md</c>, <c>.nt</c> and companions) in the
/// ephemeral, gitignored <see cref="TestArtifactSettings.DefaultDirectory"/>
/// when the body ends, with no call in the test, unless
/// <c>NARRATIVETRACE_OUTPUT=false</c> opts out. A test that drives
/// <see cref="Context"/> directly, without <c>Run</c>, writes them with
/// <see cref="WriteArtifacts(string, string, bool)"/>.
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
    private ArtifactIdentity? _currentTest;
    private LastWrite? _lastWrite;

    /// <summary>
    /// What the last write covered: a later write for the same test over the
    /// same root calls would produce the same files, so it is skipped.
    /// </summary>
    private sealed record LastWrite(ArtifactIdentity Identity, int Roots, ScenarioDelta? Delta);

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
            ConfigFrom(ConfigResolver.Resolve(readEnv)),
            readEnv)
    {
    }

    private static NarrativeTraceConfig ConfigFrom(ResolvedConfig resolved) =>
        new(resolved.Level, resolved.Identity);

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

        var tree = _context.CaptureTrace();
        if (_lastWrite is { } last && last.Identity == identity && last.Roots == tree.Roots.Count)
        {
            return last.Delta;
        }

        var rejection = ApprovalRejection(identity, displayName, failed);
        var delta = TraceArtifactWriter.Write(
            tree, identity, displayName, failed || rejection is not null,
            _output.Directory, _output.Format, Renderers, console,
            _output.EntryArtifacts, RunScope.Current?.Name);
        _lastWrite = new LastWrite(identity, tree.Roots.Count, delta);

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
                NarrativeApproval.ApprovedFile(
                    _output.ApprovedDirectoryFrom(Directory.GetCurrentDirectory()), identity));
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
    /// Runs a test <paramref name="body"/> against the fixture context and,
    /// when it ends, writes that test's narrative files. If the body throws,
    /// the captured narrative of what the code did is printed (framed by
    /// <paramref name="scenario"/>) before the exception is rethrown — so a
    /// failing test shows its story.
    /// </summary>
    /// <param name="scenario">The title of the failure narrative.</param>
    /// <param name="body">The test's act and assert steps, run against <see cref="Context"/>.</param>
    /// <param name="testMethod">
    /// The test method the files are named after. Supplied by the compiler —
    /// leave it out. Pass it only when <c>Run</c> is called from a helper
    /// rather than from the test method itself, which the compiler would
    /// otherwise name instead.
    /// </param>
    /// <remarks>
    /// <para>
    /// With output on (the default; <c>NARRATIVETRACE_OUTPUT=false</c> opts
    /// out) the files are the same ones
    /// <see cref="WriteArtifacts(string, string, bool)"/> writes, under
    /// <see cref="TestArtifactSettings.DefaultDirectory"/>: the test class is
    /// the type that declares <paramref name="body"/>, the method is
    /// <paramref name="testMethod"/>, and the test counts as failed when the
    /// body throws. xUnit hands a fixture neither the test nor its outcome,
    /// so an assertion placed <i>after</i> <c>Run</c> is not seen — keep the
    /// assertions inside the body.
    /// </para>
    /// <para>
    /// An explicit <c>WriteArtifacts</c> call for the same test, inside the
    /// body or after <c>Run</c>, writes nothing more unless the test recorded
    /// further calls in between. A <c>Run</c> from a different test method
    /// than the previous one starts a fresh trace, so a class fixture's
    /// files carry one test each.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="testMethod"/> is empty or white space.</exception>
    /// <exception cref="NarrativeApprovalException">
    /// Approval mode is on, the body passed, and the traced structure has no
    /// approved trace yet or differs from one.
    /// </exception>
    public void Run(
        string scenario, Action<INarrativeContext> body, [CallerMemberName] string testMethod = "")
    {
        Run(scenario, body, Console.Out, testMethod);
    }

    internal void Run(
        string scenario, Action<INarrativeContext> body, TextWriter output,
        [CallerMemberName] string testMethod = "")
    {
        var identity = BeginTest(body, testMethod);
        var failed = true;
        try
        {
            body(_context);
            failed = false;
        }
        catch
        {
            PrintFailure(scenario, output);
            throw;
        }
        finally
        {
            PrintTemplateWarnings(_context.CaptureTrace(), output);
            WriteWhenTestEnds(identity, testMethod, failed, output);
        }
    }

    /// <summary>
    /// Names the test a <c>Run</c> belongs to, and starts a fresh trace when
    /// it is not the test the previous <c>Run</c> belonged to.
    /// </summary>
    private ArtifactIdentity BeginTest(Action<INarrativeContext> body, string testMethod)
    {
        if (string.IsNullOrWhiteSpace(testMethod))
        {
            throw new ArgumentException("A test method name is required.", nameof(testMethod));
        }

        var identity = ArtifactIdentity.OfMethod(TestClassOf(body), testMethod);
        if (_currentTest is not null && _currentTest != identity)
        {
            _context.Reset();
            _lastWrite = null;
        }

        _currentTest = identity;
        return identity;
    }

    /// <summary>
    /// The type that declares the body, unwrapped past the compiler's
    /// closure classes (<c>&lt;&gt;c</c>, <c>&lt;&gt;c__DisplayClass…</c>) to the test class.
    /// </summary>
    private static string TestClassOf(Action<INarrativeContext> body)
    {
        var type = body.Method.DeclaringType;
        while (type is not null && type.Name.StartsWith("<", StringComparison.Ordinal))
        {
            type = type.DeclaringType;
        }

        return type?.Name ?? string.Empty;
    }

    private void PrintFailure(string scenario, TextWriter output)
    {
        var report = NarrativeFailureReport.Build(scenario, _context.CaptureTrace());
        if (report.Length > 0)
        {
            output.Write(report);
        }
    }

    /// <summary>
    /// The automatic write. A file system that refuses it is reported, not
    /// thrown: it must not replace the assertion that failed the test.
    /// </summary>
    private void WriteWhenTestEnds(
        ArtifactIdentity identity, string testMethod, bool failed, TextWriter output)
    {
        try
        {
            WriteArtifacts(identity, testMethod, failed, output);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            output.WriteLine("Failed to write trace file: " + e.Message);
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
        _currentTest = null;
        _lastWrite = null;
    }
}
