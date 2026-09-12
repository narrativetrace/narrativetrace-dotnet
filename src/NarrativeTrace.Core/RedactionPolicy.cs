// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Name-based deny-list deciding whether a reflectively-introspected
/// field value must be hidden, plus a second, name-independent axis that
/// recognizes a handful of secret <em>value shapes</em> regardless of what
/// the field holding them is called.
/// </summary>
/// <remarks>
/// Reflective introspection is sensitive-data-by-default: an object
/// without a curated <c>ToString()</c> otherwise leaks every field value
/// into traces, logs, and exports. This policy redacts values whose
/// <em>field name</em> matches a known-sensitive pattern, so secrets
/// nested inside DTOs are hidden without an annotation on every one.
/// Matching is a case- and accent-insensitive substring test — deliberately
/// erring toward over-redaction, which is the safe default for a security
/// control (<c>userPassword</c>, <c>cardCvv</c>, <c>apiToken</c> all
/// match) — <b>except</b> a handful of short patterns (<c>pan</c>,
/// <c>iban</c>, and the non-English short words below), which would redact
/// ordinary business fields as substrings (<c>companyName</c>,
/// <c>expansionRatio</c>, <c>panelId</c>, <c>spanCount</c>, <c>planId</c>,
/// <c>japaneseAddress</c>) and are matched on identifier-token boundaries
/// instead — see <see cref="ShouldRedact"/>.
/// <para>
/// The default vocabulary is <b>multilingual and always on</b> — Spanish,
/// Portuguese, French, German and Chinese words sit beside the English
/// ones, with no locale to select and nothing to opt into. A deny-list
/// that only reads English hides a <c>password</c> field and shows the
/// <c>contraseña</c> beside it, which is not a weaker guarantee but a
/// differently-distributed one: it protects whoever happens to name
/// fields in the language the list was written in. Names are folded
/// through <see cref="SecretValueShapes.Canonical"/>, so the accented and
/// unaccented spellings of a word are one pattern rather than two.
/// </para>
/// <para>
/// Two words that started out short and token-matched — Spanish
/// <c>clave</c> and French <c>carte</c> — turned out to name their own
/// ordinary business fields (<c>clavePrimaria</c>/<c>claveForanea</c>,
/// <c>carteGraphique</c>/<c>carteRoutiere</c>) rather than colliding with
/// someone else's. A token match cannot rescue a word whose own compounds
/// are the false positive, so both were narrowed to the specific
/// compounds that are credentials — <c>claveAcceso</c>/<c>claveSecreta</c>
/// and <c>carteBancaire</c>/<c>numeroCarte</c> — matched as substrings
/// instead, since a whole word that long needs no token boundary to stay
/// safe.
/// </para>
/// </remarks>
public sealed class RedactionPolicy
{
    /// <summary>Marker emitted in place of a redacted value.</summary>
    public const string Marker = "[REDACTED]";

    private static readonly string[] DefaultPatterns =
    [
        // English
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "api_key",
        "cvv",
        "ssn",
        "authorization",
        "credential",
        "privatekey",
        "private_key",
        "cardnumber",
        "card_number",
        "jwt",
        "cookie",
        "setcookie",
        "set_cookie",
        "sessionid",
        "session_id",
        "accountnumber",
        "account_number",
        "routingnumber",
        "routing_number",
        "pan",
        "iban",
        "passphrase",
        "otp",
        "bearer",
        "accesskey",
        "access_key",
        "socialsecurity",
        "social_security",
        "socialsecuritynumber",
        "taxid",
        "tax_id",

        // Short non-English words matched on identifier-token boundaries, same
        // reason as pan/iban: cpf and cnpj are here for length alone; rut is
        // inside truth/brute/scrutiny, cuit inside circuit/biscuit, dni inside
        // midnight, nir inside nirvana, mima inside semiMajorAxis, and senha is
        // the subtle one -- no English word contains it, but chosenHash and
        // frozenHash do, across the camel-case seam. See TokenBoundaryPatterns.
        "rut",
        "cuit",
        "dni",
        "senha",
        "cpf",
        "cnpj",
        "nir",
        "mima",

        // Spanish: contraseña, tarjeta, cédula -- written folded, matched either way
        "contrasena",
        "tarjeta",
        "cedula",

        // Spanish: bare "clave" was narrowed away -- it matched clavePrimaria and
        // claveForanea, ordinary database terms, not credentials. These two
        // compounds are the unit instead; both spellings, because the
        // underscore is part of the name being matched.
        "claveacceso",
        "clave_acceso",
        "clavesecreta",
        "clave_secreta",

        // Portuguese: cartão
        "cartao",

        // French: both spellings, because the underscore is part of the name being matched
        "motdepasse",
        "mot_de_passe",

        // French: bare "carte" was narrowed away -- it matched carteGraphique and
        // carteRoutiere, ordinary identifiers, not credentials. These two
        // compounds are the unit instead; both spellings, same reason as above.
        "cartebancaire",
        "carte_bancaire",
        "numerocarte",
        "numero_carte",

        // Chinese: U+5BC6 U+7801 (mima, password) and U+8EAB U+4EFD U+8BC1
        // (identity card), plus the pinyin a codebase without CJK
        // identifiers writes instead
        "密码",
        "身份证",
        "shenfenzheng",

        // German: Passwort (password), Kennwort (password/passcode)
        "passwort",
        "kennwort",
    ];

