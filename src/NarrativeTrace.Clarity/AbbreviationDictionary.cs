// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Classifies known abbreviations into quality tiers, mirroring the Java clarity
/// model (187 entries: 38 Universal, 108 WellKnown, 41 Ambiguous — CLARITY-2).
/// <see cref="TierScore"/> is the raw tier score used by the method- and
/// class-name scorers; the parameter scorer re-maps tiers with its own scale.
/// </summary>
public static class AbbreviationDictionary
{
    private static readonly Dictionary<string, AbbreviationTier>
        Known = new(StringComparer.OrdinalIgnoreCase)
        {
            // Universal (0.8) — universally understood.
            ["id"] = AbbreviationTier.Universal,
            ["url"] = AbbreviationTier.Universal,
            ["uri"] = AbbreviationTier.Universal,
            ["api"] = AbbreviationTier.Universal,
            ["html"] = AbbreviationTier.Universal,
            ["xml"] = AbbreviationTier.Universal,
            ["json"] = AbbreviationTier.Universal,
            ["http"] = AbbreviationTier.Universal,
            ["https"] = AbbreviationTier.Universal,
            ["db"] = AbbreviationTier.Universal,
            ["io"] = AbbreviationTier.Universal,
            ["ui"] = AbbreviationTier.Universal,
            ["ok"] = AbbreviationTier.Universal,
            ["max"] = AbbreviationTier.Universal,
            ["min"] = AbbreviationTier.Universal,
            ["sql"] = AbbreviationTier.Universal,
            ["css"] = AbbreviationTier.Universal,
            ["tcp"] = AbbreviationTier.Universal,
            ["udp"] = AbbreviationTier.Universal,
            ["ip"] = AbbreviationTier.Universal,
            ["dns"] = AbbreviationTier.Universal,
            ["ssh"] = AbbreviationTier.Universal,
            ["ssl"] = AbbreviationTier.Universal,
            ["tls"] = AbbreviationTier.Universal,
            ["jwt"] = AbbreviationTier.Universal,
            ["cpu"] = AbbreviationTier.Universal,
            ["gpu"] = AbbreviationTier.Universal,
            ["ram"] = AbbreviationTier.Universal,
            ["os"] = AbbreviationTier.Universal,
            ["jvm"] = AbbreviationTier.Universal,
            ["gc"] = AbbreviationTier.Universal,
            ["uuid"] = AbbreviationTier.Universal,
            ["sdk"] = AbbreviationTier.Universal,
            ["grpc"] = AbbreviationTier.Universal,
            ["sso"] = AbbreviationTier.Universal,
            ["mfa"] = AbbreviationTier.Universal,
            ["pii"] = AbbreviationTier.Universal,
            ["kpi"] = AbbreviationTier.Universal,

            // Well-known (0.6) — understood by most developers.
            ["ctx"] = AbbreviationTier.WellKnown,
            ["cfg"] = AbbreviationTier.WellKnown,
            ["config"] = AbbreviationTier.WellKnown,
            ["mgr"] = AbbreviationTier.WellKnown,
            ["impl"] = AbbreviationTier.WellKnown,
            ["src"] = AbbreviationTier.WellKnown,
            ["dest"] = AbbreviationTier.WellKnown,
            ["dst"] = AbbreviationTier.WellKnown,
            ["buf"] = AbbreviationTier.WellKnown,
            ["idx"] = AbbreviationTier.WellKnown,
            ["tmp"] = AbbreviationTier.WellKnown,
            ["temp"] = AbbreviationTier.WellKnown,
            ["auth"] = AbbreviationTier.WellKnown,
            ["repo"] = AbbreviationTier.WellKnown,
            ["env"] = AbbreviationTier.WellKnown,
            ["async"] = AbbreviationTier.WellKnown,
            ["sync"] = AbbreviationTier.WellKnown,
            ["param"] = AbbreviationTier.WellKnown,
            ["params"] = AbbreviationTier.WellKnown,
            ["attr"] = AbbreviationTier.WellKnown,
            ["attrs"] = AbbreviationTier.WellKnown,
            ["ref"] = AbbreviationTier.WellKnown,
            ["conn"] = AbbreviationTier.WellKnown,
            ["stmt"] = AbbreviationTier.WellKnown,
            ["msg"] = AbbreviationTier.WellKnown,
            ["req"] = AbbreviationTier.WellKnown,
            ["res"] = AbbreviationTier.WellKnown,
            ["resp"] = AbbreviationTier.WellKnown,
            ["err"] = AbbreviationTier.WellKnown,
            ["exc"] = AbbreviationTier.WellKnown,
            ["ex"] = AbbreviationTier.WellKnown,
            ["cmd"] = AbbreviationTier.WellKnown,
            ["arg"] = AbbreviationTier.WellKnown,
            ["args"] = AbbreviationTier.WellKnown,
            ["val"] = AbbreviationTier.WellKnown,
            ["var"] = AbbreviationTier.WellKnown,
            ["vars"] = AbbreviationTier.WellKnown,
            ["str"] = AbbreviationTier.WellKnown,
            ["num"] = AbbreviationTier.WellKnown,
            ["len"] = AbbreviationTier.WellKnown,
            ["pos"] = AbbreviationTier.WellKnown,
            ["prev"] = AbbreviationTier.WellKnown,
            ["cur"] = AbbreviationTier.WellKnown,
            ["curr"] = AbbreviationTier.WellKnown,
            ["iter"] = AbbreviationTier.WellKnown,
            ["obj"] = AbbreviationTier.WellKnown,
            ["fn"] = AbbreviationTier.WellKnown,
            ["func"] = AbbreviationTier.WellKnown,
            ["cb"] = AbbreviationTier.WellKnown,
            ["evt"] = AbbreviationTier.WellKnown,
            ["elem"] = AbbreviationTier.WellKnown,
            ["elems"] = AbbreviationTier.WellKnown,
            ["prop"] = AbbreviationTier.WellKnown,
            ["props"] = AbbreviationTier.WellKnown,
            ["dir"] = AbbreviationTier.WellKnown,
            ["lib"] = AbbreviationTier.WellKnown,
            ["pkg"] = AbbreviationTier.WellKnown,
            ["ver"] = AbbreviationTier.WellKnown,
            ["doc"] = AbbreviationTier.WellKnown,
            ["docs"] = AbbreviationTier.WellKnown,
            ["spec"] = AbbreviationTier.WellKnown,
            ["fmt"] = AbbreviationTier.WellKnown,
            ["seq"] = AbbreviationTier.WellKnown,
            ["init"] = AbbreviationTier.WellKnown,
            ["dyn"] = AbbreviationTier.WellKnown,
            ["alloc"] = AbbreviationTier.WellKnown,
            ["dealloc"] = AbbreviationTier.WellKnown,
            ["chan"] = AbbreviationTier.WellKnown,
            ["ack"] = AbbreviationTier.WellKnown,
            ["nack"] = AbbreviationTier.WellKnown,
            ["coll"] = AbbreviationTier.WellKnown,
            ["desc"] = AbbreviationTier.WellKnown,
            ["info"] = AbbreviationTier.WellKnown,
            ["stat"] = AbbreviationTier.WellKnown,
            ["stats"] = AbbreviationTier.WellKnown,
            ["cnt"] = AbbreviationTier.WellKnown,
            ["avg"] = AbbreviationTier.WellKnown,
            ["pct"] = AbbreviationTier.WellKnown,
            ["delim"] = AbbreviationTier.WellKnown,
            ["sep"] = AbbreviationTier.WellKnown,
            ["hdr"] = AbbreviationTier.WellKnown,
            ["svc"] = AbbreviationTier.WellKnown,
            ["txn"] = AbbreviationTier.WellKnown,
            ["tx"] = AbbreviationTier.WellKnown,
            ["ttl"] = AbbreviationTier.WellKnown,
            ["qps"] = AbbreviationTier.WellKnown,
            ["rps"] = AbbreviationTier.WellKnown,
            ["mtls"] = AbbreviationTier.WellKnown,
            ["oidc"] = AbbreviationTier.WellKnown,
            ["sli"] = AbbreviationTier.WellKnown,
            ["slo"] = AbbreviationTier.WellKnown,
            ["sla"] = AbbreviationTier.WellKnown,
            ["etl"] = AbbreviationTier.WellKnown,
            ["elt"] = AbbreviationTier.WellKnown,
            ["cdc"] = AbbreviationTier.WellKnown,
            ["oltp"] = AbbreviationTier.WellKnown,
            ["olap"] = AbbreviationTier.WellKnown,
            ["p99"] = AbbreviationTier.WellKnown,
            ["llm"] = AbbreviationTier.WellKnown,
            ["asr"] = AbbreviationTier.WellKnown,
            ["tts"] = AbbreviationTier.WellKnown,
            ["ner"] = AbbreviationTier.WellKnown,
            ["ocr"] = AbbreviationTier.WellKnown,
            ["totp"] = AbbreviationTier.WellKnown,
            ["siem"] = AbbreviationTier.WellKnown,
            ["soc2"] = AbbreviationTier.WellKnown,
            ["gdpr"] = AbbreviationTier.WellKnown,
            ["hipaa"] = AbbreviationTier.WellKnown,

            // Ambiguous (0.3) — multiple possible meanings; penalized.
            ["cust"] = AbbreviationTier.Ambiguous,
            ["proc"] = AbbreviationTier.Ambiguous,
            ["mod"] = AbbreviationTier.Ambiguous,
            ["del"] = AbbreviationTier.Ambiguous,
            ["acc"] = AbbreviationTier.Ambiguous,
            ["op"] = AbbreviationTier.Ambiguous,
            ["rec"] = AbbreviationTier.Ambiguous,
            ["sec"] = AbbreviationTier.Ambiguous,
            ["gen"] = AbbreviationTier.Ambiguous,
            ["app"] = AbbreviationTier.Ambiguous,
            ["calc"] = AbbreviationTier.Ambiguous,
            ["cat"] = AbbreviationTier.Ambiguous,
            ["comp"] = AbbreviationTier.Ambiguous,
            ["loc"] = AbbreviationTier.Ambiguous,
            ["perm"] = AbbreviationTier.Ambiguous,
            ["reg"] = AbbreviationTier.Ambiguous,
            ["srv"] = AbbreviationTier.Ambiguous,
            ["tgt"] = AbbreviationTier.Ambiguous,
            ["hdl"] = AbbreviationTier.Ambiguous,
            ["blk"] = AbbreviationTier.Ambiguous,
            ["chk"] = AbbreviationTier.Ambiguous,
            ["clr"] = AbbreviationTier.Ambiguous,
            ["cmp"] = AbbreviationTier.Ambiguous,
            ["cpy"] = AbbreviationTier.Ambiguous,
            ["dup"] = AbbreviationTier.Ambiguous,
            ["flt"] = AbbreviationTier.Ambiguous,
            ["grp"] = AbbreviationTier.Ambiguous,
            ["lbl"] = AbbreviationTier.Ambiguous,
            ["lvl"] = AbbreviationTier.Ambiguous,
            ["mgmt"] = AbbreviationTier.Ambiguous,
            ["neg"] = AbbreviationTier.Ambiguous,
            ["orig"] = AbbreviationTier.Ambiguous,
            ["pfx"] = AbbreviationTier.Ambiguous,
            ["sfx"] = AbbreviationTier.Ambiguous,
            ["sig"] = AbbreviationTier.Ambiguous,
            ["sym"] = AbbreviationTier.Ambiguous,
            ["tbl"] = AbbreviationTier.Ambiguous,
            ["tok"] = AbbreviationTier.Ambiguous,
            ["usr"] = AbbreviationTier.Ambiguous,
            ["wgt"] = AbbreviationTier.Ambiguous,
            ["rag"] = AbbreviationTier.Ambiguous,
        };

