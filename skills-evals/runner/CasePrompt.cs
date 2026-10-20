// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>Whether a <c>prompt.md</c> is a case document or the bare text to send.</summary>
public enum CasePromptKind
{
    /// <summary>The whole file is the prompt and is sent byte for byte.</summary>
    BarePrompt,

    /// <summary>A write-up whose first line starts with <c># Case:</c>; only its quoted task prompt is sent.</summary>
    CaseDocument,
}

/// <summary>
/// Decides what of a case's <c>prompt.md</c> the agent is sent. A case document carries the grader's
/// own sections (expected trajectory, grading) beside the task, so sending the whole file would hand
/// the agent what is checked; only the <c>&gt;</c> blockquote under its <c>**Task prompt</c> line
/// leaves it. Any other file is a bare prompt, whose hard wraps are part of the pasted text, and is
/// sent unchanged. Mirrors <c>CasePrompt.java</c> and the TypeScript <c>case-prompt.ts</c>.
/// </summary>
public static class CasePrompt
{
    private const string CaseMarker = "# Case:";
    private const string TaskLineMarker = "**Task prompt";

    /// <summary>Classifies <paramref name="text"/> by its first line alone.</summary>
    /// <param name="text">The full contents of a <c>prompt.md</c>.</param>
    /// <returns><see cref="CasePromptKind.CaseDocument"/> iff the first line starts with <c># Case:</c>.</returns>
    public static CasePromptKind Kind(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.StartsWith(CaseMarker, StringComparison.Ordinal) ? CasePromptKind.CaseDocument : CasePromptKind.BarePrompt;
    }

    /// <summary>The text to send the agent as the case's first turn.</summary>
    /// <param name="path">The file the text came from; named in the failure.</param>
    /// <param name="text">The full contents of a <c>prompt.md</c>.</param>
    /// <returns>
    /// A bare prompt unchanged; for a case document the task blockquote with its <c>&gt; </c> prefix
    /// stripped, wrapped lines joined by one space, an empty <c>&gt;</c> line as a paragraph break.
    /// </returns>
    /// <exception cref="InvalidOperationException">a case document has no non-empty blockquote under its task line.</exception>
    public static string TaskPrompt(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        if (Kind(text) == CasePromptKind.BarePrompt)
        {
            return text;
        }

        var quoted = QuotedLinesUnderTaskLine(text.Split('\n').Select(l => l.TrimEnd('\r')).ToList());
        var task = string.Join("\n\n", Paragraphs(quoted));
        return task.Length > 0
            ? task
            : throw new InvalidOperationException($"{path} is a case document but has no '>' blockquote under its '**Task prompt' line");
    }

    private static List<string> QuotedLinesUnderTaskLine(List<string> lines)
    {
        var start = lines.FindIndex(l => l.StartsWith(TaskLineMarker, StringComparison.Ordinal)) + 1;
        if (start == 0)
        {
            return [];
        }

        while (start < lines.Count && lines[start].Trim().Length == 0)
        {
            start++;
        }

        return lines.Skip(start).TakeWhile(l => l.StartsWith('>')).Select(StripQuote).ToList();
    }

    private static string StripQuote(string line) => line[1..].TrimStart(' ').TrimEnd();

    private static IEnumerable<string> Paragraphs(List<string> quoted) =>
        string.Join('\n', quoted)
            .Split("\n\n", StringSplitOptions.None)
            .Select(p => string.Join(' ', p.Split('\n', StringSplitOptions.RemoveEmptyEntries)))
            .Where(p => p.Length > 0);
}
