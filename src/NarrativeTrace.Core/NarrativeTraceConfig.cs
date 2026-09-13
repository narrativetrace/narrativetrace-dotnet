// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The live knobs a context reads: how much to capture, and which service to
/// attribute it to.
/// </summary>
/// <remarks>
/// Shared by reference with the context that was constructed from it, so
/// changing <see cref="Level"/> retunes that context in place — no restart and
/// no rebuild. Thread-safe for this purpose: the level is a volatile read on
/// every capture decision. Note the asymmetry — the level is mutable at runtime,
/// <see cref="ServiceIdentity"/> is fixed at construction.
/// </remarks>
public sealed class NarrativeTraceConfig
{
    private volatile TracingLevel _level;

    /// <summary>Creates a configuration.</summary>
    /// <param name="level">
    /// How much to capture. Defaults to <see cref="TracingLevel.Detail"/>, the
    /// most verbose level — set it explicitly for production rather than
    /// inheriting that default.
    /// </param>
    /// <param name="serviceIdentity">
    /// Service metadata stamped onto every span, or <see langword="null"/> for a
    /// single-service trace that needs no attribution.
    /// </param>
    /// <param name="initialTraceparent">
    /// A W3C trace context to adopt at construction time, or
    /// <see langword="null"/> for a fresh trace. See
    /// <see cref="InitialTraceparent"/>.
    /// </param>
    public NarrativeTraceConfig(
        TracingLevel level = TracingLevel.Detail,
        ServiceIdentity? serviceIdentity = null,
        Traceparent? initialTraceparent = null)
    {
        _level = level;
        ServiceIdentity = serviceIdentity;
        InitialTraceparent = initialTraceparent;
    }

    /// <summary>
    /// How much to capture. Writable at runtime and read volatile, so a
    /// contexts's verbosity can be changed while it is in use.
    /// </summary>
    /// <remarks>
    /// Takes effect on the next capture decision, not retroactively: spans
    /// already captured keep the detail they were captured with, and a span
    /// already open may be entered at one level and exited at another.
    /// </remarks>
    public TracingLevel Level
    {
        get => _level;
        set => _level = value;
    }

    /// <summary>
    /// Optional service metadata stamped onto every span context.
    /// </summary>
    public ServiceIdentity? ServiceIdentity { get; }

    /// <summary>
    /// A W3C trace context to adopt without an inbound HTTP header — the
    /// no-header equivalent of what <c>NarrativeTraceMiddleware</c> adopts from
    /// a <c>traceparent</c> request header. <see langword="null"/> by default,
    /// meaning every context built from this configuration starts a fresh,
    /// randomly generated trace.
    /// </summary>
    /// <remarks>
    /// Fixed at construction, like <see cref="ServiceIdentity"/> — there is no
    /// setter, so a context already running cannot be re-seeded through it;
    /// call <see cref="INarrativeContext.AdoptTraceparent"/> directly for that.
    /// Intended for seeding a demo or a documentation snippet with a
    /// deterministic trace id, never for production traffic: every context
    /// built from one shared <see cref="NarrativeTraceConfig"/> adopts the same
    /// fixed value, which is correct for a single top-level
    /// <c>SyncNarrativeContext</c> but defeats <c>AsyncNarrativeContext</c>'s
    /// whole purpose of giving each scope its own distinct trace — do not pair
    /// the two.
    /// </remarks>
    public Traceparent? InitialTraceparent { get; }
}
