// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Maps domain nouns to the verbs that idiomatically collocate with them, so
/// method-name suggestions can prefer <c>placeOrder</c> over <c>doOrder</c>.
/// The vocabulary is the union of 35 industry / technical domain groups shared
/// across NarrativeTrace runtimes; a noun appearing in several domains keeps the
/// union of every domain's verbs rather than letting one domain win.
/// </summary>
public static class CollocationDictionary
{
    // --- Finance & Banking ---
    private static readonly Dictionary<string, string[]> Finance =
        new(StringComparer.Ordinal)
        {
            ["account"] = ["debit", "credit", "balance", "close", "reconcile", "freeze"],
            ["ledger"] = ["reconcile", "balance", "post", "close"],
            ["payment"] = ["authorize", "capture", "disburse", "remit", "settle", "refund",
                "void"],
            ["loan"] = ["originate", "underwrite", "amortize", "service", "default"],
            ["invoice"] = ["issue", "settle", "void", "dispute"],
            ["transaction"] = ["commit", "rollback", "authorize", "settle", "void", "reverse"],
            ["portfolio"] = ["rebalance", "diversify", "hedge", "liquidate"],
            ["bond"] = ["issue", "mature", "redeem", "yield", "coupon"],
            ["tax"] = ["withhold", "file", "remit", "assess", "levy", "exempt"],
            ["budget"] = ["allocate", "forecast", "reconcile", "approve"],
            ["asset"] = ["value", "revalue", "impair", "liquidate"],
            ["collateral"] = ["pledge", "release", "haircut"],
        };

    // --- E-Commerce & Retail ---
    private static readonly Dictionary<string, string[]> Ecommerce =
        new(StringComparer.Ordinal)
        {
            ["order"] = ["place", "fulfill", "cancel", "ship", "return", "backorder"],
            ["cart"] = ["add", "remove", "empty", "checkout", "abandon"],
            ["inventory"] = ["replenish", "reserve", "deplete", "count", "restock"],
            ["product"] = ["list", "delist", "discount", "bundle", "feature"],
            ["subscription"] = ["activate", "cancel", "renew", "pause", "upgrade", "downgrade"],
            ["coupon"] = ["apply", "redeem", "expire", "validate"],
            ["price"] = ["set", "reprice", "discount", "markdown"],
            ["return"] = ["authorize", "receive", "refund", "restock"],
        };

    // --- Healthcare & Medical ---
    private static readonly Dictionary<string, string[]> Healthcare =
        new(StringComparer.Ordinal)
        {
            ["patient"] = ["admit", "discharge", "refer", "triage", "diagnose", "treat"],
            ["medication"] = ["prescribe", "administer", "dispense", "discontinue", "titrate"],
            ["appointment"] = ["schedule", "cancel", "reschedule", "confirm", "checkin"],
            ["diagnosis"] = ["confirm", "rule", "differential", "code"],
            ["record"] = ["chart", "amend", "seal", "release"],
            ["vaccine"] = ["administer", "store", "discard"],
            ["specimen"] = ["collect", "label", "process"],
        };

    // --- Hospitality & Travel ---
    private static readonly Dictionary<string, string[]> Hospitality =
        new(StringComparer.Ordinal)
        {
            ["reservation"] = ["book", "confirm", "cancel", "modify", "honor", "overbook"],
            ["room"] = ["assign", "vacate", "upgrade", "block", "service"],
            ["guest"] = ["checkin", "checkout", "accommodate", "bill", "comp"],
            ["booking"] = ["rebook", "confirm", "cancel"],
            ["seat"] = ["assign", "upgrade", "downgrade"],
        };

    // --- Telecommunications ---
    private static readonly Dictionary<string, string[]> Telecom =
        new(StringComparer.Ordinal)
        {
            ["call"] = ["route", "drop", "forward", "transfer", "mute", "hold", "record"],
            ["signal"] = ["amplify", "attenuate", "modulate", "demodulate", "broadcast"],
            ["channel"] = ["allocate", "multiplex", "tune", "scramble"],
            ["subscriber"] = ["provision", "suspend", "activate", "port", "throttle"],
            ["session"] = ["originate", "terminate", "handoff"],
            ["bandwidth"] = ["allocate", "shape", "throttle"],
        };

