// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The capture surface: records a method's entry and exit so a readable
/// narrative can be rebuilt from the resulting event stream.
/// </summary>
/// <remarks>
/// <para>
/// This is the interface every instrumentation front end funnels into — the
/// proxy interceptor, the ASP.NET Core middleware, and hand-written call sites
/// all end up calling <see cref="EnterMethod"/> / <c>ExitMethod*</c> here.
/// Implementations own the active span stack, the trace id, and the
/// story/chapter identity; they do <b>not</b> own rendering (see
/// <see cref="MarkdownRenderer"/> and friends) or export.
/// </para>
/// <para>
/// <b>Call protocol.</b> Every <see cref="EnterMethod"/> must be matched by
/// exactly one <c>ExitMethodWithReturn</c>, <c>ExitMethodWithException</c>, or
/// <see cref="DetachFrame"/>. Pair them in a <c>finally</c>: an unmatched enter
/// leaves the frame on the active stack and every later sibling nests under it,
/// which shows up as a runaway right-drift in the rendered trace rather than as
/// an exception.
/// </para>
/// <para>
/// <b>Handles.</b> <see cref="EnterMethod"/> returns a <see cref="SpanId"/>
/// handle. Passing it back to an exit is optional for straight-line synchronous
/// code — omitting it closes the innermost open frame — but is <b>required</b>
/// whenever frames can close out of order (async interleaving, callbacks,
/// concurrent branches), because the innermost frame is then not necessarily
/// yours.
/// </para>
/// <para>
/// <b>Threading.</b> Implementations in this library are thread-safe and lock
/// around their mutable state. Crossing a thread or a <c>Task.Run</c> boundary
/// still needs <see cref="Snapshot"/>, which carries the trace lineage; without
/// it the continuation starts an unrelated trace.
/// </para>
/// <para>
/// <b>Disabled contexts.</b> When tracing is off, <see cref="EnterMethod"/>
/// returns <see cref="SpanId.Empty"/> and the exits are no-ops, so the protocol
/// stays safe to follow unconditionally. Check <see cref="IsActive"/> only to
/// skip building arguments you would otherwise throw away — never to decide
/// whether an exit is needed. See <see cref="NoopContext"/> for the
/// always-disabled implementation.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var handle = context.EnterMethod(
///     nameof(OrderService), nameof(PlaceOrder),
///     [new ParameterCapture("orderId", orderId.ToString())]);
/// try
/// {
///     var result = PlaceOrder(orderId);
///     context.ExitMethodWithReturn(result.ToString(), handle);
///     return result;
/// }
/// catch (Exception ex)
/// {
///     context.ExitMethodWithException(ex, handle);
///     throw;
/// }
/// </code>
/// </example>
public interface INarrativeContext
{
    /// <summary>
    /// Opens a span for a method call and pushes it onto the active stack.
    /// </summary>
    /// <param name="className">
    /// The declaring type's simple name as it should read in the narrative —
    /// not the assembly-qualified name.
    /// </param>
    /// <param name="methodName">The method's name, used verbatim in the narrative.</param>
    /// <param name="parameters">
    /// The captured arguments, in declaration order. Pass an empty list rather
    /// than <see langword="null"/> for a no-arg method. Rendered values are
    /// discarded unless <see cref="CapturesParameterValues"/> is
    /// <see langword="true"/>, so check that first if rendering them is costly.
    /// </param>
    /// <param name="options">
    /// Per-call narration and error-context overrides, or <see langword="null"/>
    /// to derive the narrative from the method name.
    /// </param>
    /// <returns>
    /// A handle identifying this frame, to be passed to the matching exit.
    /// <see cref="SpanId.Empty"/> when tracing is off — still safe to pass back.
    /// </returns>
    SpanId EnterMethod(
        string className,
        string methodName,
        IReadOnlyList<ParameterCapture> parameters,
        MethodOptions? options = null);

    /// <summary>Closes a span that returned normally.</summary>
    /// <param name="renderedValue">
    /// The return value already rendered to a string, or <see langword="null"/>
    /// for a <c>void</c> method. Rendering is the caller's job so the context
    /// never holds a reference to a live domain object.
    /// </param>
    /// <param name="handle">
    /// The handle from the matching <see cref="EnterMethod"/>. Omit only in
    /// straight-line synchronous code, where the innermost open frame is
    /// necessarily this one; required whenever frames may close out of order.
    /// </param>
    void ExitMethodWithReturn(
        string? renderedValue, SpanId? handle = null);

