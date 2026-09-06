// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
namespace NarrativeTrace.Runtime;

/// <summary>
/// A context that gives each <see cref="Run(Action)"/> scope its own isolated
/// trace, so concurrently running tests or requests never share a span stack.
/// </summary>
/// <remarks>
/// <para>
/// Wraps a fresh <see cref="SyncNarrativeContext"/> per scope, held in an
/// <see cref="AsyncLocal{T}"/>. That is what makes a single shared context
/// instance safe for a parallel test suite: two scopes running at once capture
/// into separate inner contexts and cannot interleave each other's spans.
/// Use <see cref="SyncNarrativeContext"/> directly when there is exactly one
/// logical trace, and this when there are many.
/// </para>
/// <para>
/// <b>The scope is mandatory.</b> Every capture member — <c>EnterMethod</c>, the
/// exits, <see cref="Snapshot"/>, <see cref="GraftChild"/>,
/// <see cref="EnsureTraceId"/> and the rest — throws
/// <see cref="InvalidOperationException"/> when called outside a
/// <see cref="Run(Action)"/> or <see cref="RunAsync(Func{Task})"/>. The
/// exceptions to that rule are the read-only members
/// (<see cref="CaptureTrace"/>, <see cref="CurrentTraceId"/>,
/// <see cref="StoryId"/>, <see cref="ChapterId"/>, <see cref="IsActive"/>,
/// <see cref="CapturesParameterValues"/>) and <see cref="Reset"/>, which fall
/// back to the most recently completed scope so a test can read its trace after
/// the scope closes.
/// </para>
/// <para>
/// <b>The fallback is flow-scoped first, and silent when it cannot be.</b>
/// A completed scope is remembered on the flow that ran it, so two threads each
/// read back their own trace. A flow that <c>await</c>ed
/// <see cref="RunAsync(Func{Task})"/> cannot be handed anything that way — the
/// runtime restores the awaiting flow's state on resumption — so a second,
/// shared slot serves it, and that slot is filled <i>only</i> by a scope that
/// was alone for its whole lifetime. Under concurrent scopes it stays empty and
/// a later read returns an empty tree rather than a concurrent scope's trace:
/// an absent narrative is honest, another request's is not. Read
/// <see cref="CaptureTrace"/> from <i>inside</i> the scope whenever the result
/// matters.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var context = new AsyncNarrativeContext(config);
/// await context.RunAsync(async () =>
/// {
///     await service.PlaceOrderAsync(id);   // captured in this scope alone
/// });
/// var trace = context.CaptureTrace();      // the scope that just finished
/// </code>
/// </example>
public sealed class AsyncNarrativeContext
    : INarrativeContext, IConcurrentChildFactory, ITraceLossSource
{
    private readonly NarrativeTraceConfig _config;

    private readonly AsyncLocal<SyncNarrativeContext?>
        _current = new();

    /// <summary>
    /// The scope this flow most recently finished. Set inside the scope, so it
    /// reaches a synchronous caller of <see cref="Run(Action)"/> and stays
    /// invisible to every other flow.
    /// </summary>
    private readonly AsyncLocal<SyncNarrativeContext?>
        _lastCompletedInFlow = new();

    /// <summary>
    /// The last scope that was alone for its whole lifetime — the only case in
    /// which a shared slot can answer a cross-flow read without guessing.
    /// <see langword="null"/> whenever scopes overlapped.
    /// </summary>
    private SyncNarrativeContext? _lastCompleted;

    private int _openScopes;
    private int _scopeStarts;

    /// <summary>Creates a context that will open one isolated inner trace per scope.</summary>
    /// <param name="config">
    /// Shared with every inner scope, so changing its
    /// <see cref="NarrativeTraceConfig.Level"/> retunes scopes opened afterwards.
    /// </param>
    public AsyncNarrativeContext(
        NarrativeTraceConfig config)
    {
        _config = config;
    }

    /// <inheritdoc/>
    /// <remarks>Reads the shared configuration, so it is valid outside a scope.</remarks>
    public bool IsActive => _config.Level.IsActive();

    /// <inheritdoc/>
    /// <remarks>Reads the shared configuration, so it is valid outside a scope.</remarks>
    public bool CapturesParameterValues =>
        _config.Level.IsEnabled(TracingLevel.Detail);

    /// <inheritdoc/>
    /// <remarks>
    /// The active scope's trace id, falling back to the last completed scope's
    /// when called outside one — subject to the shared-fallback caveat on the
    /// type. Does not throw outside a scope.
    /// </remarks>
    public TraceId? CurrentTraceId => Readable()?.CurrentTraceId;

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a scope. Use <see cref="CurrentTraceId"/> to read without one.</exception>
    public TraceId EnsureTraceId()
    {
        return RequireCurrent().EnsureTraceId();
    }

    /// <summary>
    /// Creates an isolated child context for a concurrent branch of the active
    /// scope, seeded with its trace lineage.
    /// </summary>
    /// <returns>A child context whose captured roots graft back under the current span.</returns>
    /// <exception cref="InvalidOperationException">Called outside a scope.</exception>
    public INarrativeContext CreateConcurrentChild()
    {
        return RequireCurrent().CreateConcurrentChild();
    }

    /// <inheritdoc/>
    /// <remarks>Falls back to the last completed scope outside one; does not throw.</remarks>
    public string? StoryId => Readable()?.StoryId;

    /// <inheritdoc/>
    /// <remarks>Falls back to the last completed scope outside one; does not throw.</remarks>
    public string? ChapterId => Readable()?.ChapterId;

    /// <summary>Runs synchronous work inside a fresh, isolated trace scope.</summary>
    /// <param name="fn">The work to trace. Invoked once, synchronously.</param>
    /// <remarks>
    /// The scope closes when <paramref name="fn"/> returns, including when it
    /// throws — the exception propagates unchanged and the trace is still
    /// readable afterwards via <see cref="CaptureTrace"/>. Do not use this
    /// overload for async work: handing it a lambda that returns a
    /// <see cref="Task"/> closes the scope at the first incomplete await, and
    /// the continuation then captures nothing. Use
    /// <see cref="RunAsync(Func{Task})"/> instead.
    /// </remarks>
    public void Run(Action fn)
    {
        var scope = OpenScope();
        try
        {
            fn();
        }
        finally
        {
            CloseScope(scope);
        }
    }

    /// <summary>Runs synchronous work inside a fresh, isolated trace scope and returns its result.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="fn">The work to trace. Invoked once, synchronously.</param>
    /// <returns>Whatever <paramref name="fn"/> returns.</returns>
    /// <remarks>
    /// As with <see cref="Run(Action)"/>, a <typeparamref name="T"/> of
    /// <see cref="Task"/> is a mistake — the scope would close before the task
    /// completes. Use <see cref="RunAsync{T}(Func{Task{T}})"/>.
    /// </remarks>
    public T Run<T>(Func<T> fn)
    {
        var scope = OpenScope();
        try
        {
            return fn();
        }
        finally
        {
            CloseScope(scope);
        }
    }

    /// <summary>Runs asynchronous work inside a fresh, isolated trace scope.</summary>
    /// <param name="fn">The work to trace. Awaited to completion before the scope closes.</param>
    /// <returns>A task completing when the work and its scope have both finished.</returns>
    /// <remarks>
    /// The scope spans the whole async operation, awaits included, because it
    /// lives in an <see cref="AsyncLocal{T}"/> that flows with the continuation.
    /// Await the returned task — abandoning it leaves the scope open. Exceptions
    /// propagate unchanged and the scope still closes.
    /// </remarks>
    public async Task RunAsync(Func<Task> fn)
    {
        var scope = OpenScope();
        try
        {
            await fn().ConfigureAwait(false);
        }
        finally
        {
            CloseScope(scope);
        }
    }

    /// <summary>Runs asynchronous work inside a fresh, isolated trace scope and returns its result.</summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="fn">The work to trace. Awaited to completion before the scope closes.</param>
    /// <returns>A task producing whatever <paramref name="fn"/> produced.</returns>
    public async Task<T> RunAsync<T>(Func<Task<T>> fn)
    {
        var scope = OpenScope();
        try
        {
            return await fn().ConfigureAwait(false);
        }
        finally
        {
            CloseScope(scope);
        }
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public SpanId EnterMethod(
        string className,
        string methodName,
        IReadOnlyList<ParameterCapture> parameters,
        MethodOptions? options = null)
    {
        return RequireCurrent().EnterMethod(
            className, methodName, parameters, options);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void ExitMethodWithReturn(
        string? renderedValue, SpanId? handle = null)
    {
        RequireCurrent().ExitMethodWithReturn(
            renderedValue, handle);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void ExitMethodWithReturn(
        string? renderedValue,
        RenderedValue? structuredValue,
        SpanId? handle = null)
    {
        RequireCurrent().ExitMethodWithReturn(
            renderedValue, structuredValue, handle);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void ExitMethodWithException(
        Exception? exception, SpanId? handle = null)
    {
        RequireCurrent().ExitMethodWithException(
            exception, handle);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void ExitMethodWithException(
        Exception? exception, string? errorContext,
        SpanId? handle = null)
    {
        RequireCurrent().ExitMethodWithException(
            exception, errorContext, handle);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void DetachFrame(SpanId handle)
    {
        RequireCurrent().DetachFrame(handle);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public IContextSnapshot Snapshot()
    {
        return RequireCurrent().Snapshot();
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public SpanId? ParentOf(SpanId handle)
    {
        return RequireCurrent().ParentOf(handle);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public T RunScoped<T>(SpanId handle, Func<T> fn)
    {
        return RequireCurrent().RunScoped(handle, fn);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void GraftChild(TraceNode node)
    {
        RequireCurrent().GraftChild(node);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void SetRequestContext(
        string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp)
    {
        RequireCurrent().SetRequestContext(
            httpMethod, httpRoute, clientIp);
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Called outside a <see cref="Run(Action)"/> / <see cref="RunAsync(Func{Task})"/> scope.</exception>
    public void SetUserContext(
        EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId)
    {
        RequireCurrent().SetUserContext(
            enduserId, sessionId, tenantId);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Reads the active scope, else the most recently completed one, else
    /// returns an empty tree. Never throws for want of a scope — but see the
    /// type's remarks: the completed-scope fallback is shared, so under
    /// concurrent scopes this can return a different scope's trace.
    /// </remarks>
    public TraceTree CaptureTrace()
    {
        return Readable() is { } context
            ? context.CaptureTrace()
            : new TraceTree([]);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The readable scope's loss, or <see cref="TraceLoss.None"/> when there is
    /// no scope to read — an unrun context has lost nothing.
    /// </remarks>
    public TraceLoss TraceLoss =>
        (Readable() as ITraceLossSource)?.TraceLoss ?? TraceLoss.None;

    /// <inheritdoc/>
    /// <remarks>
    /// Resets the active scope if there is one, and always clears the completed-scope
    /// fallback so a later <see cref="CaptureTrace"/> returns an empty tree.
    /// Does not throw outside a scope.
    /// </remarks>
    public void Reset()
    {
        _current.Value?.Reset();
        _lastCompletedInFlow.Value = null;
        _lastCompleted = null;
    }

    private SyncNarrativeContext RequireCurrent()
    {
        return _current.Value
            ?? throw new InvalidOperationException(
                "No active Run() scope.");
    }

    /// <summary>
    /// The scope a read outside <c>Run</c> should answer from: the active one,
    /// else the one this flow finished, else the last uncontended one — and
    /// <see langword="null"/> rather than a guess when scopes overlapped.
    /// </summary>
    private SyncNarrativeContext? Readable() =>
        _current.Value ?? _lastCompletedInFlow.Value ?? _lastCompleted;

    /// <summary>
    /// Opens a fresh inner scope, recording the generation it started in so
    /// close can tell whether it was ever alone.
    /// </summary>
    private Scope OpenScope()
    {
        var inner = new SyncNarrativeContext(_config);
        var generation = Interlocked.Increment(ref _scopeStarts);
        var aloneAtStart = Interlocked.Increment(ref _openScopes) == 1;
        _current.Value = inner;
        return new Scope(inner, generation, aloneAtStart);
    }

    /// <summary>
    /// Closes a scope, remembering it on this flow always and in the shared
    /// slot only when no other scope opened or was open during its lifetime.
    /// A contended slot is left empty: an empty tree is honest, a concurrent
    /// scope's trace is not.
    /// </summary>
    private void CloseScope(Scope scope)
    {
        _lastCompletedInFlow.Value = scope.Inner;
        // Alone for the whole lifetime: nothing was open when it started,
        // nothing is open now, and nothing opened in between. All three are
        // needed — a scope that started second sees an empty count at close,
        // and a scope that started first sees an unchanged generation.
        var closedEmpty = Interlocked.Decrement(ref _openScopes) == 0;
        var aloneThroughout = scope.AloneAtStart
            && closedEmpty
            && Volatile.Read(ref _scopeStarts) == scope.Generation;
        _lastCompleted = aloneThroughout ? scope.Inner : null;
        _current.Value = null;
    }

    private readonly record struct Scope(
        SyncNarrativeContext Inner, int Generation, bool AloneAtStart);
}
