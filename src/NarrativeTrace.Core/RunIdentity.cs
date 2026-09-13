// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// A test-suite execution's own identity: a W3C-shaped id and the three-word
/// phrase <see cref="TraceNamer"/> derives from it.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: A trace has a name because a trace id is unreadable; a whole SUITE
/// RUN needs the same thing for a different reason — before this type, nothing
/// named "this execution" at all, so a console footer, a <c>manifest.json</c>,
/// or a log query could say "18 scenarios" but never "which 18-scenario run"
/// when two ran back to back. One <see cref="RunIdentity"/> is generated once
/// per test-suite execution (2026-09-13 ruling) and threaded explicitly to
/// every place that names the run — never re-derived, never a shared constant
/// a caller cannot vary, which is exactly what makes the "two runs,
/// byte-identical artifacts, different run name" proof possible.
/// </para>
/// <para>
/// Deliberately <b>not</b> a distinct "run id" type: a run is not a trace, has
/// no spans, and must never be confused with one in an exporter or a schema —
/// but it needs the same shape (32 lowercase hex) and the same generator's
/// entropy as a trace id, so it reuses <see cref="TraceId"/> and
/// <see cref="SpanIdGenerator.GenerateTraceId"/> as the primitive, not the
/// concept. <see cref="Name"/> is never carried beside <see cref="Id"/>, only
/// derived from it (mirroring <see cref="TraceIdentity.TraceName"/>), so the
/// two can never disagree.
/// </para>
/// <para>
/// Cross-cutting invariant: a <see cref="RunIdentity"/> must never reach the
/// structural <c>.nt</c> text, an approved/received trace, an artifact
/// filename, or a manifest per-scenario key — every call site that computes
/// one of those takes no <see cref="RunIdentity"/> parameter at all, so the
/// omission is structural, not a discipline someone has to remember.
/// </para>
/// </remarks>
public sealed record RunIdentity
{
    /// <summary>The run's own W3C-shaped id — 32 lowercase hex characters, unrelated to any trace id.</summary>
    public TraceId Id { get; }

    /// <summary>Wraps <paramref name="id"/> after rejecting what cannot name a run.</summary>
    /// <exception cref="ArgumentException"><paramref name="id"/> is <see cref="TraceId.Empty"/>.</exception>
    public RunIdentity(TraceId id)
    {
        if (id.IsEmpty)
        {
            throw new ArgumentException("RunIdentity requires a non-empty TraceId.", nameof(id));
        }

        Id = id;
    }

    /// <summary>
    /// The three-word phrase <see cref="TraceNamer"/> derives from <see cref="Id"/>, always in
    /// agreement with it because it is derived rather than carried beside it. Never empty.
    /// </summary>
    public string Name => Id.HumanName;

    /// <summary>
    /// Generates a fresh run identity: a new random id and the phrase derived from it.
    /// </summary>
    /// <remarks>
    /// Call this exactly once per test-suite execution — see the type's own remarks — and pass the
    /// single result everywhere a run needs to be named. Calling it twice names two different runs,
    /// which is correct when there genuinely are two, and wrong when a caller wanted the same run
    /// twice.
    /// </remarks>
    public static RunIdentity Generate() => new(SpanIdGenerator.GenerateTraceId());
}