    // --- Gaming ---
    private static readonly Dictionary<string, string[]> Gaming =
        new(StringComparer.Ordinal)
        {
            ["player"] = ["spawn", "respawn", "ban", "kick", "matchmake", "rank"],
            ["item"] = ["equip", "loot", "craft", "enchant", "disenchant", "trade"],
            ["character"] = ["level", "buff", "debuff", "heal", "revive", "nerf"],
            ["match"] = ["start", "pause", "forfeit", "abandon", "spectate"],
            ["queue"] = ["join", "leave", "matchmake"],
            ["lobby"] = ["create", "join", "leave"],
        };

    // --- Logistics & Supply Chain ---
    private static readonly Dictionary<string, string[]> Logistics =
        new(StringComparer.Ordinal)
        {
            ["shipment"] = ["dispatch", "track", "reroute", "deliver", "return", "insure"],
            ["cargo"] = ["load", "unload", "stow", "manifest", "inspect", "clear"],
            ["route"] = ["plan", "optimize", "divert", "schedule"],
            ["container"] = ["load", "seal", "unseal", "transload"],
            ["dock"] = ["assign", "slot", "release"],
        };

    // --- Insurance ---
    private static readonly Dictionary<string, string[]> Insurance =
        new(StringComparer.Ordinal)
        {
            ["policy"] = ["underwrite", "issue", "renew", "cancel", "lapse", "reinstate",
                "endorse"],
            ["claim"] = ["file", "adjust", "settle", "deny", "subrogate", "appeal"],
            ["premium"] = ["quote", "calculate", "collect", "waive", "refund"],
            ["endorsement"] = ["add", "remove", "amend"],
            ["deductible"] = ["apply", "waive"],
        };

    // --- Education ---
    private static readonly Dictionary<string, string[]> Education =
        new(StringComparer.Ordinal)
        {
            ["student"] = ["enroll", "expel", "graduate", "mentor", "counsel", "assess"],
            ["course"] = ["register", "audit", "drop", "complete", "accredit"],
            ["grade"] = ["assign", "appeal", "curve", "post", "withhold"],
            ["attendance"] = ["record", "audit", "verify"],
            ["exam"] = ["schedule", "proctor", "grade"],
        };

    // --- Real Estate & Property ---
    private static readonly Dictionary<string, string[]> RealEstate =
        new(StringComparer.Ordinal)
        {
            ["property"] = ["list", "appraise", "inspect", "close", "escrow", "foreclose"],
            ["lease"] = ["sign", "renew", "terminate", "sublease", "amend"],
            ["tenant"] = ["screen", "evict", "accommodate", "bill"],
            ["title"] = ["search", "clear", "record"],
            ["showing"] = ["schedule", "cancel"],
        };

    // --- HR & Workforce ---
    private static readonly Dictionary<string, string[]> Hr =
        new(StringComparer.Ordinal)
        {
            ["employee"] = ["hire", "onboard", "promote", "demote", "terminate", "furlough",
                "transfer"],
            ["candidate"] = ["screen", "interview", "recruit", "reject", "shortlist"],
            ["position"] = ["post", "fill", "eliminate", "reclassify"],
            ["headcount"] = ["plan", "reduce", "increase"],
            ["compensation"] = ["benchmark", "adjust", "approve"],
        };

    // --- Security & Authentication ---
    private static readonly Dictionary<string, string[]> Security =
        new(StringComparer.Ordinal)
        {
            ["token"] = ["issue", "revoke", "refresh", "rotate", "invalidate", "blacklist"],
            ["credential"] = ["verify", "revoke", "hash", "store", "rotate"],
            ["session"] = ["create", "invalidate", "extend", "hijack", "terminate"],
            ["certificate"] = ["sign", "revoke", "renew", "chain", "pin"],
            ["key"] = ["generate", "rotate", "revoke", "archive"],
            ["acl"] = ["enforce", "evaluate", "audit"],
        };

    // --- DevOps & Infrastructure ---
    private static readonly Dictionary<string, string[]> DevOps =
        new(StringComparer.Ordinal)
        {
            ["instance"] = ["provision", "deploy", "scale", "terminate", "snapshot", "migrate"],
            ["container"] = ["build", "deploy", "kill", "restart", "orchestrate"],
            ["pipeline"] = ["trigger", "run", "abort", "retry", "promote"],
            ["cache"] = ["warm", "invalidate", "evict", "flush", "populate"],
            ["node"] = ["cordon", "drain", "uncordon"],
            ["release"] = ["promote", "rollback", "rollout"],
        };

