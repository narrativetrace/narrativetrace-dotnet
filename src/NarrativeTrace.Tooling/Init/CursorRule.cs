// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Renders the one Cursor rule file — <c>.cursor/rules/narrativetrace.mdc</c> — that points Cursor's
/// agent at the installed skills.
/// </summary>
/// <remarks>
/// <para>INTENT: the Cursor row's pointer. The rule is a SENTENCE, never a copy of a skill page:
/// Cursor already loads the pages from <c>.agents/skills</c>, and a second copy would be a third page
/// flavour to keep in step. The same text is the plugin's rule, so a project that installed the plugin
/// and one that ran <c>init</c> read the same words.</para>
/// <para>The file is frontmatter plus one managed section: Cursor needs the <c>---</c> block first,
/// so the <c>narrativetrace:created</c> note a markdown home carries cannot go on line 1.
/// <see cref="Frontmatter"/> is therefore also what an uninstall compares against — a rule file that
/// is nothing but the frontmatter once our section is removed is a file we made, and is deleted. With
/// an empty coordinate the opening marker carries no stamp, which is how the plugin ships it and why
/// <c>init</c> can adopt it.</para>
/// <para>Pure rendering, <c>\n</c> throughout.</para>
/// </remarks>
public static class CursorRule
{
    /// <summary>The <c>.mdc</c> frontmatter: always applied, because the pointer must be seen every turn.</summary>
    internal const string Frontmatter =
        "---\ndescription: Where the NarrativeTrace agent skills live in this project\n" +
        "alwaysApply: true\n---\n";

    /// <summary>The whole rule file for one pointer sentence.</summary>
    /// <param name="text">The pointer sentence, from the platform table's Cursor row.</param>
    /// <param name="coordinate">
    /// The carrier coordinate stamped on the opening marker, or <c>""</c> for the unstamped plugin copy.
    /// </param>
    /// <exception cref="ArgumentException">The text is blank, or the coordinate is null.</exception>
    public static string Render(string text, string coordinate)
    {
        if (string.IsNullOrWhiteSpace(text) || coordinate is null)
        {
            throw new ArgumentException("a rule needs its text and a coordinate (or \"\")");
        }

        var stamp = coordinate.Length == 0 ? "" : " " + coordinate;
        var rule = Frontmatter + MarkedBlock.StartMarker + stamp + " -->\n" + text + "\n" + MarkedBlock.EndMarker + "\n";
        Debug.Assert(MarkedBlock.ScanText(rule).HasExactlyOneRegion, "the rule must be one region");
        return rule;
    }
}
