// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills;

/// <summary>
/// How fully a skill's steps can be replayed and its outcome checked
/// mechanically (skill-harness design §7).
/// </summary>
public enum SkillClass
{
    /// <summary>Steps are fully replayable and the outcome is fully specified.</summary>
    Mechanical,

    /// <summary>A replayable core, with judgment at the edges.</summary>
    Guided,

    /// <summary>The outcome is a quality delta, not an artifact a replay can assert on.</summary>
    Judgmental,
}

/// <summary>One symptom → cause → fix triple for a step's <c>failure:</c> line.</summary>
public sealed record FailureNote(string Symptom, string Cause, string Fix);

/// <summary>
/// One <c>Always</c>/<c>Never</c> bullet. Always carries its reason — a
/// naked prohibition doesn't survive an edge case, a reasoned one does.
/// </summary>
public sealed record ReasonedRule(string Rule, string Reason);

/// <summary>What a step actually does: run commands, or embed a real source file verbatim.</summary>
public interface IStepBody;

/// <summary>Shell commands, run in order. Every first token must be in <see cref="CommandVocabulary.AllowedTools"/>.</summary>
public sealed record CommandStep(IReadOnlyList<string> Commands) : IStepBody;

/// <summary>
/// A real, runnable source file (or a named <c>snippet:begin</c>/<c>snippet:end</c> region
/// within it) embedded verbatim — never a second, hand-copied literal.
/// </summary>
public sealed record SnippetStep(string Path, string Language, string? Region = null, string? Mask = null) : IStepBody;

/// <summary>
/// A named block of reference text a skill renders after its steps — a table or a short list the
/// steps point at, which is neither a step nor an always/never rule.
/// </summary>
/// <remarks>
/// INTENT: two skills that read traces share one "how to read a trace" text; a section is how that
/// text is written once in the catalogue and rendered into both pages identically, instead of being
/// copied into a step's prose where the copies drift.
/// </remarks>
public sealed record SkillSection
{
    /// <summary>A section with a one-line plain-text <paramref name="heading"/> and a non-blank body.</summary>
    /// <param name="heading">
    /// Rendered as a level-two heading: one line, not itself a heading marker, and not starting with
    /// whitespace (an indented <c>##</c> is still a marker).
    /// </param>
    /// <param name="markdown">The body, rendered as written.</param>
    /// <exception cref="ArgumentException">The heading is blank, spans lines or starts with <c>#</c>; or the body is blank.</exception>
    public SkillSection(string heading, string markdown)
    {
        if (string.IsNullOrWhiteSpace(heading) || heading.Contains('\n') || heading.Contains('\r')
            || heading.StartsWith('#') || char.IsWhiteSpace(heading[0]))
        {
            throw new ArgumentException("a SkillSection's heading is one line of plain text: " + heading, nameof(heading));
        }

        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new ArgumentException("a SkillSection's markdown must not be blank", nameof(markdown));
        }

        Heading = heading;
        Markdown = markdown;
    }

    /// <summary>The heading text, without its <c>##</c>.</summary>
    public string Heading { get; }

    /// <summary>The body, as written.</summary>
    public string Markdown { get; }
}

/// <summary>One numbered step in a skill.</summary>
/// <param name="Title">
/// The step's heading — carries the instruction itself for a guided/judgmental step, since
/// this schema has no separate prose field (mirrors the canonical TypeScript source's own shape).
/// </param>
/// <param name="Body">What the step actually does.</param>
/// <param name="Verify">
/// The step's own definition of done — asserts THIS step's specific claim, never a proxy metric.
/// <see langword="null"/> only for a judgmental step with nothing mechanical to check.
/// </param>
/// <param name="Failure">Symptom → cause → fix triples for when the step doesn't hold.</param>
/// <param name="Flag">
/// A short, honest caveat rendered verbatim (e.g. <c>"unstudied — eval cell pending"</c>).
/// <see langword="null"/> for an unflagged step.
/// </param>
/// <param name="Condition">
/// When present, rendered as a <c>**when:**</c> line under the heading: the step applies only then, and
/// the page says what to do instead — prose a reader or agent branches on, never a framework list.
/// <see langword="null"/> for an unconditional step; never blank.
/// </param>
public sealed record SkillStep(
    string Title,
    IStepBody Body,
    string? Verify = null,
    IReadOnlyList<FailureNote>? Failure = null,
    string? Flag = null,
    string? Condition = null)
{
    /// <summary>The step's condition; never blank when present.</summary>
    public string? Condition
    {
        get;
        init => field = RejectBlank(value);
    } = RejectBlank(Condition);

    private static string? RejectBlank(string? condition) =>
        condition is not null && string.IsNullOrWhiteSpace(condition)
            ? throw new ArgumentException("a SkillStep's condition must not be blank when present", nameof(condition))
            : condition;

    /// <summary>Empty when this step carries no failure notes.</summary>
    public IReadOnlyList<FailureNote> FailureNotes => Failure ?? [];
}

/// <summary>One catalogue entry — a full agent-playbook skill.</summary>
/// <param name="CanonicalName">
/// The stable, platform-neutral name (e.g. <c>narrativetrace-doctor</c>) — the single source for
/// both the frontmatter <c>name:</c> and the directory a skill renders under on every platform
/// (<c>.claude/skills/&lt;CanonicalName&gt;/</c>, <c>.agents/skills/&lt;CanonicalName&gt;/</c>).
/// Skill names must be globally self-identifying: Codex and Gemini have flat namespaces with no
/// qualified fallback, and a repo-level <c>.claude/skills/</c> directory is a flat namespace too —
/// it is not a plugin, so a shortened segment like <c>doctor</c> would collide with every other
/// vendor's <c>doctor</c> skill (skills-design ruling, 2026-09-04, reaffirmed 2026-09-13).
/// </param>
/// <param name="SkillClass">How mechanically replayable this skill's steps are.</param>
/// <param name="Description">
/// Frontmatter <c>description</c>: third person, states WHAT and WHEN with the user's literal
/// trigger phrasings. Bounded by <see cref="SkillLints.DescriptionBudget"/>.
/// </param>
/// <param name="WhenToUse">Optional frontmatter <c>when_to_use</c> clause.</param>
/// <param name="Fixture">The pinned example project every step replays against.</param>
/// <param name="Steps">The numbered steps, in order.</param>
/// <param name="Always">Reasoned rules the skill always follows.</param>
/// <param name="Never">Reasoned rules the skill never violates.</param>
/// <param name="AllowedTools">
/// The bare commands the Claude flavour pre-approves for the turn that loads the skill, or
/// <see langword="null"/> for the whole <see cref="CommandVocabulary.AllowedTools"/>. An EMPTY list
/// declares that nothing is pre-approved and the rendered page carries no <c>allowed-tools</c> line.
/// </param>
public sealed record Skill(
    string CanonicalName,
    SkillClass SkillClass,
    string Description,
    string? WhenToUse,
    string Fixture,
    IReadOnlyList<SkillStep> Steps,
    IReadOnlyList<ReasonedRule> Always,
    IReadOnlyList<ReasonedRule> Never,
    IReadOnlyList<string>? AllowedTools = null)
{
    /// <summary>
    /// Reference sections rendered after the steps and before the always/never rules, in order;
    /// empty for a skill whose steps say everything.
    /// </summary>
    public IReadOnlyList<SkillSection> Sections { get; init; } = [];

    /// <summary>The commands this skill pre-approves: its own declaration, else the whole vocabulary.</summary>
    public IReadOnlyList<string> PreApprovedTools => AllowedTools ?? CommandVocabulary.AllowedTools;
}
