// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Clarity;

namespace NarrativeTrace.Glossary;

/// <summary>Normalizes code identifiers into glossary phrase form.</summary>
/// <remarks>
/// All identifier spellings of one concept must converge to a single
/// normalized phrase — <c>accountWithOverdraft</c>,
/// <c>AccountWithOverdraft</c>, and <c>account_with_overdraft</c> all become
/// <c>"account with overdraft"</c> — because term and alias matching always
/// happens on the normalized form. Reuses the clarity module's
/// <see cref="IdentifierTokenizer"/> (camelCase / snake_case splitting) and
/// <see cref="MorphologyAnalyzer"/> (verb detection); singularization is a
/// deliberately small English heuristic, applied to non-verb, non-stopword
/// tokens.
/// <para>
/// Because normalized phrases are term identity in persisted glossaries,
/// singularization must never coin non-words from real ones (<c>alias</c>
/// must not become <c>alia</c>) and must be idempotent — every emitted token
/// is a fixpoint of singularization, so re-normalizing a phrase is the
/// identity (property-tested). Changing these rules re-keys existing
/// glossaries and must stay in lockstep across all ports.
/// </para>
/// <para>
/// One rule is deliberately <em>not</em> in lockstep:
/// <see cref="ClassCandidate"/> drops the C# interface-naming <c>I</c>, a
/// convention no other port has. It exists so the phrase this port keys on is
/// the one the other ports already key on — <c>IPaymentService</c> and Java's
/// <c>PaymentService</c> must both be <c>"payment"</c>.
/// </para>
/// </remarks>
public static class TermNormalizer
{
    /// <summary>Function words that must survive normalization untouched (never singularized).</summary>
    private static readonly HashSet<string> Stopwords =
        ["with", "and", "or", "of", "to", "for", "by", "from", "in", "on",
         "at", "as", "was", "is", "has"];

    /// <summary>Trailing patterns whose <c>es</c> suffix marks a plural (<c>boxes</c>, <c>classes</c>).</summary>
    private static readonly string[] EsPluralEndings =
        ["ses", "xes", "zes", "ches", "shes"];

    /// <summary>
    /// Words ending in <c>s</c> that are singular (<c>alias</c>, <c>gas</c>,
    /// Latin <c>-us</c> nouns), invariant plurals (<c>series</c>,
    /// <c>species</c>), or not nouns at all (<c>always</c>) — never stripped.
    /// Also the only way an <c>s</c>-final <c>-es</c> stem is accepted
    /// (<c>gases</c> → <c>gas</c>, <c>statuses</c> → <c>status</c>); an
    /// unlisted <c>s</c>-final stem means the plural was built as
    /// <c>-se + s</c> (<c>clauses</c> → <c>clause</c>).
    /// </summary>
    private static readonly HashSet<string> SFinalSingulars =
        ["alias", "always", "atlas", "bias", "bonus", "bus", "campus", "canvas",
         "census", "chaos", "corpus", "focus", "gas", "lens", "locus", "news",
         "radius", "series", "species", "status", "surplus", "virus"];

    /// <summary>Exception-type suffixes stripped when harvesting failure vocabulary.</summary>
    private static readonly HashSet<string> ExceptionSuffixes = ["exception", "error"];

    /// <summary>
    /// Shortest name the interface-prefix rule reads: the <c>I</c>, the first
    /// letter of the concept, and the one character after it that decides
    /// whether that letter opened a word or an acronym.
    /// </summary>
    private const int InterfacePrefixFloor = 3;

    /// <summary>
    /// Normalizes one identifier to phrase form: lowercase, space-separated,
    /// plural nouns singularized.
    /// </summary>
    /// <param name="identifier">camelCase, PascalCase, or snake_case identifier; must not be blank.</param>
    /// <returns>The normalized phrase; never blank.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is blank or carries no readable word.</exception>
    public static string Phrase(string identifier)
    {
        return string.Join(" ", RequireReadableTokens(identifier));
    }

    /// <summary>
    /// <see cref="Phrase"/> for callers that must stay total: the normalized
    /// phrase, or null when the identifier carries no readable word.
    /// </summary>
    /// <remarks>
    /// The same answer <see cref="ClassCandidate"/> and its siblings already
    /// give — "nothing to harvest" is a value, not a failure — for a caller
    /// that wants phrase semantics without role-suffix stripping. A renderer
    /// reading a wire format cannot treat an unreadable name as an error,
    /// because the format permits one, so it asks this instead.
    /// </remarks>
    /// <param name="identifier">camelCase, PascalCase, or snake_case identifier; must not be blank.</param>
    /// <returns>The normalized phrase, or null when nothing readable remains.</returns>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is blank.</exception>
    public static string? PhraseOrNull(string identifier)
    {
        GuardIdentifier(identifier);
        var tokens = NormalizedTokens(identifier);
        return tokens.Count == 0 ? null : string.Join(" ", tokens);
    }

