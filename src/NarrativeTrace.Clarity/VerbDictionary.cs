// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Classifies verbs as domain-specific, standard, generic, or boolean prefixes
/// for method-name scoring. The domain vocabulary is a union of 34 industry /
/// technical category sets shared across NarrativeTrace runtimes (CLARITY-1). The four
/// category sets are mutually exclusive between generic/boolean and domain
/// (enforced by drift-guard tests, mirroring Java's static overlap check).
/// </summary>
public static class VerbDictionary
{
    // --- Domain verbs organized by industry / technical domain ---

    private static readonly HashSet<string> Finance =
    [
        "accrue", "amortize", "balance", "bill", "borrow", "budget", "capitalize",
        "charge", "compound", "consolidate", "credit", "debit", "deposit",
        "depreciate", "disburse", "earn", "encumber", "expense", "finance",
        "forfeit", "fund", "hedge", "hold", "invest", "invoice", "lend", "levy",
        "liquidate", "lower", "margin", "mature", "monetize", "negotiate", "net",
        "owe", "pay", "pledge", "prorate", "raise", "rebalance", "reconcile",
        "redeem", "refund", "reimburse", "remit", "repay", "settle", "spend",
        "subsidize", "tax", "trade", "transact", "transfer", "underwrite", "vest",
        "void", "waive", "withdraw", "collateralize", "originate", "prepay",
        "securitize", "short", "chargeoff", "impair", "reprice", "revalue",
        "writeoff", "shortsell",
    ];

    private static readonly HashSet<string> Ecommerce =
    [
        "auction", "backorder", "bid", "browse", "bundle", "buy", "cancel",
        "checkout", "confirm", "discount", "exchange", "finalize", "fulfill",
        "order", "preorder", "prepare", "price", "purchase", "quote", "rate",
        "restock", "sell", "upsell", "crosssell", "merchandise", "markdown",
        "upsize", "abandon", "reserve", "ship", "refund", "reprice", "dropship",
        "resell",
    ];

    private static readonly HashSet<string> Healthcare =
    [
        "administer", "admit", "consent", "diagnose", "discharge", "dose",
        "examine", "hospitalize", "immunize", "inoculate", "prescribe", "refer",
        "rehabilitate", "resuscitate", "screen", "sedate", "sterilize", "treat",
        "triage", "vaccinate", "auscultate", "catheterize", "defibrillate",
        "dispense", "extubate", "incise", "intubate", "palpate", "suture",
        "transfuse", "ventilate",
    ];

    private static readonly HashSet<string> Hospitality =
    [
        "accommodate", "book", "cater", "checkin", "lodge", "rebook", "reserve",
        "seat", "upgrade", "vacate", "checkout", "downgrade", "housekeep",
        "overbook", "reconfirm", "ticket", "upsell", "waitlist",
    ];

    private static readonly HashSet<string> Telecom =
    [
        "broadcast", "buffer", "dial", "forward", "handoff", "multiplex", "mute",
        "page", "patch", "ping", "relay", "roam", "route", "signal", "transmit",
        "tunnel", "deprovision", "demodulate", "handover", "modulate", "originate",
        "provision", "redial", "shape", "terminate", "throttle",
    ];

    private static readonly HashSet<string> Gaming =
    [
        "aggro", "ban", "buff", "craft", "debuff", "dodge", "equip", "grab",
        "grind", "heal", "kick", "loot", "matchmake", "nerf", "parry", "quest",
        "rank", "respawn", "revive", "score", "slay", "spawn", "summon", "teleport",
        "wield", "forfeit", "levelup", "lobby", "match", "prestige", "queue",
        "reroll", "spectate",
    ];

    private static readonly HashSet<string> Logistics =
    [
        "backlog", "consign", "coordinate", "deliver", "dispatch", "expedite",
        "freight", "inventory", "label", "manifest", "pack", "palletize", "pick",
        "prioritize", "replenish", "reroute", "ship", "stow", "track", "unload",
        "unpack", "warehouse", "backhaul", "crossdock", "deconsolidate", "dock",
        "linehaul", "slot", "sort", "transload",
    ];

