// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.AspNetCore;

/// <summary>
/// Registers outbound trace propagation on an <c>IHttpClientFactory</c> client.
/// </summary>
public static class HttpClientBuilderExtensions
{
    /// <summary>
    /// Makes every request sent by this client carry the current trace as a W3C
    /// <c>traceparent</c> header, so an instrumented callee joins the caller's trace.
    /// </summary>
    /// <param name="builder">The client to add the handler to.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <remarks>
    /// Factory handlers live in their own scope, not the request's, so the handler resolves the
    /// <see cref="INarrativeContext"/> of the request being served through
    /// <see cref="IHttpContextAccessor"/> (registered here if absent). A call made outside a
    /// request, or from a flow with no trace, sends no header; a header the caller set is kept.
    /// Use <see cref="TraceparentHeaders.Current"/> when no factory is involved.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> is <see langword="null"/>.</exception>
    public static IHttpClientBuilder AddNarrativeTraceTraceparentPropagation(
        this IHttpClientBuilder builder)
    {
        if (builder is null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        builder.Services.AddHttpContextAccessor();
        return builder.AddHttpMessageHandler(services =>
        {
            var accessor = services.GetRequiredService<IHttpContextAccessor>();
            return new TraceparentPropagationHandler(() =>
                accessor.HttpContext?.RequestServices?.GetService<INarrativeContext>());
        });
    }
}
