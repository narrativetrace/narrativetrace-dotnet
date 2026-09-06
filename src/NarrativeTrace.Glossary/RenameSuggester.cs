// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Mechanically derives the canonical-term rename for an identifier using a
/// deprecated alias.
/// </summary>
/// <remarks>
/// The plan's rename rule — find the alias's token window inside the
/// identifier's normalized token list, splice in the canonical term's tokens,
/// and re-join in the identifier's original casing convention
/// (<c>openAccountWithOverdraft</c> → <c>openOverdraftAccount</c>). Matching
/// happens on normalized tokens so plural spellings still match; the
/// untouched prefix and suffix keep their original raw spelling.
/// </remarks>
public static class RenameSuggester
{
    /// <summary>
    /// Case-preserving split, same boundaries as clarity's
    /// <c>IdentifierTokenizer</c> (which lowercases and therefore cannot be
    /// reused for casing reconstruction).
    /// </summary>
    private static readonly Regex RawSplit = new(
        "(?<=[a-z])(?=[A-Z])"
        + "|(?<=[A-Z])(?=[A-Z][a-z])"
        + "|(?<=[a-zA-Z])(?=[0-9])"
        + "|(?<=[0-9])(?=[a-zA-Z])"
        + "|_");

    /// <summary>
    /// Suggests the canonical rename of an identifier that uses a deprecated
    /// alias.
    /// </summary>
    /// <param name="identifier">Offending identifier; must not be blank.</param>
    /// <param name="aliasPhrase">Normalized alias phrase observed in the identifier; must not be blank.</param>
    /// <param name="canonicalPhrase">Normalized canonical term to splice in; must not be blank.</param>
    /// <returns>
    /// The renamed identifier in the original casing convention, or null when
    /// the alias tokens do not appear contiguously in the identifier.
    /// </returns>
    /// <exception cref="ArgumentException">Any argument is blank.</exception>
    public static string? Suggest(
        string identifier, string aliasPhrase, string canonicalPhrase)
    {
        GuardBlank(identifier, nameof(identifier));
        GuardBlank(aliasPhrase, nameof(aliasPhrase));
        GuardBlank(canonicalPhrase, nameof(canonicalPhrase));
        var rawTokens = RawTokens(identifier);
        var normalizedTokens = TermNormalizer.Phrase(identifier).Split(' ');
        Debug.Assert(
            rawTokens.Count == normalizedTokens.Length,
            "raw and normalized token counts must align");
        var window = aliasPhrase.Split(' ');
        var start = WindowStart(normalizedTokens, window);
        if (start < 0)
        {
            return null;
        }

        return Rebuild(
            identifier, rawTokens, start, start + window.Length,
            canonicalPhrase.Split(' '));
    }

    private static List<string> RawTokens(string identifier)
    {
        return RawSplit.Split(identifier).Where(s => s.Length > 0).ToList();
    }

    /// <summary>Returns the first index where <paramref name="window"/> occurs contiguously, or -1.</summary>
    private static int WindowStart(
        IReadOnlyList<string> tokens, IReadOnlyList<string> window)
    {
        for (var i = 0; i + window.Count <= tokens.Count; i++)
        {
            if (window.SequenceEqual(
                    tokens.Skip(i).Take(window.Count), StringComparer.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    private static string Rebuild(
        string identifier, IReadOnlyList<string> rawTokens,
        int start, int end, IReadOnlyList<string> replacement)
    {
        var tokens = new List<string>();
        tokens.AddRange(rawTokens.Take(start));
        tokens.AddRange(replacement);
        tokens.AddRange(rawTokens.Skip(end));
        if (identifier.IndexOf('_') >= 0)
        {
            return string.Join("_", tokens.Select(t => t.ToLowerInvariant()));
        }

        return JoinCamel(
            tokens, char.IsUpper(identifier[0]), start, replacement.Count);
    }

    private static string JoinCamel(
        IReadOnlyList<string> tokens, bool pascal, int spliceStart, int spliceLength)
    {
        var output = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            var spliced = i >= spliceStart && i < spliceStart + spliceLength;
            var capitalize = i > 0 || pascal;
            output.Append(spliced || i == 0 ? Cased(token, capitalize) : token);
        }

        return output.ToString();
    }

    private static string Cased(string token, bool capitalize)
    {
        var lower = token.ToLowerInvariant();
        return capitalize
            ? char.ToUpperInvariant(lower[0]) + lower.Substring(1)
            : lower;
    }

    private static void GuardBlank(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} must not be blank", name);
        }
    }
}
