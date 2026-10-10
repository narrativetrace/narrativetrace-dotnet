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

    /// <summary>
    /// Entries of a declared tool vocabulary that are already written in a rendered platform
    /// spelling (containing <c>(</c>) rather than as a bare command.
    /// </summary>
    /// <remarks>
    /// Pins the split between the catalogue and the renderer: the vocabulary declares bare
    /// commands, and <see cref="CommandVocabulary.ClaudeToolPattern"/> is the one place a bare
    /// command becomes a platform-spelled tool pattern. A hand-written <c>Bash(git *)</c> in the
    /// vocabulary would render as <c>Bash(Bash(git *) *)</c> — a rule matching nothing, in a field
    /// whose whole purpose is to match something.
    /// </remarks>
    public static IReadOnlyList<string> AllowedToolsViolations(IReadOnlyList<string> allowedTools)
    {
        return allowedTools.Where(tool => tool.Contains('(', StringComparison.Ordinal)).ToList();
    }

    /// <summary>
    /// Commands that reach a channel where something becomes PUBLIC and must never be pre-approved
    /// by the skill that names them. A list rather than one literal: a second publishing command is
    /// a question somebody has to answer, not a regex somebody widens.
    /// </summary>
    private static readonly Regex[] PublishingCommands = [new(@"\bfeedback\b", RegexOptions.Compiled)];

    /// <summary>
    /// <c>"skill: declares allowed tool …"</c> for every skill whose steps can make something public
    /// while its <see cref="Skill.PreApprovedTools"/> would pre-approve the command doing it.
    /// </summary>
    /// <remarks>
    /// INTENT: the Claude flavour's <c>allowed-tools</c> grants its listed tools for the turn that
    /// LOADS the skill, without prompting. A reporting skill that kept the default vocabulary would
    /// pre-approve its own reporting command, and the harness would stop asking exactly where asking
    /// is the product. The absence of the declaration is the safety property, so it is linted rather
    /// than left to whoever edits the catalogue next.
    /// </remarks>
    public static IReadOnlyList<string> PublishingNotPreApprovedViolations(IReadOnlyList<Skill> skills)
    {
        return skills.SelectMany(PublishingViolations).ToList();
    }

    private static IEnumerable<string> PublishingViolations(Skill skill)
    {
        return skill.Steps
            .SelectMany(step => CommandsOf(step.Body))
            .Where(command => PublishingCommands.Any(pattern => pattern.IsMatch(command)))
            .Where(command => skill.PreApprovedTools.Contains(CommandVocabulary.FirstToken(command)))
            .Select(command =>
                $"{skill.CanonicalName}: declares allowed tool \"{CommandVocabulary.FirstToken(command)}\", " +
                $"which pre-approves its own publishing command \"{command}\" — a skill that files " +
                "something public must let the harness ask");
    }

    private static readonly Regex PromotingCommand =
        new(@"\bdotnet-narrativetrace(?:\.exe|\.dll)?\s+approve(?![\w-])", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// <c>"skill: promotes an approval baseline yet declares allowed tool …"</c>, once per pre-approved
    /// tool, for every skill whose steps run the <c>approve</c> verb.
    /// </summary>
    /// <remarks>
    /// INTENT: promoting a <c>.received.nt</c> writes the committed contract every later change is held
    /// to, on the user's yes. A page whose <c>allowed-tools</c> pre-approved any tool would let the turn
    /// that showed the review copy run the promotion (or commit it) without the harness asking —
    /// exactly where the gate says the turn must stop. The allowance is empty: such a skill declares
    /// nothing.
    /// </remarks>
    public static IReadOnlyList<string> PromotionNotPreApprovedViolations(IReadOnlyList<Skill> skills)
    {
        return skills
            .Where(skill => skill.Steps.SelectMany(step => CommandsOf(step.Body)).Any(PromotingCommand.IsMatch))
            .SelectMany(skill => skill.PreApprovedTools.Select(tool =>
                $"{skill.CanonicalName}: promotes an approval baseline yet declares allowed tool \"{tool}\" — " +
                "a skill that writes into the project on the user's yes must let the harness ask"))
            .ToList();
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

        foreach (var section in skill.Sections)
        {
            yield return ($"section '{section.Heading}'", $"{section.Heading} {section.Markdown}");
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

        if (step.Condition is not null)
        {
            yield return ($"step '{step.Title}' condition", step.Condition);
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