    // "pan"/"iban" and a set of non-English words are too short to be safe as
    // substrings (see the class remarks), so whatever pattern set a policy
    // carries, these specific tokens are always matched on identifier-token
    // boundaries rather than as substrings — a custom policy that opts into
    // "pan" is not opting into "companyName". "clave"/"carte" deliberately do
    // NOT belong here: a token match only protects a short word from
    // *someone else's* compound, not from a codebase's own clavePrimaria or
    // carteGraphique — see DefaultPatterns' claveAcceso/carteBancaire entries.
    private static readonly HashSet<string> TokenBoundaryPatterns =
        new(StringComparer.Ordinal)
        {
            "pan", "iban", "rut", "cuit", "dni", "senha", "cpf", "cnpj", "nir", "mima",
        };

    /// <summary>
    /// Secure default: redacts values for common sensitive field-name
    /// patterns, the JWT/PAN/<c>Set-Cookie</c>/national-id value shapes
    /// regardless of field name, plus whatever
    /// <see cref="AdditionalRedactionPatterns"/> configures.
    /// </summary>
    public static readonly RedactionPolicy Default =
        DefaultsFor(AdditionalRedactionPatterns.Configured());

    /// <summary>
    /// Opt-out policy that redacts nothing by name or value shape
    /// (annotations are still honored elsewhere).
    /// </summary>
    public static readonly RedactionPolicy Disabled =
        new([], valueShapesEnabled: false);

    private readonly HashSet<string> _lowerPatterns;
    private readonly bool _valueShapesEnabled;

    private RedactionPolicy(IEnumerable<string> patterns, bool valueShapesEnabled)
    {
        _lowerPatterns = new HashSet<string>(
            patterns.Select(SecretValueShapes.Canonical));
        _valueShapesEnabled = valueShapesEnabled;
    }

    /// <summary>
    /// Builds the default policy with <paramref name="additionalPatterns"/>
    /// unioned in — the seam <see cref="Default"/> itself uses, exposed so a
    /// test can rebuild the default vocabulary with a hand-picked widening
    /// instead of faking the environment (see
    /// <see cref="AdditionalRedactionPatterns.Configured()"/> for the real
    /// source). <see cref="Default"/> is constructed once, during static
    /// initialization, which is the moment the environment is actually read
    /// for it.
    /// </summary>
    internal static RedactionPolicy DefaultsFor(IReadOnlyCollection<string> additionalPatterns)
    {
        return new RedactionPolicy(Union(DefaultPatterns, additionalPatterns), valueShapesEnabled: true);
    }

    /// <summary>
    /// Creates a policy with a custom set of case- and accent-insensitive
    /// name patterns, replacing the defaults entirely. Value-shape masking
    /// (JWT/PAN/<c>Set-Cookie</c>/national-id) stays on — a custom name
    /// list is an opinion about which field <em>names</em> are sensitive,
    /// not about whether these particular byte shapes are a credential.
    /// </summary>
    /// <remarks>
    /// <see cref="AdditionalRedactionPatterns"/> is still appended here.
    /// Replacing the vocabulary is a decision about which names an
    /// application traces; it is not permission to undo a deployment's own
    /// widening.
    /// </remarks>
    public static RedactionPolicy OfPatterns(IEnumerable<string> patterns)
    {
        return OfPatternsFor(patterns, AdditionalRedactionPatterns.Configured());
    }

