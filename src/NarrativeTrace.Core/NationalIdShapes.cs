// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Core;

/// <summary>
/// Recognizes national identity numbers by their own check digits, whatever
/// the field is called.
/// </summary>
/// <remarks>
/// <see cref="SecretValueShapes"/> already answers "are these bytes a
/// credential?" for a JWT, a card number and a <c>Set-Cookie</c>. A national
/// identity number is the same question in a different jurisdiction, and it
/// is the one piece of sensitive data whose <em>name</em> is most often in a
/// language the deny-list is read in but not written in — <c>numero</c>,
/// <c>documento</c>, <c>id</c>. The value's own checksum does not care what
/// language the field name is in, which is why these shapes are
/// language-neutral and on by default for everyone.
/// <para>
/// <b>@llmNote</b> Every matcher here is a checksum, never a
/// length-and-digits guess, and every one is gated by a cheap pattern before
/// any arithmetic runs — the same discipline the Luhn PAN matcher uses. A
/// scheme without a check digit (the pre-1999 15-digit Chinese id, a bare
/// Spanish DNI with the letter dropped) is deliberately absent: it would be
/// indistinguishable from an order number, and a security default that
/// blanks ordinary business fields is one teams switch off entirely.
/// </para>
/// <para>
/// <b>@edgeCase</b> The Chilean RUT requires its verifier separator. Chile
/// writes a RUT as <c>12.345.678-5</c> or <c>12345678-5</c>, and the dash is
/// what distinguishes it from any other eight-digit number; accepting a bare
/// nine-digit run would redact roughly one in eleven of every order and
/// invoice number in the world, which is the false-positive budget this
/// class exists to avoid. Dots are optional, the dash is not.
/// </para>
/// <para>
/// <b>@edgeCase</b> CPF and CNPJ reject repeated-digit strings
/// (<c>00000000000</c>, <c>11111111111</c>) before the checksum, because
/// every one of them satisfies both check digits and none of them is a real
/// document — they are the placeholder a form writes when it has none.
/// </para>
/// </remarks>
internal static class NationalIdShapes
{
    /// <summary>Chile: 7-8 digits, optional thousands dots, and a mod-11 verifier that may be <c>K</c>.</summary>
    private static readonly Regex Rut =
        new(@"^(?:\d{1,2}\.\d{3}\.\d{3}|\d{7,8})-[0-9kK]$", RegexOptions.Compiled);

    /// <summary>Brazil: 11 digits, written bare or as <c>NNN.NNN.NNN-NN</c>.</summary>
    private static readonly Regex Cpf =
        new(@"^(?:\d{3}\.\d{3}\.\d{3}-\d{2}|\d{11})$", RegexOptions.Compiled);

    /// <summary>Brazil: 14 digits, written bare or as <c>NN.NNN.NNN/NNNN-NN</c>.</summary>
    private static readonly Regex Cnpj =
        new(@"^(?:\d{2}\.\d{3}\.\d{3}/\d{4}-\d{2}|\d{14})$", RegexOptions.Compiled);

    /// <summary>United States: <c>AAA-GG-SSSS</c>, dashes required.</summary>
    /// <remarks>
    /// <b>@edgeCase</b> The dashes are the entire signal and the reason this
    /// pattern is safe. An SSN carries no check digit, so nine bare digits
    /// are arithmetically indistinguishable from an order number, an account
    /// id or an unpunctuated phone number — matching those would blank
    /// ordinary business data in every trace, exactly the false-positive
    /// budget this class exists to protect. Punctuation is the only evidence
    /// the writer meant an SSN.
    /// </remarks>
    private static readonly Regex Ssn =
        new(@"^\d{3}-\d{2}-\d{4}$", RegexOptions.Compiled);

