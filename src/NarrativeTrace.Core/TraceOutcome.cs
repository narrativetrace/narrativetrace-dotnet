// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// How a traced call ended: returned, threw, or never completed.
/// </summary>
/// <remarks>
/// A closed hierarchy — <see cref="Returned"/>, <see cref="Threw"/> and
/// <see cref="Incomplete"/> are the only cases, and the constructor is
/// deliberately not public for extension. Match on it with a type pattern and
/// keep the switch exhaustive; a new case would be a breaking change to every
/// renderer.
/// </remarks>
/// <example>
/// <code>
/// var text = node.Outcome switch
/// {
///     Returned r  => $"returned {r.RenderedValue ?? "void"}",
///     Threw t     => $"threw {t.Error?.GetType().Name ?? "an error"}",
///     Incomplete  => "never completed",
///     _           => "unknown",
/// };
/// </code>
/// </example>
public abstract record TraceOutcome;

/// <summary>The call completed normally.</summary>
/// <param name="RenderedValue">
/// The return value rendered to a string, or <see langword="null"/> for a
/// <c>void</c> method — so <see langword="null"/> means "nothing to return",
/// while <c>"null"</c> means the method returned a null reference.
/// </param>
/// <param name="StructuredValue">
/// The same value with its .NET type retained, for typed OTel export, or
/// <see langword="null"/> when only the flat rendering was captured. Present
/// only when the caller used the structured exit overload.
/// </param>
public sealed record Returned(
    string? RenderedValue,
    RenderedValue? StructuredValue = null) : TraceOutcome;

/// <summary>The call threw; this frame and its ancestors are on the error path.</summary>
/// <param name="Error">
/// The exception that escaped, or <see langword="null"/> when a failure was
/// recorded without one. Callers must null-check: this is the outcome that
/// survives at <see cref="TracingLevel.Errors"/>, so it is the case most likely
/// to be read, and the exception is the part most likely to be absent.
/// </param>
public sealed record Threw(Exception? Error) : TraceOutcome;

/// <summary>
/// The span was opened but never closed — no exit was recorded before the
/// trace was captured.
/// </summary>
/// <remarks>
/// Usually a bug at the call site: an <see cref="INarrativeContext.EnterMethod"/>
/// without its matching exit in a <c>finally</c>. It also appears legitimately
/// for a frame closed with <see cref="INarrativeContext.DetachFrame"/>, and for
/// work still in flight when <see cref="INarrativeContext.CaptureTrace"/> ran.
/// </remarks>
public sealed record Incomplete() : TraceOutcome;