    /// <summary>Normalizes a method name into harvest candidates.</summary>
    /// <remarks>
    /// A method with a leading verb yields its verb phrase plus the object
    /// noun phrase (leading stopwords dropped); any other method yields a
    /// single noun candidate.
    /// </remarks>
    /// <param name="methodName">Method identifier; must not be blank.</param>
    /// <returns>One or two candidates, never empty.</returns>
    /// <exception cref="ArgumentException"><paramref name="methodName"/> is blank or carries no readable word.</exception>
    public static IReadOnlyList<TermCandidate> MethodCandidates(string methodName)
    {
        var tokens = RequireReadableTokens(methodName);
        if (!IsVerb(tokens[0]))
        {
            return [NounCandidate(tokens)];
        }

        var candidates = new List<TermCandidate>
        {
            new(string.Join(" ", tokens), TermKind.VerbPhrase),
        };
        var objectTokens = WithoutLeadingStopwords(tokens.Skip(1).ToList());
        if (objectTokens.Count > 0)
        {
            candidates.Add(NounCandidate(objectTokens));
        }

        return candidates;
    }

    /// <summary>
    /// Normalizes a parameter name into a noun candidate, stripping the
    /// trailing <c>id</c> role token (<c>overdraftAccountId</c> →
    /// <c>"overdraft account"</c>).
    /// </summary>
    /// <param name="parameterName">Parameter identifier; must not be blank.</param>
    /// <returns>The noun candidate, or null when only the role token remains (<c>id</c>).</returns>
    /// <exception cref="ArgumentException"><paramref name="parameterName"/> is blank.</exception>
    public static TermCandidate? ParameterCandidate(string parameterName)
    {
        return StrippedCandidate(parameterName, token => token == "id");
    }

    /// <summary>
    /// Normalizes a class name into a noun candidate, dropping the C#
    /// interface-naming <c>I</c> and a recognized role suffix
    /// (<c>IOverdraftService</c> → <c>"overdraft"</c>).
    /// </summary>
    /// <param name="className">Simple class name; must not be blank.</param>
    /// <returns>The noun candidate, or null when only the role suffix remains (<c>Service</c>).</returns>
    /// <exception cref="ArgumentException"><paramref name="className"/> is blank.</exception>
    public static TermCandidate? ClassCandidate(string className)
    {
        GuardIdentifier(className);
        return StrippedCandidate(WithoutInterfacePrefix(className), IsRoleSuffix);
    }

    /// <summary>
    /// Drops the C# interface-naming <c>I</c>, so an interface contributes its
    /// concept rather than the language's decoration: <c>IPaymentService</c>
    /// harvests as <c>"payment"</c>, exactly as Java's <c>PaymentService</c>
    /// does.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name's shape is the only signal that may be used. A trace node
    /// carries a simple class name and nothing else, so <c>Type.IsInterface</c>
    /// is unavailable on the captured-trace path and would key one concept to
    /// two different phrases depending on which harvest saw it first —
    /// unacceptable, because the normalized phrase <em>is</em> term identity in
    /// a persisted glossary.
    /// </para>
    /// <para>
    /// The convention is a capital <c>I</c> in front of a name that itself
    /// starts an ordinary PascalCase word, which is exactly what the two
    /// character tests read. <c>Item</c> and <c>Identity</c> keep their
    /// <c>I</c> (second character lower-case), and so do acronym-initial names
    /// such as <c>IOManager</c> and <c>IPAddress</c> (third character
    /// upper-case) — not the convention, and their tokenizations
    /// (<c>"io manager"</c>, <c>"ip address"</c>) are already the words a
    /// reader wants, matching what Java's <c>IoManager</c> / <c>IpAddress</c>
    /// yield.
    /// </para>
    /// </remarks>
    private static string WithoutInterfacePrefix(string className)
    {
        return className.Length >= InterfacePrefixFloor
            && className[0] == 'I'
            && char.IsUpper(className[1])
            && !char.IsUpper(className[2])
                ? className.Substring(1)
                : className;
    }

    /// <summary>
    /// Normalizes an exception type name into a noun candidate, stripping the
    /// <c>Exception</c> / <c>Error</c> suffix
    /// (<c>InsufficientFundsException</c> → <c>"insufficient fund"</c>).
    /// </summary>
    /// <param name="exceptionTypeName">Simple exception type name; must not be blank.</param>
    /// <returns>The noun candidate, or null when only the suffix remains.</returns>
    /// <exception cref="ArgumentException"><paramref name="exceptionTypeName"/> is blank.</exception>
    public static TermCandidate? ExceptionCandidate(string exceptionTypeName)
    {
        return StrippedCandidate(exceptionTypeName, ExceptionSuffixes.Contains);
    }

    // Unlike Phrase/MethodCandidates (RequireReadableTokens), an identifier
    // carrying no readable word is not an error here: these three callers
    // already model "nothing to harvest" with a null return for the
    // role-suffix-only case, so a tokenless identifier is the same answer to
    // the same question rather than a second failure mode.
    private static TermCandidate? StrippedCandidate(
        string identifier, Func<string, bool> trailingRole)
    {
        GuardIdentifier(identifier);
        var tokens = NormalizedTokens(identifier);
        if (tokens.Count == 0)
        {
            return null;
        }

        if (trailingRole(tokens[tokens.Count - 1]))
        {
            tokens = tokens.Take(tokens.Count - 1).ToList();
        }

        return tokens.Count == 0 ? null : NounCandidate(tokens);
    }