    // --- Data & Analytics ---
    private static readonly Dictionary<string, string[]> Data =
        new(StringComparer.Ordinal)
        {
            ["dataset"] = ["ingest", "cleanse", "partition", "sample", "anonymize"],
            ["schema"] = ["migrate", "validate", "version", "evolve", "normalize"],
            ["index"] = ["build", "rebuild", "drop", "optimize", "shard"],
            ["query"] = ["execute", "optimize", "cache", "paginate", "throttle"],
            ["feature"] = ["derive", "normalize", "vectorize"],
            ["window"] = ["slide", "aggregate", "rank"],
        };

    // --- Content & Media ---
    private static readonly Dictionary<string, string[]> Content =
        new(StringComparer.Ordinal)
        {
            ["article"] = ["draft", "publish", "archive", "retract", "syndicate"],
            ["comment"] = ["post", "moderate", "flag", "delete", "pin"],
            ["media"] = ["upload", "transcode", "stream", "caption", "watermark"],
            ["subtitle"] = ["generate", "sync", "translate"],
            ["transcript"] = ["generate", "edit", "publish"],
        };

    // --- Social & Community ---
    private static readonly Dictionary<string, string[]> Social =
        new(StringComparer.Ordinal)
        {
            ["user"] = ["follow", "unfollow", "block", "mute", "report", "verify"],
            ["post"] = ["publish", "pin", "boost", "archive", "flag"],
            ["thread"] = ["start", "lock", "archive"],
            ["message"] = ["send", "delete", "unsend"],
        };

    // --- Messaging & Events ---
    private static readonly Dictionary<string, string[]> Messaging =
        new(StringComparer.Ordinal)
        {
            ["message"] = ["enqueue", "dequeue", "acknowledge", "nack", "retry", "deadletter"],
            ["event"] = ["emit", "publish", "replay", "fanout", "route"],
            ["queue"] = ["drain", "purge", "park", "resume"],
        };

    // --- IoT & Embedded ---
    private static readonly Dictionary<string, string[]> Iot =
        new(StringComparer.Ordinal)
        {
            ["device"] = ["provision", "commission", "decommission", "pair", "unpair", "reboot"],
            ["sensor"] = ["calibrate", "sample", "poll", "stream"],
            ["telemetry"] = ["capture", "ingest", "aggregate"],
        };

    // --- Legal & Compliance ---
    private static readonly Dictionary<string, string[]> Legal =
        new(StringComparer.Ordinal)
        {
            ["contract"] = ["draft", "sign", "amend", "terminate", "enforce", "breach"],
            ["case"] = ["file", "adjudicate", "dismiss", "settle", "appeal"],
            ["verdict"] = ["deliver", "appeal", "overturn", "uphold"],
            ["motion"] = ["file", "argue", "grant", "deny"],
            ["brief"] = ["draft", "file", "amend"],
        };

    // --- Manufacturing ---
    private static readonly Dictionary<string, string[]> Manufacturing =
        new(StringComparer.Ordinal)
        {
            ["batch"] = ["start", "inspect", "reject", "release", "quarantine"],
            ["component"] = ["assemble", "solder", "weld", "test", "certify"],
            ["line"] = ["start", "stop", "balance", "retool"],
            ["workorder"] = ["create", "schedule", "close"],
        };

    // --- Agriculture & Food ---
    private static readonly Dictionary<string, string[]> Agriculture =
        new(StringComparer.Ordinal)
        {
            ["field"] = ["plant", "irrigate", "fertilize", "harvest"],
            ["crop"] = ["sow", "spray", "prune", "harvest"],
            ["livestock"] = ["feed", "breed", "vaccinate", "wean"],
        };

    // --- Advertising & Marketing ---
    private static readonly Dictionary<string, string[]> Advertising =
        new(StringComparer.Ordinal)
        {
            ["campaign"] = ["launch", "pause", "optimize", "target", "remarket"],
            ["audience"] = ["segment", "target", "exclude", "expand"],
            ["creative"] = ["draft", "review", "approve", "rotate"],
        };

