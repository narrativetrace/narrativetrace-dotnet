// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.ObjectModel;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// The carrier's index: which runtime shipped it and which skills it carries.
/// </summary>
/// <remarks>
/// INTENT: the whole of <c>catalogue.json</c>, validated once at read time so every later stage —
/// planner, renderer, executor — can treat it as a fact rather than as input. Deliberately carries
/// NO version: the carrier's own coordinate is the stamp, so a checked-in catalogue never drifts from
/// the artifact that ships it.
/// </remarks>
public sealed record SkillCatalogue
{
    /// <param name="runtime">The runtime slug the carrier was rendered for.</param>
    /// <param name="skills">Every skill in catalogue order, each name appearing exactly once.</param>
    public SkillCatalogue(string runtime, IReadOnlyList<SkillEntry> skills)
    {
        if (string.IsNullOrWhiteSpace(runtime))
        {
            throw new ArgumentException("a catalogue's runtime must not be blank");
        }

        if (skills is null || skills.Count == 0)
        {
            throw new ArgumentException("a catalogue must list at least one skill");
        }

        Runtime = runtime;
        Skills = new ReadOnlyCollection<SkillEntry>([.. skills]);
        var distinct = Skills.Select(entry => entry.Name).Distinct(StringComparer.Ordinal).Count();
        if (distinct != Skills.Count)
        {
            throw new ArgumentException("a catalogue names a skill twice: " + Names(Skills));
        }
    }

    /// <summary>The runtime slug the carrier was rendered for.</summary>
    public string Runtime { get; }

    /// <summary>Every skill in catalogue order, each name appearing exactly once.</summary>
    public IReadOnlyList<SkillEntry> Skills { get; }

    /// <summary>The entry with this name, or <see langword="null"/> — the lookup an installer does per target directory.</summary>
    public SkillEntry? Skill(string name) =>
        Skills.FirstOrDefault(entry => string.Equals(entry.Name, name, StringComparison.Ordinal));

    private static string Names(IReadOnlyList<SkillEntry> skills) =>
        "[" + string.Join(", ", skills.Select(entry => entry.Name).OrderBy(name => name, StringComparer.Ordinal)) + "]";
}