    private static bool IsRoleSuffix(string token)
    {
        return RoleSuffixDictionary.Classify(token) != RoleSuffixCategory.Unknown;
    }

    private static List<string> WithoutLeadingStopwords(List<string> tokens)
    {
        var start = 0;
        while (start < tokens.Count && Stopwords.Contains(tokens[start]))
        {
            start++;
        }

        return tokens.Skip(start).ToList();
    }

    private static TermCandidate NounCandidate(IReadOnlyList<string> tokens)
    {
        var kind = tokens.Count == 1 ? TermKind.Word : TermKind.NounPhrase;
        return new TermCandidate(string.Join(" ", tokens), kind);
    }

    private static List<string> NormalizedTokens(string identifier)
    {
        return IdentifierTokenizer.Tokenize(identifier)
            .Select(NormalizeToken)
            .ToList();
    }

    private static string NormalizeToken(string token)
    {
        if (Stopwords.Contains(token) || IsVerb(token))
        {
            return token;
        }

        return Singularize(token);
    }

    private static bool IsVerb(string token)
    {
        return MorphologyAnalyzer.Analyze(token) == PartOfSpeech.Verb;
    }

    private static string Singularize(string token)
    {
        if (SFinalSingulars.Contains(token))
        {
            return token;
        }

        if (token.EndsWith("ies", StringComparison.Ordinal) && token.Length > 3)
        {
            return token.Substring(0, token.Length - 3) + "y";
        }

        return StripPluralSuffix(token);
    }

    /// <summary>
    /// Strips an <c>-es</c> or <c>-s</c> plural suffix. An <c>-es</c> plural is
    /// only accepted when its stem stands on its own as a singular
    /// (<c>gases</c> → <c>gas</c>); an unstable stem means the plural was built
    /// as <c>-se + s</c> (<c>cases</c>, <c>responses</c>), so the single-s rule
    /// takes over and the <c>e</c> survives.
    /// </summary>
    private static string StripPluralSuffix(string token)
    {
        if (EsPluralEndings.Any(e => token.EndsWith(e, StringComparison.Ordinal))
            && token.Length > 3
            && IsStableSingular(token.Substring(0, token.Length - 2)))
        {
            return token.Substring(0, token.Length - 2);
        }

        if (token[token.Length - 1] == 's'
            && token.Length > 1
            && !KeepsTrailingS(token))
        {
            return token.Substring(0, token.Length - 1);
        }

        return token;
    }

    /// <summary>
    /// A stem is an acceptable singular iff <see cref="Singularize"/> would
    /// return it unchanged. Unlike <see cref="KeepsTrailingS"/>, a <c>us</c> /
    /// <c>is</c> ending does not bless a stem: those endings usually mean the
    /// plural was <c>-se + s</c> (<c>clauses</c> → <c>claus</c>,
    /// <c>promises</c> → <c>promis</c>), so only <c>ss</c> stems and listed
    /// words qualify.
    /// </summary>
    private static bool IsStableSingular(string stem)
    {
        return stem[stem.Length - 1] != 's'
            || stem.EndsWith("ss", StringComparison.Ordinal)
            || SFinalSingulars.Contains(stem);
    }

    /// <summary>Words ending in ss/us/is are not English plurals (<c>status</c>, <c>analysis</c>).</summary>
    private static bool KeepsTrailingS(string token)
    {
        return token.EndsWith("ss", StringComparison.Ordinal)
            || token.EndsWith("us", StringComparison.Ordinal)
            || token.EndsWith("is", StringComparison.Ordinal);
    }

    /// <summary>
    /// The normalized tokens of an identifier that must have some, for the two members whose
    /// contract promises a non-blank answer.
    /// </summary>
    /// <remarks>
    /// "Not blank" is the wrong precondition on its own, and used to be the only one: <c>__</c> is
    /// not blank, but the tokenizer reads no word in it, so <see cref="Phrase"/> silently returned
    /// an empty string against its own "never blank" contract, and <see cref="MethodCandidates"/>
    /// indexed an empty token list — an unguarded <see cref="ArgumentOutOfRangeException"/> out of
    /// the glossary harvest, which runs inside the caller's own test run. Found by the security
    /// fuzz suite's hostile corpus.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="identifier"/> is blank or carries no readable word.</exception>
    private static List<string> RequireReadableTokens(string identifier)
    {
        GuardIdentifier(identifier);
        var tokens = NormalizedTokens(identifier);
        if (tokens.Count == 0)
        {
            throw new ArgumentException(
                "identifier must carry at least one readable word: " + identifier, nameof(identifier));
        }

        return tokens;
    }

    private static void GuardIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException(
                "identifier must not be blank", nameof(identifier));
        }
    }
}
