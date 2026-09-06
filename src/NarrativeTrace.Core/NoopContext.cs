// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The disabled <see cref="INarrativeContext"/>: honours the whole protocol and
/// captures nothing.
/// </summary>
/// <remarks>
/// <para>
/// The null-object that lets instrumentation stay unconditional. Call sites can
/// follow the enter/exit protocol without null checks or an
/// <see cref="INarrativeContext.IsActive"/> branch, and pay only a virtual call
/// per method when tracing is off. Use it as the default wherever a context is
/// optional, in tests that must not accumulate spans, and as the fallback when
/// configuration resolves to <see cref="TracingLevel.Off"/>.
/// </para>
/// <para>
/// <b>Shared singleton.</b> There is one instance per process
/// (<see cref="Instance"/>) and the constructor is private, so it holds no
/// per-trace state to isolate — which is also why it is safe to share across
/// threads without synchronization.
/// </para>
/// <para>
/// <b>Two deliberate asymmetries</b> worth knowing before asserting on it:
/// <see cref="EnsureTraceId"/> does mint and retain a real id (see its remarks),
/// and <see cref="CaptureTrace"/> returns an empty tree rather than
/// <see langword="null"/>.
/// </para>
/// </remarks>
public sealed class NoopContext
    : INarrativeContext, IConcurrentChildFactory
{
    /// <summary>
    /// The shared instance. Thread-safe and stateless apart from the id cached
    /// by <see cref="EnsureTraceId"/>; there is no reason to want another.
    /// </summary>
    public static readonly INarrativeContext Instance = new NoopContext();

    private NoopContext() { }

    /// <inheritdoc/>
    /// <remarks>Always <see langword="false"/> — this context never captures.</remarks>
    public bool IsActive => false;

    /// <inheritdoc/>
    /// <remarks>Always <see langword="false"/>, so callers skip rendering parameter values entirely.</remarks>
    public bool CapturesParameterValues => false;

    private readonly object _traceIdLock = new();
    private TraceId? _traceId;

    /// <inheritdoc/>
    /// <remarks>
    /// Always <see langword="null"/>, even after <see cref="EnsureTraceId"/> has
    /// minted an id — this context reports no trace in progress because it never
    /// opens a span. Do not use it to test whether <see cref="EnsureTraceId"/>
    /// has been called.
    /// </remarks>
    public TraceId? CurrentTraceId => null;

    /// <inheritdoc/>
    /// <remarks>
    /// Returns a genuine, valid trace id even though nothing is captured, so a
    /// request boundary can still stamp a correlation id into logs when tracing
    /// is off. Because this type is a process-wide singleton, the id is minted
    /// once and then returned for the lifetime of the process: it is <b>not</b>
    /// per-request, and <see cref="Reset"/> does not clear it. Treat it as a
    /// placeholder that keeps log fields populated, never as a correlation key.
    /// </remarks>
    public TraceId EnsureTraceId()
    {
        lock (_traceIdLock)
        {
            _traceId ??= SpanIdGenerator.GenerateTraceId();
            return _traceId.Value;
        }
    }

    /// <inheritdoc/>
    /// <remarks>Always <see langword="null"/>; no root span is ever entered.</remarks>
    public string? StoryId => null;

    /// <inheritdoc/>
    /// <remarks>Always <see langword="null"/>; no root span is ever entered.</remarks>
    public string? ChapterId => null;

    /// <inheritdoc/>
    /// <remarks>
    /// Records nothing and returns <see cref="SpanId.Empty"/>. Still pair it
    /// with an exit: the protocol is meant to be followed unconditionally.
    /// </remarks>
    public SpanId EnterMethod(
        string className,
        string methodName,
        IReadOnlyList<ParameterCapture> parameters,
        MethodOptions? options = null)
    {
        return SpanId.Empty;
    }

    /// <inheritdoc/>
    /// <remarks>No-op.</remarks>
    public void ExitMethodWithReturn(
        string? renderedValue, SpanId? handle = null)
    { }

    /// <inheritdoc/>
    /// <remarks>No-op.</remarks>
    public void ExitMethodWithReturn(
        string? renderedValue,
        RenderedValue? structuredValue,
        SpanId? handle = null)
    { }

    /// <inheritdoc/>
    /// <remarks>No-op; the exception is neither recorded nor rethrown.</remarks>
    public void ExitMethodWithException(
        Exception? exception, SpanId? handle = null)
    { }

    /// <inheritdoc/>
    /// <remarks>No-op; the exception is neither recorded nor rethrown.</remarks>
    public void ExitMethodWithException(
        Exception? exception, string? errorContext,
        SpanId? handle = null)
    { }

    /// <inheritdoc/>
    /// <remarks>No-op — there is no active stack to detach from.</remarks>
    public void DetachFrame(SpanId handle) { }

    /// <inheritdoc/>
    /// <remarks>
    /// Always a new empty tree, never <see langword="null"/>, so callers can
    /// walk the result without a special case for disabled tracing.
    /// </remarks>
    public TraceTree CaptureTrace()
    {
        return new TraceTree(Array.Empty<TraceNode>());
    }

    /// <inheritdoc/>
    /// <remarks>
    /// No-op. Notably it does <b>not</b> clear the id cached by
    /// <see cref="EnsureTraceId"/>.
    /// </remarks>
    public void Reset() { }

    /// <inheritdoc/>
    /// <remarks>Returns a shared no-op snapshot whose scope does nothing on dispose.</remarks>
    public IContextSnapshot Snapshot() => NoopSnapshot.Shared;

    /// <inheritdoc/>
    /// <remarks>Always <see langword="null"/>; no lineage is recorded.</remarks>
    public SpanId? ParentOf(SpanId handle) => null;

    /// <inheritdoc/>
    /// <remarks>
    /// Invokes <paramref name="fn"/> directly with no scoping. Its return value
    /// and any exception propagate unchanged.
    /// </remarks>
    public T RunScoped<T>(SpanId handle, Func<T> fn) => fn();

    /// <inheritdoc/>
    /// <remarks>No-op — the node is discarded rather than grafted.</remarks>
    public void GraftChild(TraceNode node) { }

    /// <summary>
    /// Returns <see cref="Instance"/> — a disabled context forks into itself.
    /// </summary>
    /// <returns>The shared singleton, never a new object.</returns>
    public INarrativeContext CreateConcurrentChild() => Instance;

    /// <inheritdoc/>
    /// <remarks>No-op; the values are discarded.</remarks>
    public void SetRequestContext(
        string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp)
    { }

    /// <inheritdoc/>
    /// <remarks>No-op; the values are discarded.</remarks>
    public void SetUserContext(
        EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId)
    { }

    private sealed class NoopSnapshot : IContextSnapshot
    {
        public static readonly IContextSnapshot Shared = new NoopSnapshot();

        public IContextScope Activate() => new NoopScope();

        public IContextScope ActivateWithoutAdoption() => new NoopScope();
    }

    private sealed class NoopScope : IContextScope
    {
        public void Dispose() { }
    }
}