    private static readonly HashSet<string> Insurance =
    [
        "adjust", "annuitize", "assess", "claim", "commute", "cover", "endorse",
        "exclude", "indemnify", "insure", "lapse", "litigate", "reinsure",
        "reinstate", "renew", "subrogate", "underwrite", "quote", "bind", "rate",
        "nonrenew", "rescind", "cancel", "adjudicate", "underpay", "overpay",
    ];

    private static readonly HashSet<string> Education =
    [
        "accredit", "assign", "certify", "counsel", "enroll", "evaluate", "exempt",
        "expel", "grade", "graduate", "guide", "matriculate", "mentor", "proctor",
        "submit", "tutor", "instruct", "lecture", "remediate", "differentiate",
        "accommodate", "advise", "discipline", "readmit",
    ];

    private static readonly HashSet<string> RealEstate =
    [
        "appraise", "broker", "condemn", "demolish", "escrow", "evict", "foreclose",
        "inspect", "lease", "mortgage", "occupy", "rent", "rezone", "subdivide",
        "survey", "zone", "stage", "relet", "deed", "title", "record", "notarize",
        "relist", "vacate",
    ];

    private static readonly HashSet<string> Hr =
    [
        "compensate", "demote", "dismiss", "furlough", "hire", "interview", "lead",
        "offboard", "onboard", "promote", "recruit", "relocate", "retire",
        "terminate", "coach", "discipline", "evaluate", "incentivize", "backfill",
        "upskill", "reskill", "redeploy", "retain", "reclassify", "downsize",
        "rightsize", "reassign",
    ];

    private static readonly HashSet<string> Security =
    [
        "authenticate", "authorize", "blacklist", "challenge", "cipher",
        "deauthenticate", "deauthorize", "decrypt", "deny", "deprovision", "detect",
        "elevate", "encrypt", "expire", "firewall", "grant", "hash", "impersonate",
        "intercept", "isolate", "lock", "obfuscate", "pentest", "quarantine",
        "redact", "register", "remediate", "revoke", "rotate", "sanitize", "scan",
        "sign", "suspend", "tokenize", "unlock", "unregister", "verify", "whitelist",
        "attest", "declassify", "harden", "jail", "sandbox", "sequester", "unseal",
        "vault", "allowlist", "blocklist",
    ];

    private static readonly HashSet<string> DevOps =
    [
        "allocate", "autoscale", "bind", "bootstrap", "cache", "canary", "clone",
        "compile", "compress", "configure", "containerize", "decommission",
        "deploy", "discover", "drain", "drop", "failover", "flush", "healthcheck",
        "host", "hydrate", "initialize", "inject", "install", "instrument",
        "invalidate", "invoke", "keep", "launch", "lint", "loadbalance", "migrate",
        "monitor", "observe", "orchestrate", "override", "pool", "probe",
        "propagate", "provision", "prune", "purge", "reboot", "recover", "refresh",
        "rehash", "release", "reload", "replicate", "resolve", "restart", "restore",
        "retry", "rollback", "scale", "seed", "serve", "shard", "shrink", "snapshot",
        "stack", "stage", "stash", "synchronize", "throttle", "tombstone", "trace",
        "trigger", "tune", "undeploy", "unpin", "warm", "wire", "wrap", "cordon",
        "evict", "promote", "rollout", "rollforward", "rollup", "taint", "untaint",
        "remount", "reschedule",
    ];

    private static readonly HashSet<string> Data =
    [
        "aggregate", "anonymize", "backfill", "bin", "calculate", "capture",
        "classify", "cleanse", "cluster", "correlate", "crawl", "deduplicate",
        "denormalize", "derive", "downsample", "enrich", "estimate", "export",
        "extract", "feed", "import", "infer", "ingest", "interpolate", "link",
        "load", "lookup", "match", "measure", "merge", "normalize", "partition",
        "persist", "pipeline", "pivot", "populate", "precompute", "prefetch",
        "profile", "pseudonymize", "quantize", "sample", "segment", "serialize",
        "store", "summarize", "synthesize", "transform", "transpose", "traverse",
        "trim", "truncate", "unlink", "upsert", "validate", "bucketize", "featurize",
        "impute", "materialize", "repartition", "window", "rank", "dedupe",
        "coalesce", "vectorize",
    ];