    /// <summary>
    /// The <see cref="OfPatterns"/> seam with the additional-patterns source
    /// supplied directly, for the same reason <see cref="DefaultsFor"/>
    /// exists.
    /// </summary>
    internal static RedactionPolicy OfPatternsFor(
        IEnumerable<string> patterns, IReadOnlyCollection<string> additionalPatterns)
    {
        return new RedactionPolicy(Union(patterns, additionalPatterns), valueShapesEnabled: true);
    }

    // The supplied patterns plus the operator's additions, without copying
    // when there are none.
    private static IEnumerable<string> Union(
        IEnumerable<string> patterns, IReadOnlyCollection<string> additional)
    {
        return additional.Count == 0 ? patterns : patterns.Concat(additional);
    }

    /// <summary>Whether a field's value must be withheld, based on its name.</summary>
    /// <param name="fieldName">
    /// The parameter or property name to test. <see langword="null"/> returns
    /// <see langword="false"/> — an unnamed value is never redacted by this
    /// policy, so name your captures.
    /// </param>
    /// <returns><see langword="true"/> when the name matches any configured pattern.</returns>
    /// <remarks>
    /// Matching is by <b>substring</b> for every pattern except the handful
    /// of short token-boundary-only patterns (<see cref="TokenBoundaryPatterns"/>):
    /// a pattern of <c>"key"</c> also redacts <c>monkeyCount</c>. That bias
    /// toward over-redaction is deliberate — a false positive costs a
    /// hidden value, a false negative leaks a secret. The short patterns are
    /// the exception: they are matched on identifier-token boundaries (plus
    /// a whole-name comparison, so oddly-cased input like <c>IbAn</c> still
    /// matches even though tokenizing alone would split it into <c>Ib</c> +
    /// <c>An</c>), because as substrings they redact ordinary business fields
    /// (<c>companyName</c>, <c>planId</c>, <c>spanCount</c>, <c>midnight</c>,
    /// <c>circuit</c>). Both the field name and every pattern are folded
    /// through <see cref="SecretValueShapes.Canonical"/> first, so an accented
    /// and an unaccented spelling of the same word are one pattern. Decisions
    /// are name-based only; a secret in an innocuously-named field is not
    /// detected, so this is a backstop rather than a guarantee.
    /// </remarks>
    public bool ShouldRedact(string? fieldName)
    {
        if (fieldName is null)
        {
            return false;
        }

        var canonical = SecretValueShapes.Canonical(fieldName);
        return MatchesSubstringPattern(canonical)
            || MatchesTokenPattern(fieldName, canonical);
    }

    // The substring half of the vocabulary: every pattern that is not one of
    // the short TokenBoundaryPatterns words matches anywhere in the folded name.
    //
    // S3267 is suppressed deliberately: iterating the concrete hash-set field,
    // rather than through the sequence interface, uses its struct enumerator and
    // binds the test as a direct call, so nothing is allocated. ValueRenderer
    // asks this for every reflected member of every rendered object, a path the
    // RenderObject benchmark holds to a zero-allocation-regression budget, and
    // the suggested LINQ quantifier would allocate a closure on every call just
    // to fail to match an ordinary member name.
    private bool MatchesSubstringPattern(string canonical)
    {
#pragma warning disable S3267
        foreach (var pattern in _lowerPatterns)
        {
            if (!TokenBoundaryPatterns.Contains(pattern)
                && canonical.Contains(pattern))
            {
                return true;
            }
        }
#pragma warning restore S3267

        return false;
    }

    /// <summary>
    /// The token-boundary half, walked from the <em>name's</em> side rather than
    /// the pattern's: each identifier token is folded once and looked up, where
    /// asking "does this name contain pattern P as a token?" once per short
    /// pattern re-folded the same token ten times over.
    /// </summary>
    /// <remarks>
    /// The decision is unchanged, only the number of folds. Matching any
    /// token-boundary pattern is a disjunction over patterns, so it can be read
    /// from either side: a token matches iff its folded form is one this policy
    /// carries <em>and</em> one of the always-token-matched words — exactly the
    /// conjunction the per-pattern loop tested. The whole-name comparison is
    /// preserved as the same membership test on the folded name, which is what
    /// lets oddly-cased input like <c>IbAn</c> match even though tokenizing
    /// alone would split it into <c>Ib</c> + <c>An</c>.
    /// </remarks>
    private bool MatchesTokenPattern(string fieldName, string canonical)
    {
        if (IsTokenPattern(canonical))
        {
            return true;
        }

        var start = 0;
        for (var i = 1; i <= fieldName.Length; i++)
        {
            if (i < fieldName.Length && !IsTokenBoundary(fieldName, i))
            {
                continue;
            }

            if (IsWordChar(fieldName[start])
                && IsTokenPattern(FoldedToken(fieldName, start, i)))
            {
                return true;
            }

            start = i;
        }

        return false;
    }