    /// <summary>
    /// Exits with both the flat rendered string and the structured
    /// <see cref="RenderedValue"/>, so typed OTel export can emit the return
    /// value with its original .NET type. The structured value is preserved
    /// on the built node's <see cref="Returned"/> outcome.
    /// </summary>
    /// <param name="renderedValue">The return value rendered for human-readable output.</param>
    /// <param name="structuredValue">
    /// The same value with its .NET type retained, for typed export. Pass
    /// <see langword="null"/> to fall back to the flat string only.
    /// </param>
    /// <param name="handle">The handle from the matching <see cref="EnterMethod"/>.</param>
    void ExitMethodWithReturn(
        string? renderedValue,
        RenderedValue? structuredValue,
        SpanId? handle = null);

    /// <summary>Closes a span that threw, marking it as an error path.</summary>
    /// <param name="exception">
    /// The exception that escaped. <see langword="null"/> is tolerated and
    /// records an unspecified failure rather than throwing.
    /// </param>
    /// <param name="handle">The handle from the matching <see cref="EnterMethod"/>.</param>
    /// <remarks>
    /// Marks the frame and every ancestor as failed, which is what makes the
    /// error path survive at <see cref="TracingLevel.Errors"/>. This method
    /// never rethrows — call it from a <c>catch</c> and rethrow yourself.
    /// </remarks>
    void ExitMethodWithException(
        Exception? exception, SpanId? handle = null);

    /// <summary>
    /// Exits with an exception plus an error narrative resolved at
    /// exception time against the thrown type (e.g. the matching
    /// <c>[OnError]</c> template). A non-null <paramref name="errorContext"/>
    /// supersedes any enter-time error context on the node.
    /// </summary>
    /// <param name="exception">The exception that escaped; <see langword="null"/> is tolerated.</param>
    /// <param name="errorContext">
    /// The resolved error narrative. <see langword="null"/> keeps whatever
    /// context was supplied at enter time; a non-null value replaces it.
    /// </param>
    /// <param name="handle">The handle from the matching <see cref="EnterMethod"/>.</param>
    void ExitMethodWithException(
        Exception? exception, string? errorContext,
        SpanId? handle = null);

    /// <summary>
    /// Removes a frame from the active stack without recording an exit,
    /// for a span that will be completed elsewhere.
    /// </summary>
    /// <param name="handle">The handle from the <see cref="EnterMethod"/> to detach.</param>
    /// <remarks>
    /// The escape hatch for async interleaving: when a method awaits and its
    /// continuation resumes under a different logical frame, detaching stops the
    /// suspended frame from adopting unrelated spans as children. A detached
    /// frame contributes no exit event, so it renders as an unterminated span —
    /// use it only when a genuine exit will never arrive. Detaching an unknown
    /// handle is a no-op, not an error.
    /// </remarks>
    void DetachFrame(SpanId handle);

    /// <summary>
    /// Flushes buffered events and builds the finished trace tree from
    /// everything captured so far.
    /// </summary>
    /// <returns>
    /// An immutable tree filtered to the configured <see cref="TracingLevel"/>.
    /// Empty rather than null when nothing was captured.
    /// </returns>
    /// <remarks>
    /// A snapshot, not a live view: it does not clear state, so calling it twice
    /// returns the accumulated trace both times, and spans still open are
    /// rendered as unterminated. Call <see cref="Reset"/> to start a new trace.
    /// </remarks>
    TraceTree CaptureTrace();

    /// <summary>
    /// Discards all captured state — events, active stack, trace id, and
    /// story/chapter identity — returning the context to its initial condition.
    /// </summary>
    /// <remarks>
    /// The per-test boundary: call it between tests so one test's spans cannot
    /// leak into the next. Any handle from before the reset is stale afterwards;
    /// exiting on one is ignored rather than throwing. This does not restore the
    /// configured <see cref="TracingLevel"/>, which is fixed at construction.
    /// </remarks>
    void Reset();

    /// <summary>
    /// Whether this context captures anything, i.e. its level is not
    /// <see cref="TracingLevel.Off"/>.
    /// </summary>
    /// <remarks>
    /// A hint for skipping expensive capture work, not a precondition: the
    /// enter/exit protocol is safe to follow even when this is
    /// <see langword="false"/>. Never branch around an exit on it.
    /// </remarks>
    bool IsActive { get; }

    /// <summary>
    /// Performance hint: whether parameter values will be retained (true at
    /// Detail level). When false, callers may skip rendering parameter
    /// values entirely, since the context would suppress them anyway.
    /// </summary>
    bool CapturesParameterValues { get; }

    /// <summary>
    /// The trace id correlating all spans captured so far, or null before
    /// the first <see cref="EnterMethod"/> of the current trace.
    /// </summary>
    TraceId? CurrentTraceId { get; }