    private static readonly HashSet<string> Content =
    [
        "annotate", "archive", "bookmark", "caption", "censor", "crop", "curate",
        "draft", "dub", "embed", "feature", "highlight", "index", "mark", "moderate",
        "narrate", "post", "stream", "subtitle", "syndicate", "tag", "thumbnail",
        "transcode", "transcribe", "translate", "watermark", "factcheck", "geotag",
        "localize", "proofread", "publish", "redline", "serialize", "version",
        "voiceover",
    ];

    private static readonly HashSet<string> Social =
    [
        "befriend", "block", "comment", "downvote", "flag", "follow", "invite",
        "like", "mention", "pin", "react", "report", "repost", "share", "unblock",
        "unfollow", "upvote", "livestream", "mute", "quote", "reshare", "subscribe",
        "unmute", "unsend",
    ];

    private static readonly HashSet<string> Messaging =
    [
        "acknowledge", "bounce", "dequeue", "emit", "enqueue", "escalate", "fanout",
        "notify", "queue", "replay", "ack", "deadletter", "nack", "park", "publish",
        "redeliver", "retry", "settle",
    ];

    private static readonly HashSet<string> Iot =
    [
        "actuate", "arm", "beacon", "blink", "calibrate", "disarm", "flash", "mesh",
        "pair", "poll", "sense", "simulate", "unpair", "commission", "decommission",
        "provision", "deprovision", "sample", "telemeter", "reboot", "sleep", "wake",
    ];

    private static readonly HashSet<string> Legal =
    [
        "adjudicate", "amend", "appeal", "approve", "arbitrate", "attest", "audit",
        "codify", "comply", "depose", "enforce", "file", "mandate", "notarize",
        "petition", "ratify", "regulate", "reject", "sanction", "seal", "stipulate",
        "subpoena", "brief", "counterclaim", "indict", "litigate", "negotiate",
        "plead", "prosecute", "remand", "waive",
    ];

    private static readonly HashSet<string> Manufacturing =
    [
        "assemble", "batch", "cast", "cure", "engrave", "extrude", "fabricate",
        "forge", "laminate", "machine", "mill", "mold", "package", "polish",
        "produce", "rivet", "solder", "stamp", "temper", "weld", "yield", "anneal",
        "braze", "deburr", "electroplate", "sinter", "tool", "torque", "diecut",
        "harden",
    ];

    private static readonly HashSet<string> Agriculture =
    [
        "brew", "compost", "cultivate", "distill", "distribute", "ferment",
        "fertilize", "graft", "graze", "grow", "harvest", "irrigate", "pasteurize",
        "plant", "reap", "ripen", "sow", "thresh", "cull", "drain", "mulch", "prune",
        "rototill", "spray", "transplant", "wean", "weed", "winterize",
    ];

    private static readonly HashSet<string> Advertising =
    [
        "advertise", "attribute", "boost", "click", "engage", "funnel", "market",
        "nurture", "optimize", "personalize", "pitch", "retarget", "sponsor",
        "target", "activate", "allocate", "bid", "brand", "brief", "convert",
        "position", "prospect", "qualify", "remarket", "segment", "upsell",
        "acquire", "monetize",
    ];

    private static readonly HashSet<string> Transportation =
    [
        "arrive", "board", "charter", "delay", "depart", "detour", "disembark",
        "dock", "embark", "ferry", "navigate", "park", "pass", "schedule", "taxi",
        "tow", "berth", "deplane", "haul", "layover", "moor", "refuel", "reroute",
        "unmoor", "waypoint",
    ];

    private static readonly HashSet<string> Energy =
    [
        "conserve", "consume", "curtail", "desalinate", "forecast", "generate",
        "harness", "meter", "ration", "recycle", "shed", "backfeed", "blackstart",
        "decarbonize", "deenergize", "derate", "dispatch", "energize", "island",
        "ramp", "redispatch", "wheel",
    ];

    private static readonly HashSet<string> Blockchain =
    [
        "airdrop", "bridge", "burn", "delegate", "farm", "fork", "govern", "mine",
        "mint", "peg", "stake", "swap", "unstake", "bond", "claim", "finalize",
        "redelegate", "restake", "slash", "unbond",
    ];