    // --- Transportation ---
    private static readonly Dictionary<string, string[]> Transportation =
        new(StringComparer.Ordinal)
        {
            ["flight"] = ["schedule", "delay", "depart", "arrive", "reroute"],
            ["vessel"] = ["berth", "moor", "unmoor", "dock"],
            ["vehicle"] = ["dispatch", "refuel", "reroute", "park"],
        };

    // --- Energy & Utilities ---
    private static readonly Dictionary<string, string[]> Energy =
        new(StringComparer.Ordinal)
        {
            ["grid"] = ["balance", "stabilize", "shed", "curtail", "interconnect"],
            ["meter"] = ["read", "calibrate", "install", "replace", "tamper"],
            ["feeder"] = ["energize", "deenergize", "switch", "island"],
            ["plant"] = ["dispatch", "ramp", "derate", "blackstart"],
        };

    // --- Blockchain & Crypto ---
    private static readonly Dictionary<string, string[]> Blockchain =
        new(StringComparer.Ordinal)
        {
            ["token"] = ["mint", "burn", "stake", "transfer", "vest", "lock"],
            ["contract"] = ["deploy", "verify", "audit", "upgrade", "pause"],
            ["validator"] = ["delegate", "redelegate", "slash", "unbond"],
            ["bridge"] = ["lock", "mint", "burn", "release"],
        };

    // --- Government & Public Sector ---
    private static readonly Dictionary<string, string[]> PublicSector =
        new(StringComparer.Ordinal)
        {
            ["permit"] = ["issue", "renew", "revoke", "approve"],
            ["license"] = ["issue", "renew", "suspend", "revoke"],
            ["ordinance"] = ["draft", "enact", "amend", "repeal"],
        };

    // --- Pharmaceuticals & Biotechnology ---
    private static readonly Dictionary<string, string[]> PharmaBiotech =
        new(StringComparer.Ordinal)
        {
            ["assay"] = ["run", "validate", "repeat"],
            ["sample"] = ["aliquot", "dilute", "incubate", "analyze"],
            ["compound"] = ["synthesize", "formulate", "stabilize"],
        };

    // --- Customer Support & CRM ---
    private static readonly Dictionary<string, string[]> SupportCrm =
        new(StringComparer.Ordinal)
        {
            ["ticket"] = ["open", "triage", "assign", "escalate", "resolve", "close"],
            ["case"] = ["categorize", "prioritize", "reopen", "resolve"],
            ["customer"] = ["notify", "update", "verify", "retain"],
        };

    // --- Payments & Fintech ---
    private static readonly Dictionary<string, string[]> PaymentsFintech =
        new(StringComparer.Ordinal)
        {
            ["payout"] = ["initiate", "disburse", "settle", "reverse"],
            ["chargeback"] = ["file", "dispute", "win", "lose"],
            ["authorization"] = ["request", "reauthorize", "decline", "approve"],
        };

    // --- Media AdTech ---
    private static readonly Dictionary<string, string[]> MediaAdTech =
        new(StringComparer.Ordinal)
        {
            ["impression"] = ["serve", "count", "cap", "pace"],
            ["bid"] = ["submit", "win", "lose", "optimize"],
            ["audience"] = ["segment", "target", "expand", "suppress"],
        };

