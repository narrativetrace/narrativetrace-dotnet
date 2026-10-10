// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Whether a page nobody stamped is nevertheless ours.
/// </summary>
/// <remarks>
/// <para>INTENT: a project can get these skills from a registry before it ever runs <c>init</c> — a
/// registry installs THIS repository's own rendered pages, out of the public git tree, without a
/// provenance line. Such a page is not a foreign page at all; it is ours, unstamped. Recognising
/// that is what lets <c>init</c> adopt it instead of refusing it, and it is the one place the
/// distinction is decided.</para>
/// <para>The comparison is the rendered bytes with ONLY the line ending normalised. A trailing
/// space, a reordered frontmatter key, a missing final newline, another release's wording, or the
/// other flavour's page is NOT adoptable — it is either somebody's edit or another release, and
/// both of those are exactly what the refusal exists to protect.</para>
/// </remarks>
internal static class Adoption
{
    /// <summary>
    /// Whether <paramref name="installed"/> is the carrier's own <paramref name="rendered"/> page for
    /// some flavour.
    /// </summary>
    /// <remarks>
    /// An empty installed page is never adoptable: a skill directory with no page at all is a
    /// directory somebody else made, not a copy of ours.
    /// </remarks>
    internal static bool IsAdoptable(string installed, string rendered)
    {
        if (installed is null || rendered is null)
        {
            throw new ArgumentException("adoption compares two pages, never null");
        }

        return installed.Length > 0
            && string.Equals(WithOneLineEnding(installed), WithOneLineEnding(rendered), StringComparison.Ordinal);
    }

    /// <summary>Both spellings of a line ending read as one, and nothing else about the page is touched.</summary>
    private static string WithOneLineEnding(string page) =>
        page.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
