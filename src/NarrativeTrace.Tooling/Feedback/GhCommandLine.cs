// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The exact <c>gh issue create</c> line that files this report — PRINTED, never run.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the second yes-path, offered only when <c>gh</c> is present AND authenticated. It saves
/// the browser step for people who already live in that tool, and it is a convenience rather than the
/// foundation: <c>gh</c> ships on hosted runners and is absent from most ordinary machines, which is
/// why the pre-filled URL is the default and this is not.
/// </para>
/// <para>
/// <b>@llmNote</b> This library never executes the line. Running it is the user's act, in their own
/// shell, with their own credential — and because <c>gh</c> is outside the skills' closed command
/// vocabulary, an agent that runs it is asked for permission by its harness as well. Two independent
/// gates, neither of which this code can bypass by printing something.
/// </para>
/// <para>
/// <b>@llmNote</b> The title is single-quoted with <c>'\''</c> escaping, and that is a security
/// property, not formatting. The step comes from a project — a test name, a check id, whatever the
/// agent read — and an unquoted title containing <c>;</c> is a second command in a line we told
/// somebody to paste into their shell.
/// </para>
/// </remarks>
public static class GhCommandLine
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// The one-line invocation.
    /// </summary>
    /// <param name="report">The report to file.</param>
    /// <param name="bodyFile">Where the body was written, as the user will type it.</param>
    /// <exception cref="ArgumentException">when the report is not a .NET report, or the path is blank.</exception>
    public static string Of(FeedbackReport report, string bodyFile)
    {
        PublicRepository.RequireDotNetRuntime(report);
        if (string.IsNullOrWhiteSpace(bodyFile))
        {
            throw new ArgumentException(
                "the gh line files the body from a FILE — there is no line without one", nameof(bodyFile));
        }

        var line = new StringBuilder("gh issue create");
        line.Append(" --repo ").Append(PublicRepository.Slug);
        line.Append(" --template ").Append(PublicRepository.Form);
        line.Append(" --title ").Append(SingleQuote(OneLine(PublicRepository.TitleFor(report))));
        line.Append(" --body-file ").Append(bodyFile.Trim());
        foreach (var label in PublicRepository.LabelsFor(report))
        {
            line.Append(" --label ").Append(OneLine(label));
        }

        return line.ToString();
    }

    /// <summary>A printed command is one line: a newline inside it would be a second command.</summary>
    private static string OneLine(string value) => Whitespace.Replace(value, " ").Trim();

    /// <summary>POSIX single-quoting: the only quoting under which a shell interprets nothing.</summary>
    private static string SingleQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";
}
