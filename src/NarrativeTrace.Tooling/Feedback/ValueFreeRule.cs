// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The hard gate between a problem report and a leaked secret: one named rule per shape of runtime
/// value that must never reach a public issue.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: a report drafted by an agent is PUBLIC from the first second — there is no private inbox
/// to triage it first — so the only thing standing between a pasted trace and somebody's credential
/// is a check the verb runs before it will build a URL or a body file. Each rule carries a stable
/// <c>vf.*</c> id because the refusal has to name what to fix, not just say no.
/// </para>
/// <para>
/// <b>@llmNote</b> Data here, code in <see cref="ValueFreeMatchers"/>: an instance is an id, a reason
/// a person reads, and a reference to the matcher that decides. Adding a rule means adding an instance
/// and a matcher, never an <c>if</c> inside a caller — <see cref="ValueFreeCheck"/> walks
/// <see cref="All"/> and cannot know about a rule that is not there.
/// </para>
/// <para>
/// <b>@llmNote</b> These rules are deliberately STRICTER than the renderer's own redaction
/// (<c>RedactionPolicy</c> in the runtime this library never links against). The renderer refuses an
/// entropy heuristic because a false positive there silently blanks a user's data; here a false
/// positive is a refusal that names its rule and a false negative is a public leak, so the tradeoff
/// inverts. The cross-module security suite asserts the implication that matters — every value the
/// renderer redacts is also rejected here — and asserts it in that direction ONLY.
/// </para>
/// </remarks>
public sealed class ValueFreeRule
{
    /// <summary>A rendered call line: <c>method(param: value)</c> carries the argument's value.</summary>
    public static readonly ValueFreeRule RenderedCall = new(
        "vf.rendered-call",
        "a call line carries a parameter's VALUE — attach the structural trace (.nt) instead, which"
        + " carries the same shape without any value",
        ValueFreeMatchers.RenderedCall);

    /// <summary>A rendered outcome: an arrow followed by anything but the structural literal <c>value</c>.</summary>
    public static readonly ValueFreeRule RenderedOutcome = new(
        "vf.rendered-outcome",
        "an outcome arrow carries a RETURNED VALUE — the structural trace writes a bare"
        + " → value, an exception TYPE after !!, or ?? incomplete, and never a value",
        ValueFreeMatchers.RenderedOutcome);

    /// <summary>A rendered duration: the elapsed time a value-free artifact never carries.</summary>
    public static readonly ValueFreeRule Duration = new(
        "vf.duration",
        "an elapsed time is a runtime measurement, which means this text came from a rendered"
        + " narrative rather than from the structural trace",
        ValueFreeMatchers.Duration);

    /// <summary>The renderer's redaction marker, which no value-free artifact ever contains.</summary>
    public static readonly ValueFreeRule Marker = new(
        "vf.marker",
        "the redaction marker only appears where a value was redacted, so this text is a rendered"
        + " narrative — the structural trace has nothing to redact and never carries it",
        ValueFreeMatchers.Marker);

    /// <summary>A deny-listed field name with a value assigned to it.</summary>
    public static readonly ValueFreeRule NamedSecret = new(
        "vf.named-secret",
        "a field whose NAME says it holds a credential is shown with a value beside it — remove"
        + " the value; the name alone is shape and may stay",
        ValueFreeMatchers.NamedSecret);

    /// <summary>A value whose SHAPE is the secret: a credential prefix, a key block, a national id, a card.</summary>
    public static readonly ValueFreeRule ValueShape = new(
        "vf.value-shape",
        "a value here is shaped like a credential, a key, a national id or a card number —"
        + " remove it; a report never needs the value, only what happened",
        ValueFreeMatchers.ValueShape);

    /// <summary>A run of encoded characters dense enough to be a key rather than a word.</summary>
    public static readonly ValueFreeRule Entropy = new(
        "vf.entropy",
        "a long encoded run here looks like a key, a hash or an opaque identifier — remove it;"
        + " if it is genuinely not a secret, describe it in words instead of pasting it",
        ValueFreeMatchers.Entropy);

    /// <summary>An email address — somebody's personal data, whoever's it is.</summary>
    public static readonly ValueFreeRule Email = new(
        "vf.email",
        "an email address is personal data and a public issue is public forever — remove it;"
        + " the report does not need to say who",
        ValueFreeMatchers.Email);

    /// <summary>An absolute home directory, which carries whoever's login name.</summary>
    public static readonly ValueFreeRule HomePath = new(
        "vf.home-path",
        "an absolute home directory names the account it belongs to — the draft rewrites these"
        + " to ~ on its own, so one here means the text was edited by hand afterwards",
        ValueFreeMatchers.HomePath);

    /// <summary>A control character: never content, always a terminal escape or a smuggled byte.</summary>
    public static readonly ValueFreeRule Control = new(
        "vf.control",
        "a control character is not text a reader needs and is how a payload hides in one —"
        + " only a line feed and a tab belong in a report",
        ValueFreeMatchers.Control);

    private static readonly IReadOnlyList<ValueFreeRule> AllRules =
    [
        RenderedCall,
        RenderedOutcome,
        Duration,
        Marker,
        NamedSecret,
        ValueShape,
        Entropy,
        Email,
        HomePath,
        Control,
    ];

    private readonly Func<string, bool> _matcher;

    private ValueFreeRule(string id, string reason, Func<string, bool> matcher)
    {
        Id = id;
        Reason = reason;
        _matcher = matcher;
    }

    /// <summary>The stable rule id a refusal names, e.g. <c>vf.rendered-call</c>.</summary>
    public string Id { get; }

    /// <summary>Why this shape may not be filed, in the words the refusal prints.</summary>
    public string Reason { get; }

    /// <summary>
    /// Whether this rule refuses the given text.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// when <paramref name="text"/> is null — an absent field is <c>""</c> to every caller in this
    /// package, so a null here is a programming error, not empty input.
    /// </exception>
    public bool Rejects(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return _matcher(text);
    }

    /// <summary>Every rule, in declaration order — the order a refusal lists them in.</summary>
    public static IReadOnlyList<ValueFreeRule> All() => AllRules;
}
