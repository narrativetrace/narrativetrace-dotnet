// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Splits an identifier into the word tokens every clarity scorer works from.
/// </summary>
/// <remarks>
/// The first step of all clarity analysis: scoring judges tokens, not raw
/// identifiers. Handles camelCase, PascalCase and snake_case, and keeps
/// consecutive capitals together so an acronym survives as one token rather
/// than becoming a run of single letters.
/// </remarks>
public static class IdentifierTokenizer
{
    /// <summary>Splits an identifier into lowercase word tokens.</summary>
    /// <param name="identifier">
    /// The identifier to split. <see langword="null"/> or empty yields an empty
    /// list rather than throwing — callers treat "no tokens" as unscoreable.
    /// </param>
    /// <returns>
    /// The tokens in source order, <b>lowercased</b> — casing is a boundary
    /// signal here, not part of the token, so the result is not a substring of
    /// the input. Underscores are separators and are dropped, so they never
    /// appear in the result. A segment with no letter or digit in it at all
    /// (e.g. a run of plain spaces left over after an underscore split) is
    /// dropped too, rather than emitted as a token with no readable word.
    /// </returns>
    /// <example>
    /// <code>
    /// IdentifierTokenizer.Tokenize("parseHTTPResponse");
    /// // ["parse", "http", "response"] — the acronym stays one token
    /// </code>
    /// </example>
    public static IReadOnlyList<string> Tokenize(
        string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            return [];
        }

        var tokens = new List<string>();
        var start = 0;

        for (var i = 1; i < identifier.Length; i++)
        {
            if (IsBoundary(identifier, i))
            {
                AddToken(tokens, identifier, start, i);
                start = IsUnderscore(identifier[i])
                    ? i + 1 : i;
            }
        }

        AddToken(tokens, identifier, start, identifier.Length);
        return tokens;
    }

    private static bool IsBoundary(string s, int i)
    {
        if (IsUnderscore(s[i]))
        {
            return true;
        }

        if (IsDigitBoundary(s, i))
        {
            return true;
        }

        return IsCaseBoundary(s, i);
    }

    private static bool IsUnderscore(char c) => c == '_';

    private static bool IsDigitBoundary(string s, int i)
    {
        return char.IsDigit(s[i]) != char.IsDigit(s[i - 1]);
    }

    private static bool IsCaseBoundary(string s, int i)
    {
        if (!char.IsUpper(s[i]))
        {
            return false;
        }

        if (!char.IsUpper(s[i - 1]))
        {
            return true;
        }

        return i + 1 < s.Length && char.IsLower(s[i + 1]);
    }

    private static void AddToken(
        List<string> tokens, string s, int start, int end)
    {
        // Leading underscores (e.g. "_account") are delimiters, not token
        // content — Java-edition parity: tokens never contain underscores.
        while (start < end && IsUnderscore(s[start]))
        {
            start++;
        }

        // A segment can survive underscore-stripping and still carry no
        // readable word — e.g. "__  " splits on the underscores and leaves a
        // trailing run of plain spaces, which is neither empty nor a word.
        // Only underscores are treated as separators elsewhere in this class,
        // so nothing upstream would otherwise stop whitespace (or any other
        // non-word content glued in by a hostile identifier) from being
        // emitted as a bogus token — found by the security fuzz suite's
        // generated-identifier property test.
        if (start < end && HasWordChar(s, start, end))
        {
            tokens.Add(
                s.Substring(start, end - start)
                    .ToLowerInvariant());
        }
    }

    private static bool HasWordChar(string s, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (char.IsLetterOrDigit(s[i]))
            {
                return true;
            }
        }

        return false;
    }
}
