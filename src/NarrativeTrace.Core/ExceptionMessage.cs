// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The one reading of <see cref="Exception.Message"/> every emitter shares.
/// </summary>
/// <remarks>
/// Mirrors java's <c>ExceptionMessage</c>. A thrown
/// outcome carries the live <see cref="Exception"/> itself, unlike a
/// returned value, which is text <c>ValueRenderer</c> already produced — so
/// a message never passed the redaction policy at all: a JWT thrown as
/// <c>new ArgumentException(token)</c> reached every output in full while
/// the identical bytes as a captured parameter answered
/// <see cref="RedactionPolicy.Marker"/>. That falsifies
/// <see cref="SecretValueShapes"/>' own stated purpose — the value axis
/// exists because a bearer token arrives with no name, and an exception
/// message is exactly such a place. This is now the one accessor every
/// renderer and exporter reads a message through, so they cannot drift:
/// before it, this port had the same message read open-coded at nine call
/// sites, four of which did not even escape.
/// <para>
/// The axis applied is the value-shape one only, deliberately: the name
/// deny-list has nothing to match on in a message, and blanking messages by
/// keyword would destroy the diagnostic value the message exists for.
/// Length is not capped for the same reason — a message is the one piece of
/// an exceptional trace a reader acts on. The claim repaired is parity —
/// the same bytes answer the same way whichever channel they travelled —
/// not clairvoyance: a token embedded in prose is shown, exactly as the
/// same string is shown as a parameter.
/// </para>
/// <para>
/// <see cref="Exception.Message"/> is a virtual property, run once per
/// emitter on whatever thread renders — a getter that throws or recurses
/// used to be able to break a renderer once per emitter path. The read is
/// guarded here, so a message that cannot be produced degrades to a marker
/// instead of failing the emitter.
/// </para>
/// </remarks>
public static class ExceptionMessage
{
    /// <summary>What an emitter says when the message could not be read at all.</summary>
    private const string Unreadable = "<error>";

    /// <summary>
    /// The message an emitter may print: redacted when its bytes are a
    /// credential, control-escaped always, and <see langword="null"/> when
    /// the exception carries none.
    /// </summary>
    /// <param name="exception">The captured exception; <see langword="null"/> answers <see langword="null"/>.</param>
    /// <returns>The text every emitter must print in place of the raw message.</returns>
    /// <remarks>
    /// <see langword="null"/> is preserved rather than turned into text. The
    /// canonical exporters distinguish "no message" from the four
    /// characters <c>null</c> — a JSON field that is absent says something
    /// a field holding <c>"null"</c> does not — so a caller that wants the
    /// literal uses <see cref="Text"/> instead.
    /// </remarks>
    public static string? Of(Exception? exception)
    {
        if (exception is null)
        {
            return null;
        }

        string? raw;
        try
        {
            raw = exception.Message;
        }
        catch
        {
            return Unreadable;
        }

        if (raw is null)
        {
            return null;
        }

        return RedactionPolicy.Default.ShouldRedactValue(raw)
            ? RedactionPolicy.Marker
            : ControlEscape.Sanitize(raw);
    }

    /// <summary>
    /// The same message where a caller needs text rather than
    /// <see langword="null"/>: the literal <c>"null"</c> every
    /// line-oriented renderer has always printed for a message-less
    /// exception.
    /// </summary>
    /// <param name="exception">The captured exception.</param>
    /// <returns><see cref="Of"/>, with <see langword="null"/> spelled as <c>"null"</c>.</returns>
    public static string Text(Exception? exception)
    {
        return Of(exception) ?? "null";
    }
}
