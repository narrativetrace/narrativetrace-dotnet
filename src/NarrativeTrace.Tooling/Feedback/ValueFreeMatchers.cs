// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The deciding half of <see cref="ValueFreeRule"/>: one predicate per rule, each testable on its
/// own without building a report around it.
/// </summary>
/// <remarks>
/// <b>@llmNote</b> Every pattern is compiled once, at class initialisation. A rule runs over every
/// field of every report the verb drafts, so a per-call compile would be paid on the one path that
/// must never be the slow one a user skips.
/// </remarks>
internal static class ValueFreeMatchers
{
    /// <summary>
    /// <c>Name.method(param: value</c> — an identifier, a colon and something after it, inside a call
    /// line's parentheses. The colon is what separates a rendered call from a structural one: the
    /// <c>.nt</c> form writes <c>Name.method(param, param)</c> and never a value.
    /// </summary>
    /// <remarks>
    /// <b>@edgeCase</b> Every runtime's Markdown renderer bolds the qualified name
    /// (<c>- **Name.method**(param: value)</c>), so one optional emphasis marker (<c>**</c>, <c>__</c>,
    /// <c>*</c>, <c>_</c>) may sit between the method and the parenthesis — written as a bounded class
    /// (<c>[*_]{0,2}</c>), never as the alternation <c>(?:\*\*|__|\*|_)?</c>: overlapping alternatives
    /// are the shape a ReDoS gate rejects, and a stray mixed pair the class also admits only makes this
    /// deny rule stricter.
    /// </remarks>
    private static readonly Regex RenderedCallPattern =
        new(@"\w+\.\w+[*_]{0,2}\([^)]*\b\w+:\s*\S", RegexOptions.Compiled);

    /// <summary>
    /// An outcome arrow followed by something other than the structural literal <c>value</c>. The
    /// other two structural outcomes carry no arrow at all (<c>!! TypeName</c>, <c>?? incomplete</c>),
    /// so they cannot reach this pattern; a rendered return always does.
    /// </summary>
    private static readonly Regex RenderedOutcomePattern =
        new(@"→\s*(?!value\b)\S", RegexOptions.Compiled);

    /// <summary>
    /// The rendered narrative's own duration suffix: an em dash, a number and a time unit. Anchored
    /// on the em dash rather than on the number alone, so a version coordinate and a finding count —
    /// the two numbers a legitimate report is full of — are not durations.
    /// </summary>
    private static readonly Regex DurationPattern =
        new(@"—\s*\d+(\.\d+)?\s?(ns|µs|ms|s)\b", RegexOptions.Compiled);

    /// <summary>
    /// The runtime's redaction marker, written out rather than imported: this library declares zero
    /// dependencies and never links against the runtime it diagnoses. The cross-module security suite
    /// asserts this literal equals <c>RedactionPolicy.Marker</c>, so the two cannot drift.
    /// </summary>
    internal const string RedactionMarker = "[REDACTED]";

    /// <summary>
    /// One <c>key: value</c> or <c>key=value</c> binding, capturing the key. Keys are matched so a
    /// Spanish or Chinese field name is a word here too (.NET <c>\w</c> is Unicode by default), and
    /// bounded in length so a pathological line cannot make this quadratic.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>@llmNote</b> The value is a LOOKAHEAD, not a consumed character, and that is not a
    /// micro-optimisation. Consuming it ate the first letter of the next key, so in an indented YAML
    /// paste the scan matched <c>datasource:</c> and then resumed inside <c>password</c>, read the key
    /// as <c>assword</c> and let the credential through. Corpus row <c>named-secret-yaml-indented</c>
    /// is that bug.
    /// </para>
    /// <para>
    /// <b>@llmNote</b> The optional quotes are load-bearing for the SAME reason the lookahead is. A
    /// JSON key is <c>"password": "hunter2"</c>, and the closing quote sits between the key and the
    /// colon — so without them the one attachment every report carries, the doctor's own JSON, was the
    /// one format the deny-list could not read at all. Corpus row <c>named-secret-json-quoted</c> is
    /// that bug.
    /// </para>
    /// </remarks>
    private static readonly Regex KeyedAssignmentPattern =
        new(@"[""']?([\w.\-]{1,80})[""']?\s*[:=]\s*(?=\S)", RegexOptions.Compiled);

    /// <summary>
    /// A run of base64 (and base64url <c>_</c>) characters long enough to be an encoded secret.
    /// </summary>
    /// <remarks>
    /// <b>@llmNote</b> The hyphen is deliberately NOT a run character, though base64url uses it: it is
    /// the one character ordinary prose and this project's own doc anchors use as a word separator, and
    /// admitting it makes every hyphenated phrase of 32 characters a secret
    /// (<c>the-quick-brown-fox-jumps-over-the-lazy</c> measures 4.33 bits per character). A hyphenated
    /// base64url token is still reached by its halves, and a JWT by <see cref="SecretVocabulary"/>'s
    /// own shape.
    /// </remarks>
    private static readonly Regex Base64RunPattern =
        new("[A-Za-z0-9+/=_]{32,}", RegexOptions.Compiled);