    /// <summary>Spain: a DNI is 8 digits plus a letter; a NIE swaps the leading digit for <c>X/Y/Z</c>.</summary>
    private static readonly Regex SpanishId =
        new(@"^(?:[XYZ]\d{7}|\d{8})-?[A-Z]$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// France: 13-character body plus a 2-digit key. Only the department
    /// (positions 6-7) may be non-numeric, and only as Corsica's <c>2A</c>/<c>2B</c>.
    /// </summary>
    private static readonly Regex Nir =
        new(@"^[1-478]\d{4}(?:\d{2}|2[AB])\d{8}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>China: the post-1999 resident identity card, 17 digits and a check character.</summary>
    private static readonly Regex ChineseId = new(@"^\d{17}[0-9Xx]$", RegexOptions.Compiled);

    private static readonly Regex Punctuation = new(@"[.\-/]", RegexOptions.Compiled);
    private static readonly Regex Space = new(" ", RegexOptions.Compiled);

    /// <summary>The Spanish check letter, indexed by the document number modulo 23.</summary>
    private const string DniLetters = "TRWAGMYFPDXBNJZSQVHLCKE";

    /// <summary>A NIE's leading letter stands for a digit: <c>X</c>=0, <c>Y</c>=1, <c>Z</c>=2.</summary>
    private const string NiePrefixes = "XYZ";

    private static readonly int[] ChineseWeights =
    [
        7, 9, 10, 5, 8, 4, 2, 1, 6, 3, 7, 9, 10, 5, 8, 4, 2,
    ];

    /// <summary>The Chinese check character, indexed by the weighted sum modulo 11.</summary>
    private const string ChineseCheckCharacters = "10X98765432";

    private const int EarliestBirthYear = 1900;
    private const int LatestBirthYear = 2100;

    /// <summary>
    /// Whether the value is a national identity number that passes its own
    /// checksum: a valid Chilean RUT, Brazilian CPF or CNPJ, Spanish DNI or
    /// NIE, French NIR, Chinese resident identity card, or a dashed US
    /// Social Security number.
    /// </summary>
    /// <param name="value">A trimmed rendered value.</param>
    internal static bool IsNationalId(string value)
    {
        return IsRut(value)
            || IsCpf(value)
            || IsCnpj(value)
            || IsSpanishId(value)
            || IsFrenchNir(value)
            || IsChineseResidentId(value)
            || IsUsSsn(value);
    }

    /// <summary>
    /// A US Social Security number written in the dashed form
    /// <c>AAA-GG-SSSS</c>.
    /// </summary>
    /// <remarks>
    /// The name deny-list carries <c>ssn</c> and, since 2026-09-10, its
    /// longer spellings — but a real SSN arriving under an innocuous name
    /// (<c>taxpayerRef</c>, <c>identifier</c>) was caught by nothing at
    /// all. An audit found the concept covered by name in one language and
    /// by no shape whatsoever; this is that missing axis.
    /// <para>
    /// <b>@edgeCase</b> This is the one matcher here that is not a
    /// checksum, because the scheme has none. The structural rules the SSA
    /// publishes stand in for one: area <c>000</c>, <c>666</c> and
    /// <c>900-999</c> are never issued, group <c>00</c> never is, and
    /// serial <c>0000</c> never is. Rejecting those keeps
    /// <c>000-00-0000</c> — the placeholder that fills test fixtures and
    /// redacted forms everywhere — visible rather than blanked, and costs
    /// nothing real: those combinations cannot be anyone's number.
    /// </para>
    /// </remarks>
    private static bool IsUsSsn(string value)
    {
        if (!Ssn.IsMatch(value))
        {
            return false;
        }

        var area = value[..3];
        var group = value.Substring(4, 2);
        var serial = value[7..];
        if (area is "000" or "666" || area[0] == '9')
        {
            return false;
        }

        return group != "00" && serial != "0000";
    }

    private static bool IsRut(string value)
    {
        if (!Rut.IsMatch(value))
        {
            return false;
        }

        var compact = Punctuation.Replace(value, string.Empty);
        var body = compact[..^1];
        return char.ToLowerInvariant(compact[^1]) == RutVerifier(body);
    }

    /// <summary>Chile's mod 11: weights 2..7 cycling from the right, <c>10</c> written <c>K</c>.</summary>
    private static char RutVerifier(string body)
    {
        var sum = 0;
        var weight = 2;
        for (var i = body.Length - 1; i >= 0; i--)
        {
            sum += (body[i] - '0') * weight;
            weight = weight == 7 ? 2 : weight + 1;
        }

        var rest = 11 - (sum % 11);
        if (rest == 11)
        {
            return '0';
        }

        return rest == 10 ? 'k' : (char)('0' + rest);
    }

    private static bool IsCpf(string value)
    {
        if (!Cpf.IsMatch(value))
        {
            return false;
        }

        var digits = Punctuation.Replace(value, string.Empty);
        return !IsRepeatedDigit(digits)
            && CpfCheckDigit(digits, 9) == digits[9] - '0'
            && CpfCheckDigit(digits, 10) == digits[10] - '0';
    }

    /// <summary>Brazil's mod 11 for the CPF: weights count down from <c>length + 1</c> to 2.</summary>
    private static int CpfCheckDigit(string digits, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
        {
            sum += (digits[i] - '0') * (length + 1 - i);
        }

        return CheckDigitFromRemainder(sum);
    }

    private static bool IsCnpj(string value)
    {
        if (!Cnpj.IsMatch(value))
        {
            return false;
        }

        var digits = Punctuation.Replace(value, string.Empty);
        return !IsRepeatedDigit(digits)
            && CnpjCheckDigit(digits, 12) == digits[12] - '0'
            && CnpjCheckDigit(digits, 13) == digits[13] - '0';
    }

    /// <summary>Brazil's mod 11 for the CNPJ: weights 2..9 cycling from the right.</summary>
    private static int CnpjCheckDigit(string digits, int length)
    {
        var sum = 0;
        var weight = 2;
        for (var i = length - 1; i >= 0; i--)
        {
            sum += (digits[i] - '0') * weight;
            weight = weight == 9 ? 2 : weight + 1;
        }

        return CheckDigitFromRemainder(sum);
    }

    /// <summary>Both Brazilian schemes share the final step: a remainder below two means a zero digit.</summary>
    private static int CheckDigitFromRemainder(int sum)
    {
        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }

    private static bool IsRepeatedDigit(string digits)
    {
        for (var i = 1; i < digits.Length; i++)
        {
            if (digits[i] != digits[0])
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsSpanishId(string value)
    {
        if (!SpanishId.IsMatch(value))
        {
            return false;
        }

        var upper = Punctuation.Replace(value.ToUpperInvariant(), string.Empty);
        var body = upper[..^1];
        var prefix = NiePrefixes.IndexOf(body[0]);
        var number = prefix < 0 ? body : prefix.ToString(CultureInfo.InvariantCulture) + body[1..];
        return DniLetters[int.Parse(number, CultureInfo.InvariantCulture) % 23] == upper[^1];
    }

    private static bool IsFrenchNir(string value)
    {
        var compact = Space.Replace(value, string.Empty);
        if (!Nir.IsMatch(compact))
        {
            return false;
        }

        var body = compact[..13]
            .ToUpperInvariant()
            .Replace("2A", "19")
            .Replace("2B", "18");
        var key = int.Parse(compact[13..], CultureInfo.InvariantCulture);
        var bodyValue = long.Parse(body, CultureInfo.InvariantCulture);
        return key == 97 - (bodyValue % 97);
    }

    private static bool IsChineseResidentId(string value)
    {
        if (!ChineseId.IsMatch(value) || !HasPlausibleBirthDate(value))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < ChineseWeights.Length; i++)
        {
            sum += (value[i] - '0') * ChineseWeights[i];
        }

        return ChineseCheckCharacters[sum % 11] == char.ToUpperInvariant(value[17]);
    }

    /// <summary>
    /// Positions 7-14 of a Chinese resident id are the holder's birth date.
    /// Checking it removes most of what the check character alone would let
    /// through — a one-in-eleven hit rate on eighteen-digit numbers is
    /// otherwise the whole false-positive budget.
    /// </summary>
    private static bool HasPlausibleBirthDate(string value)
    {
        var year = int.Parse(value.Substring(6, 4), CultureInfo.InvariantCulture);
        var month = int.Parse(value.Substring(10, 2), CultureInfo.InvariantCulture);
        var day = int.Parse(value.Substring(12, 2), CultureInfo.InvariantCulture);
        return year is >= EarliestBirthYear and <= LatestBirthYear
            && month is >= 1 and <= 12
            && day is >= 1 and <= 31;
    }
}
