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

/// <summary>One numbered step in a skill.</summary>
/// <param name="Title">
/// The step's heading — carries the instruction itself for a guided/judgmental step, since
/// this schema has no separate prose field (mirrors the golden TypeScript source's own shape).
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
public sealed record SkillStep(
    string Title,
    IStepBody Body,
    string? Verify = null,
    IReadOnlyList<FailureNote>? Failure = null,
    string? Flag = null)
{
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
public sealed record Skill(
    string CanonicalName,
    SkillClass SkillClass,
    string Description,
    string? WhenToUse,
    string Fixture,
    IReadOnlyList<SkillStep> Steps,
    IReadOnlyList<ReasonedRule> Always,
    IReadOnlyList<ReasonedRule> Never);