    /// <summary>
    /// Returns the current trace id, generating and retaining one if the
    /// trace has not entered a span yet. Lets a request boundary stamp a
    /// stable correlation id into log scope before the first
    /// <see cref="EnterMethod"/> — the first span then adopts it.
    /// </summary>
    /// <returns>The trace id, newly generated on first call if needed. Never empty.</returns>
    /// <remarks>
    /// Unlike <see cref="CurrentTraceId"/> this mutates: it pins an id for the
    /// rest of the trace. Prefer <see cref="CurrentTraceId"/> when merely
    /// observing, so a diagnostic read cannot start a trace by accident.
    /// </remarks>
    TraceId EnsureTraceId();

    /// <summary>
    /// The story id — <c>Class.Method</c> of the first root enter — set
    /// lazily and stable for the trace lifetime, or null before any root
    /// enter.
    /// </summary>
    string? StoryId { get; }

    /// <summary>
    /// The chapter id — this service's contribution to the story. Equals
    /// <see cref="StoryId"/> for a single-service trace.
    /// </summary>
    string? ChapterId { get; }

    /// <summary>
    /// Captures the current trace continuation (trace id and parent-span
    /// lineage) as a re-activatable handle. Activating it on another thread
    /// attaches work to this trace; the returned scope restores the prior
    /// state on dispose. See <see cref="IContextSnapshot"/> for the
    /// <c>Wrap</c> helpers used at executor / <c>Task.Run</c> call sites.
    /// </summary>
    /// <returns>A snapshot that can be activated on another thread. Never null.</returns>
    IContextSnapshot Snapshot();

    /// <summary>Looks up the parent of a span in the recorded lineage.</summary>
    /// <param name="handle">A handle previously returned by <see cref="EnterMethod"/>.</param>
    /// <returns>
    /// The parent's handle, or <see langword="null"/> when
    /// <paramref name="handle"/> is a root span <b>or</b> is simply unknown to
    /// this context — the two cases are indistinguishable from the return value.
    /// </returns>
    SpanId? ParentOf(SpanId handle);

    /// <summary>
    /// Runs a function with a specific frame installed as the parent for any
    /// spans it opens.
    /// </summary>
    /// <typeparam name="T">The function's return type.</typeparam>
    /// <param name="handle">The frame to nest new spans under.</param>
    /// <param name="fn">The work to run. Executed exactly once, synchronously.</param>
    /// <returns>Whatever <paramref name="fn"/> returns.</returns>
    /// <remarks>
    /// Re-parents rather than re-entering: it opens no span of its own. The
    /// previous parent is restored even if <paramref name="fn"/> throws, and the
    /// exception propagates unchanged. The scoping flows with async control flow,
    /// so awaits inside <paramref name="fn"/> stay correctly parented — but the
    /// method returns as soon as <paramref name="fn"/> does, so handing it an
    /// <c>async</c> lambda scopes only up to that lambda's first incomplete await.
    /// </remarks>
    T RunScoped<T>(SpanId handle, Func<T> fn);

    /// <summary>
    /// Attaches an already-built subtree — typically captured on another
    /// thread or in another context — beneath the current frame.
    /// </summary>
    /// <param name="node">The finished subtree to splice in.</param>
    /// <remarks>
    /// The merge-back half of concurrency stitching: a forked child context
    /// captures independently, then grafts its roots here so the branches
    /// reappear under the span that spawned them. Graft after the child work has
    /// finished — the node is copied as-is and later mutation is not observed.
    /// </remarks>
    void GraftChild(TraceNode node);

    /// <summary>
    /// Stamps request-tier context (HTTP method, route, client IP) onto
    /// spans created after this call. Typically set once at a request entry
    /// point.
    /// </summary>
    /// <param name="httpMethod">The HTTP verb, or <see langword="null"/> to leave unset.</param>
    /// <param name="httpRoute">The matched route template — the low-cardinality pattern, not the raw path.</param>
    /// <param name="clientIp">The caller's address, subject to the configured redaction policy.</param>
    /// <remarks>
    /// Not retroactive: spans already opened keep whatever context they were
    /// created with, so call this before the first <see cref="EnterMethod"/> of
    /// the request. Each argument is independently optional.
    /// </remarks>
    void SetRequestContext(
        string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp);

    /// <summary>
    /// Stamps user-tier context (end-user, session, tenant identity) onto
    /// spans created after this call.
    /// </summary>
    /// <param name="enduserId">The acting user's id, or <see langword="null"/> to leave unset.</param>
    /// <param name="sessionId">The session id, or <see langword="null"/>.</param>
    /// <param name="tenantId">The tenant id in a multi-tenant deployment, or <see langword="null"/>.</param>
    /// <remarks>
    /// Write-only by design — these values are side-band span metadata, not a
    /// request-scoped user store, and there is deliberately no read-back on this
    /// interface. Like <see cref="SetRequestContext"/> it is not retroactive.
    /// </remarks>
    void SetUserContext(
        EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId);
}
