// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// An <see cref="HttpClient"/> handler that sends the current trace as a W3C
/// <c>traceparent</c> header on every outgoing request, so an instrumented
/// callee joins the caller's trace.
/// </summary>
/// <remarks>
/// The header names the trace id and the innermost active span as the callee's
/// parent. Nothing is added when no trace is active, and a <c>traceparent</c>
/// the caller already set is left untouched. Resolution of the context happens
/// per request, so one handler instance serves many flows.
/// </remarks>
public sealed class TraceparentPropagationHandler : DelegatingHandler
{
    private readonly Func<INarrativeContext?> _context;

    /// <summary>Propagates the trace of a fixed <paramref name="context"/>.</summary>
    /// <param name="context">The narrative context of the calling flow.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public TraceparentPropagationHandler(INarrativeContext context)
        : this(CaptureOf(context))
    {
    }

    /// <summary>Propagates the trace of whatever context <paramref name="context"/> yields per request.</summary>
    /// <param name="context">Resolver called for each request; may return <see langword="null"/> for "no trace".</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public TraceparentPropagationHandler(Func<INarrativeContext?> context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Attach(request);
        return base.SendAsync(request, cancellationToken);
    }

    private void Attach(HttpRequestMessage request)
    {
        if (request.Headers.Contains(Traceparent.HeaderName))
        {
            return;
        }

        var value = _context() is { } context ? TraceparentHeaders.Current(context) : null;
        if (value is not null)
        {
            request.Headers.TryAddWithoutValidation(Traceparent.HeaderName, value);
        }
    }

    private static Func<INarrativeContext?> CaptureOf(INarrativeContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return () => context;
    }
}
