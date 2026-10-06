// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// One skill as the carrier's <c>catalogue.json</c> lists it: the name that becomes a directory in
/// the consumer project, the description an always-on agent pointer repeats verbatim, and the
/// carrier-relative path of each rendered flavour.
/// </summary>
/// <remarks>
/// INTENT: the installer's unit of work. Everything the planner needs about a skill is here, so a
/// plan can be computed without opening the carrier a second time. The two paths are relative to the
/// carrier root and are entry names inside the carrier, so they always use <c>/</c>, never the
/// platform separator.
/// </remarks>
public sealed record SkillEntry
{
    /// <param name="name">The skill's directory name, unique within a catalogue.</param>
    /// <param name="description">The one-paragraph trigger description, copied into the managed block as-is.</param>
    /// <param name="agentsPath">Carrier-relative path of the open-standard flavour.</param>
    /// <param name="claudePath">Carrier-relative path of the vendor flavour.</param>
    public SkillEntry(string name, string description, string agentsPath, string claudePath)
    {
        Name = RequireText(name, "name");
        Description = RequireText(description, "description");
        AgentsPath = RequireText(agentsPath, "agents path");
        ClaudePath = RequireText(claudePath, "claude path");
    }

    /// <summary>The skill's directory name, unique within a catalogue.</summary>
    public string Name { get; }

    /// <summary>The one-paragraph trigger description, copied into the managed block as-is.</summary>
    public string Description { get; }

    /// <summary>Carrier-relative path of the open-standard flavour.</summary>
    public string AgentsPath { get; }

    /// <summary>Carrier-relative path of the vendor flavour.</summary>
    public string ClaudePath { get; }

    /// <summary>The carrier-relative path of one flavour's rendered page.</summary>
    public string PathFor(SkillFlavour flavour) => flavour switch
    {
        SkillFlavour.Agents => AgentsPath,
        SkillFlavour.Claude => ClaudePath,
        _ => throw new ArgumentException("a flavour must be given to pick a skill's path"),
    };

    private static string RequireText(string value, string what)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("a catalogue skill's " + what + " must not be blank");
        }

        return value;
    }
}