    /// <summary>Number of known abbreviations; used by the size-floor drift guard.</summary>
    public static int Count => Known.Count;

    /// <summary>Returns the tier for a token, or null if unknown.</summary>
    /// <param name="token">The identifier token to look up.</param>
    /// <param name="vocabulary">
    /// The project's declared vocabulary, or null for the built-in dictionary
    /// alone.
    /// </param>
    /// <remarks>
    /// A token the project listed in its glossary's <c>abbreviations</c>
    /// section returns null — the same answer as a word the dictionary never
    /// knew. Callers already treat null as "not an abbreviation", so accepted
    /// shorthand is neither scored down nor asked to be spelled out. Only that
    /// section accepts: a token that merely appears inside a committed term is
    /// still an abbreviation, because nobody decided it was shorthand.
    /// </remarks>
    public static AbbreviationTier? Classify(
        string token, DomainVocabulary? vocabulary = null)
    {
        if (vocabulary is not null && vocabulary.IsAcceptedAbbreviation(token))
        {
            return null;
        }

        return Known.TryGetValue(token, out var tier) ? tier : null;
    }

    /// <summary>The raw tier score (Universal 0.8 / WellKnown 0.6 / Ambiguous 0.3).</summary>
    public static double TierScore(AbbreviationTier tier)
    {
        return tier switch
        {
            AbbreviationTier.Universal => 0.8,
            AbbreviationTier.WellKnown => 0.6,
            _ => 0.3,
        };
    }
}