    // --- Programming & Software Engineering ---
    private static readonly Dictionary<string, string[]> Programming =
        new(StringComparer.Ordinal)
        {
            ["node"] = ["create", "build", "render", "visit", "traverse", "remove", "insert",
                "find", "update", "delete"],
            ["tree"] = ["build", "render", "traverse", "walk", "flatten", "prune", "create",
                "parse"],
            ["list"] = ["create", "build", "render", "filter", "sort", "append", "remove",
                "clear", "find"],
            ["score"] = ["compute", "calculate", "normalize", "compare", "update", "aggregate"],
            ["value"] = ["get", "set", "render", "format", "parse", "validate", "compute",
                "convert"],
            ["text"] = ["render", "format", "parse", "tokenize", "trim", "split", "join",
                "encode"],
            ["name"] = ["parse", "validate", "format", "generate", "resolve", "normalize",
                "tokenize", "score"],
            ["error"] = ["handle", "throw", "catch", "log", "report", "wrap", "format",
                "recover"],
            ["result"] = ["compute", "build", "format", "render", "aggregate", "merge",
                "collect"],
            ["state"] = ["update", "reset", "restore", "save", "load", "merge", "initialize",
                "validate"],
            ["context"] = ["create", "build", "enter", "exit", "restore", "save", "capture",
                "wrap"],
            ["source"] = ["read", "parse", "scan", "analyze", "load", "validate", "compile",
                "transform"],
            ["parameter"] = ["validate", "parse", "capture", "render", "extract", "format",
                "score"],
            ["issue"] = ["collect", "detect", "report", "create", "resolve", "format", "filter"],
            ["method"] = ["score", "analyze", "extract", "invoke", "call", "find", "validate"],
            ["class"] = ["define", "instantiate", "extend", "load", "inspect", "serialize"],
            ["object"] = ["create", "clone", "serialize", "deserialize", "validate", "merge"],
            ["module"] = ["load", "initialize", "configure", "wire", "register", "reload"],
            ["package"] = ["publish", "install", "resolve", "upgrade", "sign", "scan"],
            ["field"] = ["read", "write", "map", "validate", "serialize", "redact"],
            ["exception"] = ["throw", "catch", "wrap", "propagate", "log", "map"],
            ["buffer"] = ["allocate", "fill", "flush", "drain", "resize", "slice"],
            ["stream"] = ["open", "read", "write", "flush", "close", "pipe"],
            ["payload"] = ["build", "parse", "validate", "sanitize", "sign", "compress"],
            ["request"] = ["build", "send", "retry", "cancel", "validate", "throttle"],
            ["response"] = ["return", "serialize", "parse", "cache", "stream", "validate"],
            ["schema"] = ["define", "validate", "migrate", "evolve", "generate", "infer"],
            ["event"] = ["emit", "publish", "consume", "handle", "replay", "enrich"],
            ["command"] = ["dispatch", "execute", "validate", "queue", "retry", "cancel"],
            ["handler"] = ["register", "resolve", "invoke", "chain", "decorate", "replace"],
            ["factory"] = ["create", "build", "configure", "wire", "cache", "resolve"],
        };

    // --- API & Platform Engineering ---
    private static readonly Dictionary<string, string[]> ApiPlatform =
        new(StringComparer.Ordinal)
        {
            ["endpoint"] = ["expose", "secure", "version", "deprecate", "throttle", "document"],
            ["webhook"] = ["register", "deliver", "sign", "verify", "retry", "disable"],
            ["gateway"] = ["route", "authorize", "throttle", "cache", "rewrite"],
            ["tenant"] = ["provision", "isolate", "migrate", "suspend", "activate", "offboard"],
            ["featureflag"] = ["enable", "disable", "rollout", "target", "evaluate", "retire"],
            ["job"] = ["schedule", "enqueue", "run", "retry", "cancel", "monitor"],
            ["workflow"] = ["start", "advance", "pause", "resume", "cancel", "complete"],
            ["rule"] = ["define", "evaluate", "prioritize", "enforce", "override", "disable"],
            ["template"] = ["render", "compile", "validate", "version", "override", "publish"],
            ["artifact"] = ["build", "publish", "sign", "promote", "download", "verify"],
        };

    // --- Observability & Reliability ---
    private static readonly Dictionary<string, string[]> Observability =
        new(StringComparer.Ordinal)
        {
            ["metric"] = ["record", "aggregate", "export", "tag", "sample", "reset"],
            ["trace"] = ["start", "annotate", "propagate", "sample", "flush", "end"],
            ["span"] = ["start", "annotate", "tag", "link", "flush", "finish"],
            ["alert"] = ["trigger", "silence", "acknowledge", "escalate", "resolve"],
            ["incident"] = ["declare", "triage", "escalate", "mitigate", "resolve", "postmortem"],
            ["dashboard"] = ["build", "publish", "share", "refresh", "drilldown", "archive"],
            ["log"] = ["write", "parse", "filter", "ship", "redact", "correlate"],
            ["checkpoint"] = ["create", "restore", "persist", "prune", "verify", "rotate"],
        };

