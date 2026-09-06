// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Diagnostics;

namespace NarrativeTrace.Runtime;

/// <summary>
/// The standard capturing context: one span stack, one trace, safe to share
/// across threads.
/// </summary>
/// <remarks>
/// <para>
/// The workhorse implementation of <see cref="INarrativeContext"/>. Use it
/// wherever there is a single logical trace — a console application, a worker,
/// a single-threaded test. When several traces must run at once without mixing,
/// use <see cref="AsyncNarrativeContext"/>, which is a thin per-scope wrapper
/// around this type rather than an alternative to it.
/// </para>
/// <para>
/// <b>Thread-safe but not trace-isolating.</b> All mutable state is guarded by a
/// lock, so concurrent calls are safe; what they are not is separate. Two
/// unrelated operations sharing one instance land in a single trace, and their
/// spans interleave. Sharing is correct for branches of one trace — via
/// <see cref="Snapshot"/> or <see cref="CreateConcurrentChild"/> — and wrong as
/// a way of tracing independent work.
/// </para>
/// <para>
/// <b>Snapshots are the cross-thread seam.</b>
/// <see cref="Snapshot"/> hands a worker the trace lineage, and the work traced
/// under it comes back: the capture the snapshot was taken from reports the
/// worker's spans from the moment they are published — transitively, so a chain
/// of async hops reaches the flow that captures. The activated capture is held
/// in an <see cref="AsyncLocal{T}"/>, so a worker never disturbs the origin's
/// own stack.
/// </para>
/// <para>
/// State accumulates until <see cref="Reset"/>: the instance is reusable, but
/// only after an explicit reset. Also implements
/// <see cref="IConcurrencyLifecycle"/>, so the fork helpers can report group
/// boundaries to it, and <see cref="ITraceLossSource"/>, so a reporter can name
/// what a run lost.
/// </para>
/// </remarks>
public sealed class SyncNarrativeContext
    : INarrativeContext, IConcurrentChildFactory, IConcurrencyLifecycle,
      ITraceLossSource
{
    private readonly NarrativeTraceConfig _config;
    private readonly IEventPipeline? _sink;
    private readonly object _lock = new();
    private readonly AsyncLocal<SpanId?> _scopedParent = new();
    private readonly AsyncLocal<State?> _activated = new();
    private readonly int _adoptionCeiling;
    private State _state;

    /// <summary>Creates a context that captures into memory only.</summary>
    /// <param name="config">
    /// The level and service identity to capture under. Held by reference, so a
    /// later change to <see cref="NarrativeTraceConfig.Level"/> takes effect here.
    /// </param>
    public SyncNarrativeContext(NarrativeTraceConfig config)
        : this(config, sink: null)
    {
    }

    /// <summary>
    /// Creates a context whose capture events are also fanned out to an
    /// optional real-time <paramref name="sink"/> (e.g. a
    /// <see cref="DualPathPipeline"/> or <see cref="BufferedEventConsumer"/>)
    /// for streaming and aggregate queries. Per-fork capture isolation is
    /// unaffected — the sink is shared across forks by design.
    /// </summary>
    public SyncNarrativeContext(
        NarrativeTraceConfig config, IEventPipeline? sink)
        : this(config, sink, AdoptionLedger.DefaultCeiling)
    {
    }

    /// <summary>
    /// Test seam: the adoption ceiling is a boundary condition, reachable in a
    /// unit test only by lowering it.
    /// </summary>
    internal SyncNarrativeContext(
        NarrativeTraceConfig config,
        IEventPipeline? sink,
        int adoptionCeiling)
    {
        _config = config;
        _sink = sink;
        _adoptionCeiling = adoptionCeiling;
        _state = NewState();
    }

    /// <summary>
    /// The capture this flow is writing to: the one a snapshot activation
    /// installed, else this context's own.
    /// </summary>
    /// <remarks>
    /// The activated capture lives in an <see cref="AsyncLocal{T}"/>, so a
    /// worker's activation is invisible to every other flow using the same
    /// context — which is what makes concurrent propagation safe here, and what
    /// a shared field could never be.
    /// </remarks>
    private State Current => _activated.Value ?? _state;

    private State NewState() => new(_adoptionCeiling);

    /// <inheritdoc/>
    public bool IsActive => _config.Level.IsActive();

    /// <inheritdoc/>
    /// <remarks>
    /// True only at <see cref="TracingLevel.Detail"/>. Below it,
    /// <see cref="EnterMethod"/> blanks every rendered parameter value before
    /// storing it, so checking this first only saves the rendering cost — it is
    /// not what enforces the suppression.
    /// </remarks>
    public bool CapturesParameterValues =>
        _config.Level.IsEnabled(TracingLevel.Detail);

    /// <inheritdoc/>
    public TraceId? CurrentTraceId
    {
        get
        {
            lock (_lock)
            {
                return Current.TraceId;
            }
        }
    }

    /// <inheritdoc/>
    public string? StoryId
    {
        get
        {
            lock (_lock)
            {
                return Current.StoryId;
            }
        }
    }

    /// <summary>
    /// Returns the current trace id, generating and retaining one if the
    /// trace has not entered a span yet. Lets a request boundary stamp a
    /// stable correlation id into log scope before the first
    /// <see cref="EnterMethod"/> — the first span then adopts it.
    /// </summary>
    public TraceId EnsureTraceId()
    {
        lock (_lock)
        {
            var state = Current;
            state.TraceId ??= SpanIdGenerator.GenerateTraceId();
            return state.TraceId.Value;
        }
    }

    /// <inheritdoc/>
    public string? ChapterId
    {
        get
        {
            lock (_lock)
            {
                return Current.ChapterId;
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see cref="SpanId.Empty"/> without recording anything when the
    /// level is <see cref="TracingLevel.Off"/>. Below
    /// <see cref="TracingLevel.Detail"/> the parameters are stored with their
    /// rendered values blanked.
    /// </remarks>
    public SpanId EnterMethod(
        string className,
        string methodName,
        IReadOnlyList<ParameterCapture> parameters,
        MethodOptions? options = null)
    {
        if (_config.Level == TracingLevel.Off)
        {
            return SpanId.Empty;
        }

        var effectiveParameters = CapturesParameterValues
            ? parameters
            : SuppressParameterValues(parameters);
        var signature = new MethodSignature(
            className, methodName, effectiveParameters,
            options?.Narration, options?.ErrorContext,
            options?.Namespace, options?.ReturnType, options?.NarrationTemplate);
        lock (_lock)
        {
            return AppendEnter(Current, signature);
        }
    }

    private static IReadOnlyList<ParameterCapture>
        SuppressParameterValues(
            IReadOnlyList<ParameterCapture> parameters)
    {
        var suppressed = new ParameterCapture[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            suppressed[i] = parameters[i] with { RenderedValue = "" };
        }

        return suppressed;
    }

    private SpanId AppendEnter(State state, MethodSignature signature)
    {
        state.TraceId ??= SpanIdGenerator.GenerateTraceId();
        var parentSpanId = ResolveParent();
        DeriveStoryIdIfAbsent(state, parentSpanId, signature);
        var spanId = SpanIdGenerator.GenerateSpanId();
        var spanContext = BuildSpanContext(
            state, spanId, parentSpanId, signature);
        Emit(state, new EnterEvent(
            spanContext, Stopwatch.GetTimestamp(), signature,
            AsyncTagFor(state, signature)));
        state.SpanContextMap[spanId] = spanContext;
        state.ParentMap[spanId] = parentSpanId;
        state.ActiveStack.Add(spanId);
        return spanId;
    }

    /// <summary>
    /// Tags the first span a flow opens under a propagated snapshot as
    /// <see cref="ConcurrencyKind.Async"/> — and only that span, since
    /// everything below it is ordinary sequential work on the same flow.
    /// </summary>
    /// <remarks>
    /// This is what makes adopted work render the way fork members always have:
    /// under a marker, members sorted, order not asserted. The scheduler
    /// decides which task starts first, so capture order is not behaviour and a
    /// structural baseline must not pin it. The group is keyed by the launching
    /// span so every async child of one call is one group; work propagated with
    /// no parent span groups per trace instead.
    /// </remarks>
    private static ConcurrencyInfo? AsyncTagFor(
        State state, MethodSignature signature)
    {
        if (!state.FromSnapshot || state.ActiveStack.Count > 0)
        {
            return null;
        }

        var key = state.SnapshotParentSpanId?.ToString()
            ?? state.TraceId?.ToString() ?? "root";
        var thread = Thread.CurrentThread;
        return new ConcurrencyInfo(
            "async-" + key,
            $"{signature.ClassName}.{signature.MethodName}",
            thread.ManagedThreadId,
            thread.Name,
            thread.IsThreadPoolThread,
            ConcurrencyKind.Async);
    }

    private SpanContext BuildSpanContext(
        State state, SpanId spanId, SpanId? parentSpanId,
        MethodSignature signature)
    {
        return SpanContext.Create(
            state.TraceId!.Value, spanId, parentSpanId,
            _config.ServiceIdentity) with
        {
            SpanName =
                $"{signature.ClassName}.{signature.MethodName}",
            StoryId = state.StoryId,
            ChapterId = state.ChapterId,
            HttpMethod = state.HttpMethod,
            HttpRoute = state.HttpRoute,
            ClientIp = state.ClientIp,
            EnduserId = state.EnduserId,
            SessionId = state.SessionId,
            TenantId = state.TenantId,
        };
    }

    /// <inheritdoc/>
    public void DetachFrame(SpanId handle)
    {
        lock (_lock)
        {
            Current.ActiveStack.Remove(handle);
        }
    }

    /// <inheritdoc/>
    public void ExitMethodWithReturn(
        string? renderedValue, SpanId? handle = null)
    {
        ExitWith(new Returned(renderedValue), handle);
    }

    /// <inheritdoc/>
    public void ExitMethodWithReturn(
        string? renderedValue,
        RenderedValue? structuredValue,
        SpanId? handle = null)
    {
        ExitWith(
            new Returned(renderedValue, structuredValue), handle);
    }

    /// <inheritdoc/>
    public void ExitMethodWithException(
        Exception? exception, SpanId? handle = null)
    {
        ExitWith(new Threw(exception), handle);
    }

    /// <inheritdoc/>
    public void ExitMethodWithException(
        Exception? exception, string? errorContext,
        SpanId? handle = null)
    {
        ExitWith(new Threw(exception), handle, errorContext);
    }

    private void ExitWith(
        TraceOutcome outcome, SpanId? handle,
        string? errorContext = null)
    {
        lock (_lock)
        {
            var state = Current;
            if (ResolveHandle(handle) is not { } spanId
                || !state.SpanContextMap.TryGetValue(
                    spanId, out var spanContext))
            {
                return;
            }

            Emit(state, new ExitEvent(
                spanContext, Stopwatch.GetTimestamp(), outcome,
                errorContext));
            state.ActiveStack.Remove(spanId);
        }
    }

    /// <inheritdoc/>
    public void GraftChild(TraceNode node)
    {
        lock (_lock)
        {
            Emit(Current, new GraftEvent(
                ResolveParent(), Stopwatch.GetTimestamp(), node));
        }
    }

    private void Emit(State state, TraceEvent traceEvent)
    {
        state.Capture.Publish(traceEvent);
        _sink?.Publish(traceEvent);
    }

    /// <inheritdoc/>
    public void OnForkCreated(string groupId)
    {
        lock (_lock)
        {
            Emit(Current, new ForkCreatedEvent(
                groupId, Stopwatch.GetTimestamp()));
        }
    }

    /// <inheritdoc/>
    public void OnJoinComplete(
        string groupId, int memberCount, long wallTimeTicks)
    {
        lock (_lock)
        {
            Emit(Current, new MergeEvent(
                groupId, memberCount, wallTimeTicks,
                Stopwatch.GetTimestamp()));
        }
    }

    /// <inheritdoc/>
    public void OnFireAndForgetLaunched(string groupId)
    {
        lock (_lock)
        {
            Emit(Current, new FireAndForgetEvent(
                groupId, Stopwatch.GetTimestamp()));
        }
    }

    /// <inheritdoc/>
    public void SetRequestContext(
        string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp)
    {
        lock (_lock)
        {
            var state = Current;
            state.HttpMethod = httpMethod;
            state.HttpRoute = httpRoute;
            state.ClientIp = clientIp;
        }
    }

    /// <inheritdoc/>
    public void SetUserContext(
        EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId)
    {
        lock (_lock)
        {
            var state = Current;
            state.EnduserId = enduserId;
            state.SessionId = sessionId;
            state.TenantId = tenantId;
        }
    }

    /// <inheritdoc/>
    public SpanId? ParentOf(SpanId handle)
    {
        lock (_lock)
        {
            return Current.ParentMap.TryGetValue(
                handle, out var parent) ? parent : null;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Flushes the pipeline, then rebuilds the tree from scratch on every call —
    /// it is not cached, so hold the result rather than re-capturing in a loop.
    /// Does not clear state; call <see cref="Reset"/> for that.
    /// The tree carries this context's trace id, so every exporter of it names
    /// the trace that was captured; an idle context hands over none and its
    /// empty tree stays unidentified rather than minting an id here.
    /// </remarks>
    public TraceTree CaptureTrace()
    {
        lock (_lock)
        {
            var state = Current;
            state.Capture.Flush();
            return TraceTreeBuilder.Build(
                state.ReportableEvents(),
                _config.Level,
                state.TraceId.GetValueOrDefault());
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Counts what this capture lost: events a bounded sink shed under load,
    /// and worker scopes its adoption ceiling refused whole. Read after the
    /// work is done — it is a live reading, not a finished tally.
    /// </remarks>
    public TraceLoss TraceLoss
    {
        get
        {
            lock (_lock)
            {
                var state = Current;
                return new TraceLoss(
                    DroppedEvents(state),
                    state.Ledger.RefusedScopeCount,
                    state.Ledger.RefusedSpanCount);
            }
        }
    }

    private long DroppedEvents(State state)
    {
        var fromSink = (_sink as IEventLossCounter)?.DroppedEventCount ?? 0;
        var store = state.Capture as IEventLossCounter;
        return ReferenceEquals(store, _sink)
            ? fromSink
            : fromSink + (store?.DroppedEventCount ?? 0);
    }

    /// <summary>
    /// Creates an isolated fork/background context seeded with this context's
    /// trace id (generated eagerly if absent), story/chapter ids, and
    /// request/user metadata, reusing the same <see cref="NarrativeTraceConfig"/>
    /// (level + service identity) and streaming sink. The child captures into a
    /// fresh stack whose roots graft back under the parent span, so concurrent
    /// spans stay correlated to the parent trace instead of starting a new one.
    /// </summary>
    public INarrativeContext CreateConcurrentChild()
    {
        lock (_lock)
        {
            var state = Current;
            state.TraceId ??= SpanIdGenerator.GenerateTraceId();
            return new SyncNarrativeContext(_config, _sink)
            {
                _state = state.ForkSeed(),
            };
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Records the lineage as it stands — trace id (generated eagerly if
    /// absent, so a worker cannot mint a competing one), the span that is open
    /// right now, and the request/user metadata — and holds the capture it was
    /// taken from <b>weakly</b>: a pending snapshot must never keep a finished
    /// request alive, and a capture that is gone simply adopts nothing.
    /// </remarks>
    public IContextSnapshot Snapshot()
    {
        lock (_lock)
        {
            var state = Current;
            state.TraceId ??= SpanIdGenerator.GenerateTraceId();
            return new ContextSnapshot(this, state, ResolveParent());
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Replaces the calling flow's captured state atomically — the activated
    /// one inside a snapshot scope, this context's own otherwise. Handles
    /// issued before the reset are stale afterwards and exiting on one is
    /// ignored rather than throwing. Workers still holding a snapshot of the
    /// discarded state hand their spans to an object nothing reads any more:
    /// a reset request cannot be resurrected by late async work.
    /// </remarks>
    public void Reset()
    {
        lock (_lock)
        {
            if (_activated.Value is not null)
            {
                _activated.Value = NewState();
            }
            else
            {
                _state = NewState();
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The scoped parent is held in an <see cref="AsyncLocal{T}"/>, so it flows
    /// into awaits inside <paramref name="fn"/> and is restored even if it throws.
    /// </remarks>
    public T RunScoped<T>(SpanId handle, Func<T> fn)
    {
        var prev = _scopedParent.Value;
        _scopedParent.Value = handle;
        try
        {
            return fn();
        }
        finally
        {
            _scopedParent.Value = prev;
        }
    }

    private static void DeriveStoryIdIfAbsent(
        State state, SpanId? parentSpanId, MethodSignature signature)
    {
        if (parentSpanId is not null || state.StoryId is not null)
        {
            return;
        }

        state.StoryId =
            $"{signature.ClassName}.{signature.MethodName}";
        state.ChapterId = state.StoryId;
    }

    private SpanId? ResolveParent()
    {
        if (_scopedParent.Value is { } scoped)
        {
            return scoped;
        }

        var state = Current;
        if (state.ActiveStack.Count > 0)
        {
            return state.ActiveStack[state.ActiveStack.Count - 1];
        }

        return state.SnapshotParentSpanId;
    }

    private SpanId? ResolveHandle(SpanId? handle)
    {
        if (handle is { IsEmpty: false } explicitHandle)
        {
            return explicitHandle;
        }

        var stack = Current.ActiveStack;
        return stack.Count > 0 ? stack[stack.Count - 1] : null;
    }

    /// <summary>
    /// Installs a capture on the calling flow and returns what it displaced.
    /// The activated capture is flow-scoped, so this is invisible to every
    /// other thread using this context.
    /// </summary>
    private State? Install(State? state)
    {
        var previous = _activated.Value;
        _activated.Value = state;
        return previous;
    }

    /// <summary>
    /// One flow's capture: its event trail, its open spans, the lineage it
    /// inherited, and the ledger of worker captures it answers for.
    /// </summary>
    private sealed class State : IReportableCapture
    {
        private readonly int _adoptionCeiling;

        public State(int adoptionCeiling)
        {
            _adoptionCeiling = adoptionCeiling;
            Ledger = new AdoptionLedger(this, adoptionCeiling);
        }

        /// <summary>Worker captures this one is answering for, live and adopted.</summary>
        public AdoptionLedger Ledger { get; }

        public IEventPipeline Capture { get; init; } =
            new SynchronousEventPipeline();
        public List<SpanId> ActiveStack { get; init; } = [];
        public Dictionary<SpanId, SpanId?> ParentMap { get; init; } = new();
        public Dictionary<SpanId, SpanContext> SpanContextMap { get; init; }
            = new();
        public TraceId? TraceId { get; set; }
        public SpanId? SnapshotParentSpanId { get; set; }

        /// <summary>Whether this capture was opened by activating a snapshot.</summary>
        public bool FromSnapshot { get; set; }

        public string? StoryId { get; set; }
        public string? ChapterId { get; set; }
        public string? HttpMethod { get; set; }
        public HttpRoute? HttpRoute { get; set; }
        public ClientIp? ClientIp { get; set; }
        public EnduserId? EnduserId { get; set; }
        public SessionId? SessionId { get; set; }
        public TenantId? TenantId { get; set; }

        /// <summary>
        /// Seeds a fresh capture state for a concurrent child: copies trace id
        /// and request/user metadata but starts with an empty stack and its own
        /// pipeline so the child's roots graft back under the parent span.
        /// </summary>
        public State ForkSeed()
        {
            return new State(_adoptionCeiling)
            {
                TraceId = TraceId,
                StoryId = StoryId,
                ChapterId = ChapterId,
                HttpMethod = HttpMethod,
                HttpRoute = HttpRoute,
                ClientIp = ClientIp,
                EnduserId = EnduserId,
                SessionId = SessionId,
                TenantId = TenantId,
            };
        }

        /// <summary>
        /// Seeds the capture a snapshot activation installs: a fork seed that
        /// also remembers the span that was open when the snapshot was taken,
        /// so placement follows <i>submit</i> time rather than whenever the
        /// worker happened to run.
        /// </summary>
        public State SnapshotSeed(SpanId? parentSpanId)
        {
            var seed = ForkSeed();
            seed.SnapshotParentSpanId = parentSpanId;
            seed.FromSnapshot = true;
            return seed;
        }

        /// <summary>
        /// Everything this capture may report: its own events plus every worker
        /// capture's, transitively. Returns the trail unchanged when nothing
        /// was ever propagated, which is the overwhelmingly common case.
        /// </summary>
        public IReadOnlyList<TraceEvent> ReportableEvents()
        {
            if (Ledger.IsEmpty)
            {
                return Capture.Events();
            }

            var events = new List<TraceEvent>();
            CollectReportable(events);
            return events;
        }

        public void CollectReportable(List<TraceEvent> into)
        {
            into.AddRange(Capture.Events());
            Ledger.CollectReportable(into);
        }
    }

    /// <summary>
    /// A re-activatable handle on one capture's lineage. Holds the capture it
    /// was taken from weakly and its lineage by value, so an activation still
    /// carries the trace id and parent span after the origin is gone — it
    /// simply has nowhere to hand its spans back to.
    /// </summary>
    private sealed class ContextSnapshot : IContextSnapshot
    {
        private readonly SyncNarrativeContext _context;
        private readonly WeakReference<State> _origin;
        private readonly State _lineage;
        private readonly SpanId? _parentSpanId;

        internal ContextSnapshot(
            SyncNarrativeContext context, State origin, SpanId? parentSpanId)
        {
            _context = context;
            _origin = new WeakReference<State>(origin);
            _lineage = origin.ForkSeed();
            _parentSpanId = parentSpanId;
        }

        public IContextScope Activate() => Activate(adopt: true);

        public IContextScope ActivateWithoutAdoption() =>
            Activate(adopt: false);

        /// <summary>
        /// Registers the worker capture with the origin <i>before</i> any span
        /// can be published into it, so the origin reports the worker's calls
        /// from the moment they are published rather than from scope close —
        /// which is too late, because a framework can hand control back to the
        /// caller while the scope is still open.
        /// </summary>
        private IContextScope Activate(bool adopt)
        {
            var child = _lineage.SnapshotSeed(_parentSpanId);
            var origin = adopt ? Origin() : null;
            var registration = origin?.Ledger.Register(child);
            return new ContextScope(
                _context, _context.Install(child), child, origin, registration);
        }

        private State? Origin() =>
            _origin.TryGetTarget(out var origin) ? origin : null;
    }

    /// <summary>
    /// The lifetime of one activation: restores the flow's previous capture,
    /// then hands everything the worker can answer for back to the origin.
    /// </summary>
    private sealed class ContextScope : IContextScope
    {
        private readonly SyncNarrativeContext _context;
        private readonly State? _previous;
        private readonly State _child;
        private readonly State? _origin;
        private readonly WeakReference<IReportableCapture>? _registration;
        private bool _closed;

        internal ContextScope(
            SyncNarrativeContext context,
            State? previous,
            State child,
            State? origin,
            WeakReference<IReportableCapture>? registration)
        {
            _context = context;
            _previous = previous;
            _child = child;
            _origin = origin;
            _registration = registration;
        }

        /// <summary>
        /// Adopts and ends the live registration in one step, so a capture
        /// racing this close sees the worker's calls through one side or the
        /// other and never through neither — nor twice. Idempotent: a second
        /// dispose must not adopt a second time.
        /// </summary>
        public void Dispose()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _context.Install(_previous);
            _origin?.Ledger.Adopt(_child, _registration);
        }
    }
}
