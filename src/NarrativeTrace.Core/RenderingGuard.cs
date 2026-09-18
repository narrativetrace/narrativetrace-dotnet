// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Per-flow flag marking that rendering-time reflective code is currently
/// running on the calling async flow.
/// </summary>
/// <remarks>
/// <para>
/// <b>INTENT:</b> <c>NarrativeInterceptor</c> (<c>NarrativeTrace.Proxy</c>)
/// renders a captured parameter or return value by reflecting over its
/// public members (<see cref="ValueRenderer"/>/<see cref="TypeShape"/>)
/// and, for a <c>[Narrated]</c> template, by reading a named property path
/// (<c>NarrationResolver</c>). When the rendered value's runtime type is
/// itself a <see cref="System.Reflection.DispatchProxy"/>-generated tracing
/// proxy — exactly what a DI-wrapped collaborator, or one traced interface
/// handed to another, already is — that reflective read invokes the
/// proxy's generated getter, which calls straight back into
/// <c>NarrativeInterceptor.Invoke</c>: a call the application never made,
/// opening a span for it. Attribution is wrong two ways depending on which
/// value is being rendered: a root-level phantom for a parameter (rendered
/// while building the enter event, before any span is open yet), or a
/// spurious child of whatever span happens to still be open for a return
/// value (rendered before the owning span's own exit is recorded). This
/// flag is how <c>NarrativeInterceptor.Invoke</c> recognizes "I am being
/// invoked back from inside my own rendering" and answers with the
/// untraced fast path instead of opening a span.
/// </para>
/// <para>
/// Lives in <c>NarrativeTrace.Core</c> rather than beside its original
/// caller (<c>NarrativeTrace.Proxy</c>) so a second, sibling caller can
/// share it without a lateral cross-layer dependency: <c>LoggingNarrativeContext</c>
/// (<c>NarrativeTrace.Logging</c>) reads a thrown exception's overridden
/// <c>Message</c> on the durable logging path (<c>ExceptionMessage.Text</c>),
/// and that read needs the same guard — a hostile <c>Message</c> override
/// that calls back into a traced collaborator must not open a phantom span
/// for it either. Both callers are granted <c>InternalsVisibleTo</c> by
/// <c>NarrativeTrace.Core</c>.
/// </para>
/// <para>
/// <see cref="System.Threading.AsyncLocal{T}"/>, not
/// <see cref="System.Threading.ThreadLocal{T}"/>: rendering runs
/// synchronously within one logical call flow, but that flow can hop threads
/// under async continuations, and a thread-pool thread picked up for an
/// unrelated concurrent call must never observe a flag another flow set on
/// it. A boolean, not a counter, is enough — rendering never observes
/// re-entry on the same flow, because a reflective call back into a woven
/// accessor made while the flag is set is stopped at
/// <c>NarrativeInterceptor.Invoke</c>'s own gate before it can call back
/// into the renderer.
/// </para>
/// </remarks>
internal static class RenderingGuard
{
    private static readonly AsyncLocal<bool> Rendering = new();

    /// <summary>
    /// Whether the calling flow is currently inside a rendering-time
    /// reflective read.
    /// </summary>
    /// <remarks>
    /// Read at the very top of <c>NarrativeInterceptor.Invoke</c>, before
    /// anything else; keep this a plain <see cref="AsyncLocal{T}"/> read,
    /// never anything that can throw or block.
    /// </remarks>
    public static bool IsActive => Rendering.Value;

    /// <summary>
    /// Marks the calling flow as rendering for the lifetime of the returned
    /// scope. Always used in a <see langword="using"/> statement so the flag
    /// clears even when the wrapped work throws — a throwing
    /// <c>ToString()</c>/getter during rendering must never leave the flow
    /// permanently marked as rendering.
    /// </summary>
    public static IDisposable Enter()
    {
        Rendering.Value = true;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Rendering.Value = false;
        }
    }
}