    // --- Machine Learning & AI ---
    private static readonly Dictionary<string, string[]> MlAi =
        new(StringComparer.Ordinal)
        {
            ["model"] = ["train", "validate", "evaluate", "serve", "deploy", "retrain"],
            ["embedding"] = ["generate", "index", "normalize", "store", "cache", "search"],
            ["prompt"] = ["compose", "template", "ground", "evaluate", "sanitize", "version"],
            ["classifier"] = ["train", "score", "calibrate", "threshold", "evaluate", "serve"],
            ["prediction"] = ["generate", "score", "explain", "cache", "serve", "audit"],
            ["experiment"] = ["design", "run", "track", "compare", "promote", "archive"],
            ["label"] = ["assign", "review", "correct", "merge", "map", "validate"],
            ["featurestore"] = ["publish", "materialize", "backfill", "serve", "monitor",
                "deprecate"],
        };

    // --- Quality, Governance & Compliance Engineering ---
    private static readonly Dictionary<string, string[]> QualityGovernance =
        new(StringComparer.Ordinal)
        {
            ["testcase"] = ["define", "execute", "assert", "parameterize", "isolate",
                "stabilize"],
            ["fixture"] = ["prepare", "seed", "reset", "load", "teardown", "reuse"],
            ["baseline"] = ["establish", "compare", "refresh", "approve", "pin", "version"],
            ["benchmark"] = ["run", "compare", "profile", "optimize", "track", "report"],
            ["regression"] = ["detect", "reproduce", "triage", "fix", "verify", "prevent"],
            ["coverage"] = ["measure", "report", "increase", "enforce", "track", "gate"],
            ["runbook"] = ["author", "version", "execute", "review", "validate", "retire"],
            ["playbook"] = ["draft", "execute", "simulate", "update", "review", "publish"],
            ["auditlog"] = ["capture", "append", "seal", "query", "retain", "export"],
            ["control"] = ["define", "implement", "test", "enforce", "monitor", "audit"],
            ["evidence"] = ["collect", "attach", "review", "retain", "export", "verify"],
            ["finding"] = ["record", "triage", "assign", "remediate", "verify", "close"],
            ["lineage"] = ["capture", "trace", "visualize", "validate", "repair", "publish"],
            ["policy"] = ["evaluate", "enforce", "apply", "override", "simulate", "attest"],
            ["compliance"] = ["assess", "monitor", "report", "attest", "remediate", "enforce"],
        };

    private static readonly Dictionary<string, IReadOnlyCollection<string>>
        AllCollocations = new Dictionary<string, string[]>[]
        {
            Finance, Ecommerce, Healthcare, Hospitality, Telecom, Gaming, Logistics, Insurance,
            Education, RealEstate, Hr, Security, DevOps, Data, Content, Social, Messaging, Iot,
            Legal, Manufacturing, Agriculture, Advertising, Transportation, Energy, Blockchain,
            PublicSector, PharmaBiotech, SupportCrm, PaymentsFintech, MediaAdTech, Programming,
            ApiPlatform, Observability, MlAi, QualityGovernance,
        }
            .SelectMany(domain => domain)
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)new HashSet<string>(
                    group.SelectMany(entry => entry.Value), StringComparer.Ordinal),
                StringComparer.Ordinal);

    private static readonly IReadOnlyCollection<string> NoVerbs = [];

    /// <summary>Every noun in the merged lookup, lowercase; the drift-guard surface.</summary>
    public static IReadOnlyCollection<string> Nouns => AllCollocations.Keys;

    /// <summary>Verbs that idiomatically collocate with the noun; empty when unknown.</summary>
    /// <param name="noun">Domain noun, matched case-insensitively; may be null or blank.</param>
    /// <returns>The preferred verbs, or an empty collection for an unknown or blank noun.</returns>
    public static IReadOnlyCollection<string> PreferredVerbs(string? noun)
    {
        var key = noun ?? string.Empty;
        if (string.IsNullOrWhiteSpace(key))
        {
            return NoVerbs;
        }

        return AllCollocations.TryGetValue(key.ToLowerInvariant(), out var verbs)
            ? verbs
            : NoVerbs;
    }

    /// <summary>Whether the verb is a preferred collocation for the noun.</summary>
    /// <param name="verb">Candidate verb, matched case-insensitively; may be null or blank.</param>
    /// <param name="noun">Domain noun, matched case-insensitively; may be null or blank.</param>
    /// <returns><c>true</c> when the verb collocates with the noun; otherwise <c>false</c>.</returns>
    public static bool IsPreferred(string? verb, string? noun)
    {
        var candidate = verb ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return PreferredVerbs(noun).Contains(candidate.ToLowerInvariant());
    }
}