    /// <summary>
    /// A run of nothing but hex digits, long enough to be a hash, an HMAC or an opaque id.
    /// </summary>
    /// <remarks>
    /// <b>@llmNote</b> Entropy is not the discriminator for hex and cannot be: sixteen symbols cap
    /// Shannon entropy at 4.0 bits per character, and a measured sample of random hex runs of 32 to
    /// 128 characters never exceeded 3.97 — so the entropy ceiling below can never fire on hex, which
    /// is half of what this rule is for. Length alone carries the hex half: no word is 32 hex digits.
    /// </remarks>
    private static readonly Regex HexRunPattern =
        new(@"\b[0-9a-fA-F]{32,}\b", RegexOptions.Compiled);

    /// <summary>Bits per character above which an encoded run is treated as a secret rather than a word.</summary>
    private const double EntropyCeiling = 4.0;

    /// <summary>
    /// An email address. The local part must be non-empty, which is what keeps a C# attribute
    /// (<c>[NotTraced]</c>, or a Java <c>@NotTraced</c> a cross-port report quotes) and a package
    /// coordinate off this rule.
    /// </summary>
    private static readonly Regex EmailPattern =
        new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    /// <summary>
    /// The three platforms' home directories. Matched with a trailing separator and a non-empty
    /// account segment, so <c>/home</c> or a relative <c>build/</c> path is not one.
    /// </summary>
    private static readonly Regex HomePathPattern =
        new(@"(/Users/|/home/)[^/\s]+/|[A-Za-z]:\\Users\\[^\\\s]+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Every C0 and C1 control character except the line feed and the tab a report legitimately has,
    /// plus Unicode's own bidirectional controls and the byte-order mark.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>@llmNote</b> The bidi characters are in scope because they are the attack this rule is for:
    /// a right-to-left override in an issue title reorders what the person triaging it SEES without
    /// changing a byte of what was filed. They carry the <c>Bidi_Control</c> property, so "control
    /// character" is their own name for themselves, not a widening.
    /// </para>
    /// <para>
    /// <b>@edgeCase</b> Deliberately NOT all of <c>\p{Cf}</c>. The zero-width joiner and non-joiner are
    /// format characters too, and they are load-bearing letters in Devanagari, Bengali and emoji
    /// sequences — refusing them would refuse a report written in Hindi, and reports may be in any
    /// language.
    /// </para>
    /// </remarks>
    private static readonly Regex ControlPattern =
        new("[\\x00-\\x08\\x0b-\\x1f\\x7f-\\x9f\\u202a-\\u202e\\u2066-\\u2069\\ufeff]",
            RegexOptions.Compiled);

    internal static bool RenderedCall(string text) => RenderedCallPattern.IsMatch(text);

    internal static bool RenderedOutcome(string text) => RenderedOutcomePattern.IsMatch(text);

    internal static bool Duration(string text) => DurationPattern.IsMatch(text);

    internal static bool Marker(string text) => text.Contains(RedactionMarker, StringComparison.Ordinal);

    /// <summary>
    /// A deny-listed name immediately followed by a value. Anchored on the key being ASSIGNED, not on
    /// the word appearing anywhere: a report that says "the authorization header never arrived" is the
    /// report we want, and the key there is <c>header</c>.
    /// </summary>
    internal static bool NamedSecret(string text) =>
        KeyedAssignmentPattern.Matches(text).Any(match => SecretVocabulary.NamesASecret(match.Groups[1].Value));

    internal static bool ValueShape(string text) => SecretVocabulary.ContainsASecretShape(text);

    /// <summary>The hex half decided by length, the base64 half by the density of its own alphabet.</summary>
    internal static bool Entropy(string text) =>
        HexRunPattern.IsMatch(text)
        || Base64RunPattern.Matches(text).Any(run => ShannonBitsPerCharacter(run.Value) > EntropyCeiling);

    internal static bool Email(string text) => EmailPattern.IsMatch(text);

    internal static bool HomePath(string text) => HomePathPattern.IsMatch(text);

    internal static bool Control(string text) => ControlPattern.IsMatch(text);

    /// <summary>
    /// Shannon entropy of the run's own character distribution, in bits per character. The run is
    /// ASCII by construction of the patterns above, which is what lets the histogram be an array.
    /// </summary>
    private static double ShannonBitsPerCharacter(string run)
    {
        var counts = new int[128];
        foreach (var c in run)
        {
            counts[c]++;
        }

        return counts.Where(count => count > 0).Sum(count =>
        {
            var share = (double)count / run.Length;
            return -share * (Math.Log(share) / Math.Log(2));
        });
    }
}
