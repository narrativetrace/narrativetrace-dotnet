// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Recognizes a small, deliberately narrow set of secret value shapes —
/// independent of any field name — for <see cref="RedactionPolicy.ShouldRedactValue"/>.
/// </summary>
/// <remarks>
/// Every shape here is structural (a format or a checksum), never a
/// statistical guess: an entropy or "looks random" heuristic would blank
/// values the reader has no way to inspect or override. The one accepted
/// false positive is inherent to detecting payment cards at all — a
/// card-length numeric identifier that happens to satisfy Luhn (about 1 in
/// 10) masks even though it is not a real PAN; an identifier that fails
/// Luhn (e.g. an ordinary order number) is required to stay visible.
/// <para>
/// The JWT, PAN and <c>Set-Cookie</c> checks below are hand-written to
/// reject a non-matching value (the overwhelming majority of strings a
/// trace ever renders) without allocating: this runs on every rendered
/// string scalar, a path <c>CoreBenchmarks.RenderObject</c> holds to a
/// zero-allocation-regression budget. A
/// <see cref="System.Text.RegularExpressions.Regex"/> or a
/// <see cref="string.Split(char[])"/> would pay for that on every call
/// instead of only on an actual match.
/// </para>
/// <para>
/// National identity numbers are the fourth shape and live in
/// <see cref="NationalIdShapes"/>, kept apart because they are six
/// checksums rather than one matcher, each gated by a cheap
/// <see cref="System.Text.RegularExpressions.Regex"/> before any
/// arithmetic runs. They are language-neutral by construction — a CPF is
/// a CPF whatever the field holding it is called, which is the point: the
/// name deny-list can be read in a language it was not written in, and a
/// checksum cannot.
/// </para>
/// </remarks>
internal static class SecretValueShapes
{
    private static readonly string[] CookieAttributes =
    [
        "path=", "domain=", "expires=", "max-age=", "secure", "httponly", "samesite=",
    ];

    private const char MaxAscii = (char)0x7f;

    /// <summary>Whether <paramref name="value"/> matches any recognized secret shape.</summary>
    public static bool Matches(string value)
    {
        return IsJwt(value)
            || IsPan(value)
            || IsSetCookieValue(value)
            || NationalIdShapes.IsNationalId(value);
    }

    /// <summary>
    /// Folds a field name or a deny-list pattern to the one spelling both are
    /// compared in: lower case, and without diacritics.
    /// </summary>
    /// <remarks>
    /// The deny-list carries every language's vocabulary, and a Spanish team
    /// writes <c>contraseña</c> while the same team's DTO generator writes
    /// <c>contrasena</c>. Both are the word "password" and both must be
    /// hidden, so the fold happens on <b>both</b> sides rather than the
    /// pattern set listing every spelling. The ASCII scan in front is not
    /// premature: virtually every real field name is ASCII, where
    /// <see cref="string.ToLowerInvariant()"/> alone already answers, and
    /// normalization is only paid for by names that actually carry a
    /// non-ASCII character.
    /// <para>
    /// Total on any input: an unpaired surrogate or a noncharacter folds to
    /// something harmless rather than throwing out of a redaction decision —
    /// the one place an exception would fail open.
    /// </para>
    /// </remarks>
    internal static string Canonical(string text)
    {
        var lower = text.ToLowerInvariant();
        return IsAscii(lower) ? lower : WithoutDiacritics(lower);
    }

    private static bool IsAscii(string text)
    {
        foreach (var c in text)
        {
            if (c > MaxAscii)
            {
                return false;
            }
        }

        return true;
    }

    // NFD splits a letter from its accent; dropping the combining marks
    // (Unicode general category M — Mn/Mc/Me, matching Java's \p{M}) leaves
    // the bare letter.
    private static string WithoutDiacritics(string lower)
    {
        var normalized = lower.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (!IsCombiningMark(c))
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static bool IsCombiningMark(char c)
    {
        var category = CharUnicodeInfo.GetUnicodeCategory(c);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    // Three base64url segments, the first starting "eyJ" — the base64url
    // encoding of a JSON object's opening brace and quote. Every real JWT
    // header has it; a dotted hostname or an "a.b.c" value does not. The
    // trailing segment may be empty (an "alg: none" unsecured JWT has an
    // empty signature).
    internal static bool IsJwt(string value)
    {
        if (!value.StartsWith("eyJ", StringComparison.Ordinal))
        {
            return false;
        }

        var firstDot = value.IndexOf('.');
        var secondDot = firstDot < 0 ? -1 : value.IndexOf('.', firstDot + 1);
        return secondDot >= 0
            && value.IndexOf('.', secondDot + 1) < 0
            && IsBase64UrlSegment(value, 0, firstDot, allowEmpty: false)
            && IsBase64UrlSegment(value, firstDot + 1, secondDot, allowEmpty: false)
            && IsBase64UrlSegment(value, secondDot + 1, value.Length, allowEmpty: true);
    }

    private static bool IsBase64UrlSegment(
        string value, int start, int end, bool allowEmpty)
    {
        if (end == start)
        {
            return allowEmpty;
        }

        for (var i = start; i < end; i++)
        {
            var c = value[i];
            if (!char.IsLetterOrDigit(c) && c is not ('-' or '_'))
            {
                return false;
            }
        }

        return true;
    }

    // 13-19 digits (the PAN length range ISO/IEC 7812 allows), [ -] separators
    // tolerated, and Luhn-valid — a structural checksum, not a length guess.
    internal static bool IsPan(string value)
    {
        return CountPanDigits(value) is { } count
            && count is >= 13 and <= 19
            && PassesLuhn(value);
    }

    // Rejects any character outside digits/space/hyphen while counting, so a
    // sentence that merely contains 13+ digits (e.g. a UUID or a phone number
    // with letters) never reaches the Luhn check, and never allocates.
    private static int? CountPanDigits(string value)
    {
        var digitCount = 0;
        foreach (var c in value)
        {
            if (char.IsDigit(c))
            {
                digitCount++;
            }
            else if (c is not (' ' or '-'))
            {
                return null;
            }
        }

        return digitCount;
    }

    private static bool PassesLuhn(string value)
    {
        var sum = 0;
        var doubleDigit = false;
        for (var i = value.Length - 1; i >= 0; i--)
        {
            if (!char.IsDigit(value[i]))
            {
                continue;
            }

            sum += LuhnDigit(value[i] - '0', doubleDigit);
            doubleDigit = !doubleDigit;
        }

        return sum % 10 == 0;
    }

    private static int LuhnDigit(int digit, bool doubleDigit)
    {
        if (!doubleDigit)
        {
            return digit;
        }

        var doubled = digit * 2;
        return doubled > 9 ? doubled - 9 : doubled;
    }

    // A name=value pair followed by at least one named RFC 6265 cookie
    // attribute — "key=value" alone, or "name=Ada; age=36" with no attribute
    // keyword, both stay visible.
    internal static bool IsSetCookieValue(string value)
    {
        return HasLeadingNameValuePair(value) && HasNamedCookieAttribute(value);
    }

    private static bool HasLeadingNameValuePair(string value)
    {
        var end = value.IndexOf(';');
        var segmentLength = end < 0 ? value.Length : end;
        var eq = value.IndexOf('=', 0, segmentLength);
        return eq > 0 && eq < segmentLength - 1;
    }

    // Reached only once a name=value pair is already confirmed present, so
    // this is not on the common "ordinary string" fast-reject path.
    private static bool HasNamedCookieAttribute(string value)
    {
        var lower = value.ToLowerInvariant();
        return CookieAttributes.Any(lower.Contains);
    }
}
