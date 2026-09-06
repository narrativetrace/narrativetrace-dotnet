// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Core;

/// <summary>
/// Turns a test method name into the human-readable scenario heading a trace
/// renders under.
/// </summary>
/// <remarks>
/// Pure string formatting with no knowledge of any test framework — hand it
/// whatever display name the framework reports. Deliberately mirrors the Java
/// edition's <c>humanize</c> so the same test produces the same heading in both
/// ports; changing the wording rules here breaks that parity.
/// </remarks>
public static class ScenarioFramer
{
    private static readonly string[] Prefixes = ["Test_", "Should_"];

    private static readonly Regex TrailingArgs =
        new(@"\([^)]*\)$", RegexOptions.CultureInvariant);

    /// <summary>Formats a test name as a <c>"Scenario: ..."</c> heading.</summary>
    /// <param name="testName">
    /// The test's name or display name. A leading <c>Test_</c> or <c>Should_</c>
    /// is stripped before humanizing; only those two, and only at the start.
    /// </param>
    /// <returns>The heading, always prefixed with <c>"Scenario: "</c> even when the humanized remainder is empty.</returns>
    /// <example>
    /// <code>
    /// ScenarioFramer.Frame("Should_RejectExpiredCard");
    /// // "Scenario: Reject expired card"
    /// </code>
    /// </example>
    public static string Frame(string testName)
    {
        return "Scenario: " + Humanize(StripPrefix(testName));
    }

    /// <summary>
    /// Converts an identifier-shaped name into a readable sentence, without the
    /// <c>"Scenario: "</c> prefix.
    /// </summary>
    /// <param name="displayName">
    /// The name to humanize. A trailing parenthesized argument list — the
    /// <c>(Int32, String)</c> a parameterized test appends — is removed first.
    /// </param>
    /// <returns>
    /// The humanized phrase, capitalized. Empty input, or input that is nothing
    /// but a parenthesized suffix, yields the empty string rather than throwing.
    /// </returns>
    /// <remarks>
    /// <b>A name that already contains a space is passed through untouched</b>,
    /// on the assumption the framework supplied prose rather than an identifier.
    /// So this is not idempotent in the usual sense: its own output, being
    /// spaced, is returned unchanged on a second pass — which is intended, but
    /// also means a single embedded space disables all splitting and
    /// capitalization for the whole name. Otherwise underscores become spaces
    /// and camelCase boundaries are split, with acronyms left grouped
    /// (<c>HTTPServer</c> → <c>httpserver</c>, not <c>h t t p server</c>).
    /// </remarks>
    // Mirrors Java humanize: strip a trailing (...), pass already-spaced names
    // through untouched, else split camel/underscore words and capitalize.
    public static string Humanize(string displayName)
    {
        var name = TrailingArgs.Replace(displayName, string.Empty);
        if (name.Length == 0)
        {
            return string.Empty;
        }

        if (name.IndexOf(' ') >= 0)
        {
            return name;
        }

        var phrase = CamelCaseSplitter.ToPhrase(name.Replace('_', ' '));
        return Capitalize(phrase);
    }

    private static string Capitalize(string phrase)
    {
        return phrase.Length == 0
            ? phrase
            : char.ToUpperInvariant(phrase[0]) + phrase[1..];
    }

    private static string StripPrefix(string name)
    {
        var match = Prefixes.FirstOrDefault(
            p => name.StartsWith(p, StringComparison.Ordinal));
        return match is not null
            ? name[match.Length..]
            : name;
    }
}
