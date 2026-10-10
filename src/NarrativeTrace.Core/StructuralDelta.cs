// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The structural delta between two <c>.nt</c> artifacts (see
/// <see cref="StructuralTraceRenderer"/>).
/// </summary>
/// <remarks>
/// INTENT: The comparison engine for the test-loop feedback surfaces — the
/// post-run console delta line, the failure delta against the last green
/// artifact, and approval-mode verification. Sameness is equality of the
/// artifact's lines with span ids (<see cref="SpanPath"/>) set aside: the
/// renderer is deterministic, so the same lines mean the same behavior, and
/// any difference is real change worth surfacing. A baseline written before
/// span ids existed, or checked out with CRLF line endings, still compares.
/// </remarks>
public sealed class StructuralDelta
{
    private readonly string _baseline;
    private readonly string _current;

    private StructuralDelta(string baseline, string current)
    {
        _baseline = baseline;
        _current = current;
        Unchanged = string.Equals(WithoutIds(baseline), WithoutIds(current), StringComparison.Ordinal);
    }

    /// <summary>Compares a baseline artifact (last green or approved) against the current one.</summary>
    /// <exception cref="ArgumentNullException">
    /// Either document is null — absence of a baseline is a caller-level state
    /// (a new scenario), not a delta.
    /// </exception>
    public static StructuralDelta Between(string baseline, string current)
    {
        if (baseline is null)
        {
            throw new ArgumentNullException(nameof(baseline));
        }

        if (current is null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        return new StructuralDelta(baseline, current);
    }

    /// <summary>
    /// True iff the two artifacts have the same lines once span ids, line
    /// terminators and a final newline are set aside — the scenario's
    /// structure did not change.
    /// </summary>
    public bool Unchanged { get; }

    /// <summary>
    /// Compact per-signature call-count changes, e.g. <c>+4 calls
    /// CurrencyConverter.ToBaseCurrency</c>; empty when <see cref="Unchanged"/>.
    /// </summary>
    public string Summary()
    {
        if (Unchanged)
        {
            return string.Empty;
        }

        var order = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        Accumulate(_current, 1, order, counts);
        Accumulate(_baseline, -1, order, counts);

        var parts = new List<string>();
        foreach (var signature in order)
        {
            var count = counts[signature];
            if (count != 0)
            {
                parts.Add(FormatChange(signature, count));
            }
        }

        return parts.Count == 0 ? "structure changed" : string.Join(", ", parts);
    }

    /// <summary>
    /// Full-document line diff in the conventional format: <c>-</c> removed,
    /// <c>+</c> added, one leading space on unchanged context lines; empty
    /// when <see cref="Unchanged"/>. Artifacts are small (one test scenario),
    /// so no hunk elision is applied — the whole document stays readable.
    /// </summary>
    public string Diff()
    {
        return Unchanged
            ? string.Empty
            : LineDiff.Unified(_baseline, _current, SpanPath.Strip, ContextLine);
    }

    /// <summary>
    /// An unchanged line as the current document prints it, citing the
    /// baseline's id when an insertion or removal earlier in the list shifted
    /// it: <c>#1.3 - A.B()  (was #1.2)</c>. A baseline written before span ids
    /// existed has none to cite.
    /// </summary>
    private static string ContextLine(string was, string now)
    {
        var wasId = SpanPath.Of(was);
        return wasId is null || wasId == SpanPath.Of(now) ? now : now + "  (was " + wasId + ")";
    }

    /// <summary>
    /// The document's lines with every span id removed, joined by LF — the
    /// form sameness is decided on. Ids are derived from position and carry no
    /// behavior of their own; line terminators and a final newline are
    /// encoding, not structure.
    /// </summary>
    private static string WithoutIds(string document)
    {
        return string.Join("\n", LineDiff.ToLines(document).Select(SpanPath.Strip));
    }

    private static void Accumulate(
        string document, int delta, List<string> order, Dictionary<string, int> counts)
    {
        foreach (var signature in CallSignatures(document))
        {
            if (!counts.ContainsKey(signature))
            {
                counts[signature] = 0;
                order.Add(signature);
            }

            counts[signature] += delta;
        }
    }

    /// <summary>
    /// Call lines are <c>- Class.method(params)</c> at any indent; fork
    /// markers and blanks are not calls.
    /// </summary>
    private static IEnumerable<string> CallSignatures(string document)
    {
        foreach (var line in LineDiff.ToLines(document))
        {
            var trimmed = SpanPath.Strip(line).TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                yield return SignatureOf(trimmed);
            }
        }
    }

    private static string SignatureOf(string callLine)
    {
        var open = callLine.IndexOf('(');
        return open < 0 ? callLine[2..] : callLine[2..open];
    }

    private static string FormatChange(string signature, int count)
    {
        var magnitude = Math.Abs(count);
        var noun = magnitude == 1 ? " call " : " calls ";
        return (count > 0 ? "+" : "-") + magnitude + noun + signature;
    }
}
