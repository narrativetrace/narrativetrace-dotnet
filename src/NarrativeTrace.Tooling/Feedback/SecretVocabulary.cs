// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The names and the credential prefixes the value-free gate refuses — the DATA half of
/// <see cref="ValueFreeRule"/>'s name and shape rules.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: the runtime's own deny-list (<c>SensitiveVocabulary</c>, five languages, behind
/// <c>RedactionPolicy</c>) is the authority on what a sensitive field is called. This library
/// declares zero dependencies and may never link against that runtime (see the tooling-library
/// architecture gate), so the vocabulary is restated here as data and the cross-module security
/// suite asserts the only implication that matters: every name the runtime redacts is also refused
/// here. Drift is a build failure, not a leak discovered in a public issue.
/// </para>
/// <para>
/// <b>@llmNote</b> Matching here is COARSER than the runtime's on purpose. The runtime distinguishes
/// substring terms from identifier-token terms because blanking <c>circuitBreaker</c> is how a team
/// switches redaction off entirely; this gate blanks nothing — it refuses to file a report and names
/// the rule — so a near miss costs a sentence, and the only expensive mistake is the one that files a
/// credential.
/// </para>
/// </remarks>
internal static class SecretVocabulary
{
    /// <summary>
    /// Every deny-listed term, already canonical (lower case, no diacritics). Matched as a substring
    /// of the canonical form of whatever key is being assigned a value.
    /// </summary>
    private static readonly string[] Terms =
    [
        // credentials
        "password",
        "passwd",
        "passphrase",
        "contrasena",
        "claveacceso",
        "clave_acceso",
        "clavesecreta",
        "clave_secreta",
        "senha",
        "motdepasse",
        "mot_de_passe",
        "passwort",
        "kennwort",
        "密码",
        "mima",
        "token",
        "apikey",
        "api_key",
        "accesskey",
        "access_key",
        "bearer",
        "secret",
        "credential",
        "privatekey",
        "private_key",
        "authorization",
        "otp",
        "mfacode",
        "mfa_code",
        "totp",
        // session
        "sessionid",
        "session_id",
        "cookie",
        "setcookie",
        "set_cookie",
        "jwt",
        // payment
        "cardnumber",
        "card_number",
        "pan",
        "tarjeta",
        "cartao",
        "cartebancaire",
        "carte_bancaire",
        "numerocarte",
        "numero_carte",
        "cvv",
        "accountnumber",
        "account_number",
        "routingnumber",
        "routing_number",
        "iban",
        // national ids
        "ssn",
        "socialsecurity",
        "social_security",
        "socialsecuritynumber",
        "taxid",
        "tax_id",
        "rut",
        "cuit",
        "dni",
        "cedula",
        "cpf",
        "cnpj",
        "nir",
        "身份证",
        "shenfenzheng",
    ];

    /// <summary>
    /// The shapes a value gives itself away by, STRUCTURALLY — no checksum is verified here.
    /// </summary>
    /// <remarks>
    /// <b>@llmNote</b> Deliberately not a copy of the runtime's checksum-exact matchers. Copying nine
    /// national-id check-digit algorithms into a library that may not link the one that already has
    /// them is how two implementations start disagreeing; a shape that is a SUPERSET of theirs cannot.
    /// The cross-module security suite proves the superset over the shared corpus, and the price is
    /// paid in the right currency: a checksum-failing lookalike is refused with a named rule instead
    /// of being filed.
    /// </remarks>
    private static readonly Regex[] Shapes =
    [
        // A JWT, anchored on the base64url encoding of a JSON object's opening — `{"`.
        new(@"\beyJ[A-Za-z0-9_-]{6,}\.[A-Za-z0-9_-]{6,}", RegexOptions.Compiled),
        // A PEM private key block header.
        new("-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.Compiled),
        // Named credential prefixes: the four whose shape is the credential.
        new(@"\bghp_[A-Za-z0-9]{8,}", RegexOptions.Compiled),
        new(@"\bgithub_pat_[A-Za-z0-9_]{8,}", RegexOptions.Compiled),
        new(@"\bsk-[A-Za-z0-9_-]{8,}", RegexOptions.Compiled),
        new(@"\bAKIA[0-9A-Z]{12,}", RegexOptions.Compiled),
        new(@"\bxox[abprs]-[A-Za-z0-9-]{8,}", RegexOptions.Compiled),
        // A Chilean RUT, printed with or without its thousands dots.
        new(@"\b\d{1,2}\.?\d{3}\.?\d{3}-[\dkK]\b", RegexOptions.Compiled),
        // A Brazilian CPF.
        new(@"\b\d{3}\.?\d{3}\.?\d{3}-?\d{2}\b", RegexOptions.Compiled),
        // A Brazilian CNPJ.
        new(@"\b\d{2}\.?\d{3}\.?\d{3}/?\d{4}-?\d{2}\b", RegexOptions.Compiled),
        // A Spanish DNI or NIE.
        new(@"\b[XYZxyz]?\d{7,8}-?[A-Za-z]\b", RegexOptions.Compiled),
        // A French NIR, bare or in the spaced form a card is printed in, Corsica included.
        new(@"\b[12]\s?\d{2}\s?\d{2}\s?(\d{2}|\d?[AB])\s?\d{3}\s?\d{3}\s?\d{2}\b", RegexOptions.Compiled),
        // A Chinese resident identity card.
        new(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled),
        // A payment card number's digit length.
        new(@"\b\d{13,19}\b", RegexOptions.Compiled),
    ];

    private static readonly Regex CombiningMarks = new(@"\p{M}+", RegexOptions.Compiled);

    /// <summary>Whether any secret-shaped value appears anywhere in this text.</summary>
    internal static bool ContainsASecretShape(string text) =>
        Array.Exists(Shapes, shape => shape.IsMatch(text));

    /// <summary>Whether the canonical form of this key contains any deny-listed term.</summary>
    internal static bool NamesASecret(string key)
    {
        var canonical = Canonical(key);
        return Array.Exists(Terms, canonical.Contains);
    }

    /// <summary>
    /// Lower case with diacritics folded away — <c>contraseña</c>, <c>contrasena</c> and the
    /// decomposed <c>contraseña</c> a Mac filesystem hands back all become one string. Separators are
    /// deliberately NOT stripped, which is why <c>api_key</c> and <c>apikey</c> are two terms above
    /// rather than one.
    /// </summary>
    internal static string Canonical(string text)
    {
        var lower = text.ToLowerInvariant();
        return CombiningMarks.Replace(lower.Normalize(NormalizationForm.FormD), "");
    }
}