    // Reached only for a word this policy actually carries AND that is one of
    // the short words always matched on identifier-token boundaries rather than
    // as a substring.
    private bool IsTokenPattern(string? candidate)
    {
        return candidate is not null
            && TokenBoundaryPatterns.Contains(candidate)
            && _lowerPatterns.Contains(candidate);
    }

    // Folded once per token, not once per token-boundary pattern. Every entry in
    // _lowerPatterns is already canonicalized by the constructor, so only the
    // name side needs folding — and only the (short) token, never the whole
    // field name.
    private static string? FoldedToken(string name, int start, int end)
    {
        return end - start == 0
            ? null
            : SecretValueShapes.Canonical(name[start..end]);
    }

    /// <summary>
    /// Whether a scalar string value must be withheld based on its own
    /// content, independent of the field name (or absence of one) that
    /// carries it.
    /// </summary>
    /// <param name="value">The candidate value. <see langword="null"/> returns <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the value looks like a JWT, a Luhn-valid
    /// payment card number, an HTTP <c>Set-Cookie</c> header value, or a
    /// national identity number that passes its own checksum.
    /// </returns>
    /// <remarks>
    /// Deliberately narrow and structural — no entropy or "looks random"
    /// heuristics. A value blanked by guesswork is a hole in the trace the
    /// reader cannot see and cannot switch off per-value. See
    /// <see cref="SecretValueShapes"/> for the shapes themselves.
    /// Off under <see cref="Disabled"/>; on under <see cref="Default"/> and
    /// every <see cref="OfPatterns"/> policy.
    /// </remarks>
    public bool ShouldRedactValue(string? value)
    {
        return _valueShapesEnabled
            && value is not null
            && SecretValueShapes.Matches(value);
    }

    // A minimal, self-contained tokenizer: splits on any non-alphanumeric
    // character, a digit/letter boundary, and a case boundary (camelCase,
    // PascalCase, and an acronym's last capital before a new word), walking
    // MatchesTokenPattern's spans in place rather than materializing a token
    // list — this runs on every reflected member name ValueRenderer renders (see
    // ShouldRedact's own remark on the benchmark budget). Not a reuse of
    // NarrativeTrace.Clarity's IdentifierTokenizer — Core cannot depend on
    // Clarity — and deliberately guards against that type's own known gap (a run
    // of non-word characters surviving as a bogus token; see the project
    // backlog, item 24) by only ever comparing a span that starts on a
    // letter-or-digit.
    private static bool IsTokenBoundary(string s, int i)
    {
        var prev = s[i - 1];
        var curr = s[i];
        if (!IsWordChar(prev) || !IsWordChar(curr))
        {
            return true;
        }

        return char.IsDigit(curr) != char.IsDigit(prev) || IsCaseBoundary(s, i);
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

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c);

    /// <summary>
    /// The single redaction rule for a named member: an explicit
    /// <c>[NotTraced]</c> attribute always redacts, and otherwise the
    /// name-based deny-list decides.
    /// </summary>
    /// <remarks>
    /// Every surface that can name a member — reflective introspection in
    /// <c>ValueRenderer</c>, and <c>[Narrated]</c>/<c>[OnError]</c> template
    /// paths in <c>NarrationResolver</c> — asks this one method. Two
    /// implementations of "is this redacted?" would drift, and a drifted
    /// redaction rule is a leak on whichever surface fell behind.
    /// </remarks>
    /// <param name="memberName">The declared field, property or record-component name.</param>
    /// <param name="annotated">Whether that member carries <c>[NotTraced]</c>.</param>
    /// <returns><see langword="true"/> if the value behind the member must be hidden.</returns>
    public bool IsRedacted(string? memberName, bool annotated)
    {
        return annotated || ShouldRedact(memberName);
    }
}
