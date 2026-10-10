// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// Whether a file is a structural trace — the one artifact a report may attach, because it is the
/// one artifact that carries no runtime value by construction.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: defence in depth over the value-free rules, not a substitute for them. The rules decide
/// about TEXT; this decides about a FILE's claim to be a <c>.nt</c>. A rendered narrative copied to a
/// <c>.nt</c> name is caught by the rules anyway — it is full of values — but a file that passes the
/// rules while not being a structural trace at all (a log with nothing interesting in it, a
/// half-written note) is not the attachment the report promises, and attaching it would put an
/// unreviewed file shape into a public issue.
/// </para>
/// <para>
/// <b>@llmNote</b> The grammar is the published one: a <c>scenario:</c> header, then call lines
/// <c>Type.method(name, name)</c> with an optional outcome of the returned marker, <c>!! TypeName</c>
/// or the incomplete marker, and concurrency markers such as <c>~ fork [2]</c>. Anything else makes
/// the whole file not-a-structural-trace. Loosening this means loosening what a report may attach, so
/// loosen the FORMAT first and this after.
/// </para>
/// <para>
/// <b>@llmNote</b> A call line is taken apart with <see cref="string.IndexOf(char)"/> and
/// one-quantifier patterns rather than matched by a single regex. The obvious regex needs a nested
/// quantifier for the dotted name, which is both a backtracking hazard and a shape the security
/// scanner refuses on sight — and a check that has to be excused by an exclusion is a check nobody
/// trusts.
/// </para>
/// </remarks>
public static class StructuralTrace
{
    /// <summary>One identifier: a class name, a method name, a parameter name. No dot, by construction.</summary>
    private static readonly Regex Segment = new(@"^[\w$]+$", RegexOptions.Compiled);

    /// <summary>The whole parameter list: names, commas and spaces. Never a colon, never a value.</summary>
    private static readonly Regex Parameters = new(@"^[\w$, ]*$", RegexOptions.Compiled);

    private static readonly Regex Header = new(@"^scenario: \S.*$", RegexOptions.Compiled);

    /// <summary>A concurrency marker with an optional bracketed count, e.g. <c>~ fork [2]</c>.</summary>
    private static readonly Regex MarkerLine = new(@"^\s*~ [\w-]+( \[\d+\])?$", RegexOptions.Compiled);

    private const string Bullet = "- ";
    private const string Returned = " → value";
    private const string Incomplete = " ?? incomplete";
    private const string Threw = " !! ";

    /// <summary>
    /// Whether this content parses as a structural trace.
    /// </summary>
    /// <exception cref="ArgumentNullException">when <paramref name="content"/> is null — an absent file is <c>""</c>.</exception>
    public static bool LooksStructural(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var headerSeen = false;
        foreach (var line in content.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!AdvancesParse(line, ref headerSeen))
            {
                return false;
            }
        }

        return headerSeen;
    }

    /// <summary>
    /// Whether <paramref name="line"/> keeps the file a structural trace. The first non-blank line
    /// must be the header (and sets <paramref name="headerSeen"/>); every line after it must be a
    /// call line or a concurrency marker.
    /// </summary>
    private static bool AdvancesParse(string line, ref bool headerSeen)
    {
        if (headerSeen)
        {
            return IsBodyLine(line);
        }

        headerSeen = Header.IsMatch(line);
        return headerSeen;
    }

    /// <summary>A line after the header: a call line or a concurrency marker.</summary>
    private static bool IsBodyLine(string line) => IsCallLine(line) || MarkerLine.IsMatch(line);

    /// <summary><c>- Type.method(a, b)</c> plus at most one outcome marker.</summary>
    private static bool IsCallLine(string line)
    {
        var bullet = line.Trim();
        if (!bullet.StartsWith(Bullet, StringComparison.Ordinal))
        {
            return false;
        }

        var call = bullet[Bullet.Length..];
        var open = call.IndexOf('(');
        var close = call.LastIndexOf(')');
        if (open < 0 || close < open)
        {
            return false;
        }

        return IsDottedName(call[..open], 2)
            && Parameters.IsMatch(call[(open + 1)..close])
            && IsOutcome(call[(close + 1)..]);
    }

    /// <summary>Nothing (void), the returned marker, a thrown type name, or the incomplete marker.</summary>
    private static bool IsOutcome(string suffix)
    {
        if (suffix.Length == 0
            || Returned.Equals(suffix, StringComparison.Ordinal)
            || Incomplete.Equals(suffix, StringComparison.Ordinal))
        {
            return true;
        }

        return suffix.StartsWith(Threw, StringComparison.Ordinal)
            && IsDottedName(suffix[Threw.Length..], 1);
    }

    /// <summary>A dotted name of at least <paramref name="minimumSegments"/> identifier segments.</summary>
    private static bool IsDottedName(string name, int minimumSegments)
    {
        var segments = name.Split('.');
        if (segments.Length < minimumSegments)
        {
            return false;
        }

        return Array.TrueForAll(segments, segment => Segment.IsMatch(segment));
    }
}
