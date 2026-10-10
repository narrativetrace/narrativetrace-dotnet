// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// Rewrites an absolute home directory to <c>~</c> — the one normalisation a draft performs before
/// the gate reads anything.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: a home path names the account it belongs to, and a problem report is full of them because
/// that is where people's projects live. Refusing the report would be the wrong answer to a mistake
/// nobody made on purpose; <c>~</c> says the same thing about the same file and names nobody. So this
/// runs FIRST, and <see cref="ValueFreeRule.HomePath"/> is left as the backstop for a form this cannot
/// normalise — a UNC share, a path typed into the body file after the draft was shown.
/// </para>
/// <para>
/// <b>@llmNote</b> <c>/home</c> with nothing after it is NOT a home directory, and neither is
/// <c>/usr/share/ada</c>: the account segment has to be present and has to follow one of the three
/// platform roots. A rewrite that was eager here would turn an ordinary path into <c>~</c> and make
/// the report wrong instead of safe.
/// </para>
/// </remarks>
public static class HomePaths
{
    /// <summary>The three platform roots followed by one account segment.</summary>
    private static readonly Regex Home =
        new(@"(?:/Users/|/home/)[^/\s]+|[A-Za-z]:\\Users\\[^\\\s]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// The text with every home directory replaced by <c>~</c>.
    /// </summary>
    /// <exception cref="ArgumentNullException">when <paramref name="text"/> is null — an absent field is <c>""</c>.</exception>
    public static string ToTilde(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Home.Replace(text, "~");
    }
}
