// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;

namespace NarrativeTrace.Core;

/// <summary>
/// What a run lost: events the buffered path shed, and worker scopes whose
/// spans a capture refused because its adoption ceiling had no room.
/// </summary>
/// <remarks>
/// <para>
/// Loss is normal in a best-effort tracer and silent loss is not: a trace that
/// quietly omits a subtree reads exactly like a trace of code that never ran
/// it. Every lossy path in this runtime reports through this one record, and
/// <see cref="Describe"/> renders the single suite-footer line that says so —
/// omitted entirely when nothing was lost, so a clean run stays quiet.
/// </para>
/// <para>
/// Read it from a context through <see cref="ITraceLossSource"/>; contexts that
/// cannot lose anything (the no-op context, a purely synchronous capture)
/// report <see cref="None"/>.
/// </para>
/// </remarks>
/// <param name="DroppedEvents">
/// Events discarded by a bounded buffered consumer under load. Never negative.
/// </param>
/// <param name="RefusedScopes">
/// Worker scopes whose hand-over was refused whole because the receiving
/// capture's adoption ceiling had no room for it. Counted per scope, because
/// "3 scopes" tells a reader how many stories are missing.
/// </param>
/// <param name="RefusedSpans">
/// Spans lost to those refusals — the number of calls a reader of the trace is
/// missing, which "3 scopes" alone does not say.
/// </param>
public sealed record TraceLoss(
    long DroppedEvents,
    long RefusedScopes,
    long RefusedSpans)
{
    /// <summary>A run that lost nothing. The value every lossless path reports.</summary>
    public static readonly TraceLoss None = new(0, 0, 0);

    /// <summary>
    /// Whether nothing was lost — the case in which no footer line is printed
    /// at all.
    /// </summary>
    public bool IsLossless =>
        DroppedEvents == 0 && RefusedScopes == 0 && RefusedSpans == 0;

    /// <summary>
    /// The one-line loss summary, or <see langword="null"/> when
    /// <see cref="IsLossless"/>.
    /// </summary>
    /// <returns>
    /// A line of the shape
    /// <c>Incomplete: 1,204 events dropped (buffer full) · 3 async scopes not adopted (cap, 4,100 spans)</c>,
    /// carrying only the halves that actually lost something. Formatted with
    /// <see cref="CultureInfo.InvariantCulture"/>, like every other artifact
    /// this library writes.
    /// </returns>
    /// <remarks>
    /// Returning <see langword="null"/> rather than an empty string is
    /// deliberate: the caller's decision is "print a line or print nothing",
    /// and a null makes forgetting the check a compile-time nag rather than a
    /// blank line in the footer.
    /// </remarks>
    public string? Describe()
    {
        if (IsLossless)
        {
            return null;
        }

        var parts = new List<string>(2);
        if (DroppedEvents > 0)
        {
            parts.Add($"{Number(DroppedEvents)} events dropped (buffer full)");
        }

        if (RefusedScopes > 0 || RefusedSpans > 0)
        {
            parts.Add(
                $"{Number(RefusedScopes)} async scopes not adopted "
                + $"(cap, {Number(RefusedSpans)} spans)");
        }

        return "Incomplete: " + string.Join(" · ", parts);
    }

    private static string Number(long value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