    private static readonly HashSet<string> PublicSector =
    [
        "adopt", "annex", "appropriate", "charter", "debar", "elect", "enact",
        "gazette", "inaugurate", "legislate", "license", "marshal", "procure",
        "promulgate", "ratify", "redistrict", "register", "rezone",
    ];

    private static readonly HashSet<string> PharmaBiotech =
    [
        "aliquot", "assay", "centrifuge", "compound", "culture", "dilute", "ferment",
        "formulate", "incubate", "inoculate", "lyophilize", "pipette", "sequence",
        "synthesize", "titrate", "validate",
    ];

    private static readonly HashSet<string> SupportCrm =
    [
        "acknowledge", "categorize", "deescalate", "deflect", "escalate", "handoff",
        "prioritize", "queue", "reassign", "reopen", "reroute", "resolve", "snooze",
        "triage", "unassign", "unsnooze",
    ];

    private static readonly HashSet<string> PaymentsFintech =
    [
        "authorize", "capture", "chargeback", "clear", "decline", "detokenize",
        "dispute", "fund", "increment", "preauthorize", "present", "reauthorize",
        "reconcile", "refund", "reverse", "settle", "tokenize", "void",
    ];

    private static readonly HashSet<string> MediaAdTech =
    [
        "bid", "brandlift", "cap", "flight", "geofence", "impress", "pace", "pixel",
        "retarget", "remarket", "segment", "sponsor", "target", "traffic",
    ];

    private static readonly HashSet<string> Programming =
    [
        "deserialize", "instantiate", "inspect", "pipe", "slice", "wire", "compile",
        "serialize", "tokenize", "sanitize",
    ];

    private static readonly HashSet<string> ApiPlatform =
    [
        "deprecate", "document", "offboard", "rewrite", "throttle", "retry",
        "schedule", "enqueue", "route", "version",
    ];

    private static readonly HashSet<string> Observability =
    [
        "annotate", "correlate", "drilldown", "escalate", "mitigate", "postmortem",
        "sample", "silence", "trigger", "acknowledge",
    ];

    private static readonly HashSet<string> MlAi =
    [
        "train", "retrain", "evaluate", "calibrate", "threshold", "rerank", "ground",
        "explain", "materialize", "vectorize",
    ];

    private static readonly HashSet<string> DomainVerbSet =
    [
        .. Finance, .. Ecommerce, .. Healthcare, .. Hospitality, .. Telecom,
        .. Gaming, .. Logistics, .. Insurance, .. Education, .. RealEstate, .. Hr,
        .. Security, .. DevOps, .. Data, .. Content, .. Social, .. Messaging,
        .. Iot, .. Legal, .. Manufacturing, .. Agriculture, .. Advertising,
        .. Transportation, .. Energy, .. Blockchain, .. PublicSector,
        .. PharmaBiotech, .. SupportCrm, .. PaymentsFintech, .. MediaAdTech,
        .. Programming, .. ApiPlatform, .. Observability, .. MlAi,
    ];

    private static readonly HashSet<string> StandardVerbSet =
    [
        "create", "define", "find", "delete", "update", "convert", "parse",
        "render", "format", "filter", "sort", "send", "receive", "publish",
        "subscribe", "save", "remove", "add", "insert", "select", "fetch", "read",
        "write", "open", "close", "start", "stop", "build", "check", "count",
        "compare", "copy", "move", "clear", "reset", "enable", "disable", "show",
        "hide", "attach", "detach", "advance", "connect", "disconnect", "encode",
        "decode", "map", "reduce", "collect", "group", "flatten", "wrap", "unwrap",
        "log", "print", "display", "list", "search", "query", "test", "assert",
        "throw", "catch", "await", "join", "split", "replace", "append", "prepend",
        "push", "pop", "peek", "poll", "put", "take", "offer", "drain", "fill",
        "empty", "notify", "listen", "watch", "emit", "expose", "on", "off",
        "toggle", "abort", "accept", "allow", "analyze", "argue", "augment",
        "benchmark", "breach", "breed", "chain", "chart", "code", "comp", "complete",
        "correct", "coupon", "curve", "default", "delist", "deplete", "differential",
        "discard", "disenchant", "diversify", "divert", "draw", "edit", "eliminate",
        "enchant", "ensure", "evolve", "expand", "extend", "haircut", "hijack",
        "increase", "initiate", "interconnect", "issue", "kill", "leave", "level",
        "lose", "mask", "overturn", "paginate", "pause", "place", "plan", "port",
        "rebuild", "repeal", "repeat", "request", "resize", "resume", "retool",
        "retract", "return", "review", "rule", "scramble", "scrub", "shortlist",
        "slide", "stabilize", "standardize", "sublease", "suppress", "switch",
        "sync", "tamper", "uncordon", "uphold", "upload", "value", "win", "withhold",
        "amplify", "attenuate", "author", "commit", "compose", "compute", "declare",
        "decorate", "design", "discontinue", "download", "enter", "establish",
        "exit", "finish", "fix", "freeze", "gate", "honor", "implement", "modify",
        "parameterize", "prevent", "repair", "reproduce", "reuse", "secure",
        "teardown", "template", "visit", "visualize", "walk",
    ];

