// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Skills;

/// <summary>
/// Tier A lints (skill-harness design §4.1): fast, offline, deterministic checks over the
/// catalogue's own data — no rendering, no replay, no network.
/// </summary>
public static class SkillLints
{
    /// <summary>Per-skill frontmatter <c>description</c> budget, in characters.</summary>
    public const int DescriptionBudget = 1024;

    /// <summary>Catalogue-wide description budget — a conservative proxy for a ~15k-token cliff.</summary>
    public const int CatalogueCharBudget = 40_000;

    private static readonly Regex MarkdownFilename = new(@"\b[\w.-]+\.md\b", RegexOptions.Compiled);

    /// <summary>Skills whose <see cref="Skill.Description"/> exceeds <see cref="DescriptionBudget"/>.</summary>
    public static IReadOnlyList<string> DescriptionBudgetViolations(IReadOnlyList<Skill> skills)
    {
        return skills.Where(s => s.Description.Length > DescriptionBudget)
            .Select(s => s.CanonicalName)
            .ToList();
    }

    /// <summary>Total description characters across the whole catalogue.</summary>
    public static int CatalogueDescriptionChars(IReadOnlyList<Skill> skills)
    {
        return skills.Sum(s => s.Description.Length + (s.WhenToUse?.Length ?? 0));
    }

    /// <summary>
    /// <c>"skill: command"</c> pairs whose command's first token is outside
    /// <see cref="CommandVocabulary.AllowedTools"/>.
    /// </summary>
    public static IReadOnlyList<string> VocabularyViolations(IReadOnlyList<Skill> skills)
    {
        return skills.SelectMany(VocabularyViolations).ToList();
    }

    private static IEnumerable<string> VocabularyViolations(Skill skill)
    {
        return skill.Steps
            .SelectMany(step => CommandsOf(step.Body))
            .Where(command => !CommandVocabulary.IsAllowed(command))
            .Select(command => $"{skill.CanonicalName}: {command}");
    }

    private static IReadOnlyList<string> CommandsOf(IStepBody body)
    {
        return body is CommandStep commands ? commands.Commands : [];
    }

    /// <summary>Step titles with no <see cref="SkillStep.Verify"/> — advisory, not itself a failure.</summary>
    public static IReadOnlyList<string> StepsWithoutVerify(IReadOnlyList<Skill> skills)
    {
        return skills
            .SelectMany(s => s.Steps.Where(step => step.Verify is null).Select(step => $"{s.CanonicalName}: {step.Title}"))
            .ToList();
    }

    /// <summary>
    /// Citation violations across every rendered prose field: a section-mark character, or a
    /// referenced <c>.md</c> filename not present in <paramref name="repoMarkdownBasenames"/>.
    /// Never checks <see cref="Skill.CanonicalName"/> or resolved snippet file content.
    /// </summary>
    public static IReadOnlyList<string> CitationViolations(
        IReadOnlyList<Skill> skills, IReadOnlySet<string> repoMarkdownBasenames)
    {
        return skills.SelectMany(skill => CitationViolations(skill, repoMarkdownBasenames)).ToList();
    }

    private static IEnumerable<string> CitationViolations(Skill skill, IReadOnlySet<string> repoMarkdownBasenames)
    {
        foreach (var (label, text) in ProseFields(skill))
        {
            foreach (var violation in CitationViolationsInText(skill.CanonicalName, label, text, repoMarkdownBasenames))
            {
                yield return violation;
            }
        }
    }

    private static IEnumerable<(string Label, string Text)> ProseFields(Skill skill)
    {
        yield return ("description", skill.Description);
        if (skill.WhenToUse is not null)
        {
            yield return ("whenToUse", skill.WhenToUse);
        }

        foreach (var field in skill.Steps.SelectMany(StepProseFields))
        {
            yield return field;
        }

        foreach (var rule in skill.Always.Concat(skill.Never))
        {
            yield return ("rule", $"{rule.Rule} {rule.Reason}");
        }
    }

    private static IEnumerable<(string Label, string Text)> StepProseFields(SkillStep step)
    {
        yield return ($"step '{step.Title}'", step.Title);
        if (step.Verify is not null)
        {
            yield return ($"step '{step.Title}' verify", step.Verify);
        }

        foreach (var failure in step.FailureNotes)
        {
            yield return ($"step '{step.Title}' failure", $"{failure.Symptom} {failure.Cause} {failure.Fix}");
        }
    }

    private static IEnumerable<string> CitationViolationsInText(
        string skillName, string label, string text, IReadOnlySet<string> repoMarkdownBasenames)
    {
        if (text.Contains('§', StringComparison.Ordinal))
        {
            yield return $"{skillName} {label}: section-mark citation";
        }

        var externalFilenames = MarkdownFilename.Matches(text)
            .Select(match => match.Value)
            .Where(filename => !repoMarkdownBasenames.Contains(filename));
        foreach (var filename in externalFilenames)
        {
            yield return $"{skillName} {label}: external filename citation ({filename})";
        }
    }

    /// <summary>
    /// Pro listings whose <see cref="ProListing.FeatureGuideStatusText"/> is not found verbatim
    /// in <paramref name="featureGuideContent"/>.
    /// </summary>
    public static IReadOnlyList<string> ListingsDisagreeingWithFeatureGuide(
        IReadOnlyList<ProListing> listings, string featureGuideContent)
    {
        return listings
            .Where(listing => !featureGuideContent.Contains(listing.FeatureGuideStatusText, StringComparison.Ordinal))
            .Select(listing => listing.CanonicalName)
            .ToList();
    }
}