    private static readonly HashSet<string> GenericVerbSet =
    [
        "get", "set", "process", "handle", "execute", "do", "run", "perform",
        "manage", "apply", "make", "call", "use", "go", "work", "try", "begin",
        "end", "init", "setup", "cleanup",
    ];

    private static readonly HashSet<string> BooleanPrefixSet =
    [
        "is", "has", "can", "should", "was", "will", "contains", "exists",
        "matches", "supports", "needs", "allows", "requires", "includes", "accepts",
        "enables", "equals",
    ];

    /// <summary>The union of the 34 domain-verb category sets (deduplicated).</summary>
    public static IReadOnlyCollection<string> DomainVerbs => DomainVerbSet;

    /// <summary>Common, tool-agnostic verbs that read clearly but carry no domain intent.</summary>
    public static IReadOnlyCollection<string> StandardVerbs => StandardVerbSet;

    /// <summary>Vague verbs that reveal little about behaviour (e.g. handle, process).</summary>
    public static IReadOnlyCollection<string> GenericVerbs => GenericVerbSet;

    /// <summary>Boolean-question prefixes typical of predicate methods (is, has, can).</summary>
    public static IReadOnlyCollection<string> BooleanPrefixes => BooleanPrefixSet;

    /// <summary>Classifies a verb into its category; unknown verbs return <see cref="VerbCategory.Unknown"/>.</summary>
    /// <param name="verb">The identifier token to classify.</param>
    /// <param name="vocabulary">
    /// The project's declared vocabulary, or null for the built-in tiers alone.
    /// </param>
    /// <remarks>
    /// Boolean prefixes, built-in domain verbs and generic verbs are decided
    /// first, so a project cannot promote <c>process</c> or <c>is</c> by
    /// committing them to its glossary — generic stays generic. What a project
    /// can do is claim a word the built-in dictionaries would score as merely
    /// standard or unknown: in its domain, <c>fold</c> and <c>send</c> are
    /// domain verbs. Generic is checked before standard here, which is
    /// behaviour-preserving only because the two tiers are disjoint — the
    /// drift-guard tests assert that, matching Java's overlap check.
    /// </remarks>
    public static VerbCategory Classify(
        string verb, DomainVocabulary? vocabulary = null)
    {
        var lower = verb.ToLowerInvariant();
        var authoritative = ClassifyAuthoritative(lower);
        if (authoritative != VerbCategory.Unknown)
        {
            return authoritative;
        }

        if (vocabulary is not null && vocabulary.IsDomainVerb(lower))
        {
            return VerbCategory.Domain;
        }

        return StandardVerbSet.Contains(lower)
            ? VerbCategory.Standard
            : VerbCategory.Unknown;
    }

    /// <summary>
    /// The tiers a project's committed glossary can never override: boolean
    /// prefixes, built-in domain verbs, and generic verbs. Returns
    /// <see cref="VerbCategory.Unknown"/> when the verb belongs to none of them
    /// — the only case in which the project's own vocabulary is consulted.
    /// </summary>
    private static VerbCategory ClassifyAuthoritative(string lower)
    {
        if (BooleanPrefixSet.Contains(lower))
        {
            return VerbCategory.Boolean;
        }

        if (DomainVerbSet.Contains(lower))
        {
            return VerbCategory.Domain;
        }

        return GenericVerbSet.Contains(lower)
            ? VerbCategory.Generic
            : VerbCategory.Unknown;
    }
}
