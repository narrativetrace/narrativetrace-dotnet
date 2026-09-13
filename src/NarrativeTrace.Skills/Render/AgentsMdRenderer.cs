// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders and splices the managed <c>AGENTS.md</c> section listing every shipped skill and
/// Pro-tier mention. The markers are new to this repository — no prior managed-section
/// convention existed here — chosen to match the product-scoped, non-repo-specific form other
/// ports already use.
/// </summary>
public static class AgentsMdRenderer
{
    /// <summary>Opens the managed section.</summary>
    public const string BeginMarker = "<!-- narrativetrace:skills:start -->";

    /// <summary>Closes the managed section.</summary>
    public const string EndMarker = "<!-- narrativetrace:skills:end -->";

    /// <summary>Renders the managed section's content (without the markers).</summary>
    public static string RenderSection(IReadOnlyList<Skill> skills, IReadOnlyList<ProListing> proListings)
    {
        var builder = new StringBuilder();
        builder.Append("## NarrativeTrace agent skills\n\n");
        AppendSkillLines(builder, skills);
        AppendProLines(builder, proListings);
        builder.Append("See documentation/agent-skills.md for the full doc index.\n");
        return builder.ToString();
    }

    private static void AppendSkillLines(StringBuilder builder, IReadOnlyList<Skill> skills)
    {
        foreach (var skill in skills)
        {
            builder.Append("- `").Append(skill.CanonicalName).Append("` — ")
                .Append(skill.Description).Append('\n');
        }
    }

    private static void AppendProLines(StringBuilder builder, IReadOnlyList<ProListing> proListings)
    {
        foreach (var listing in proListings)
        {
            builder.Append("- `").Append(listing.CanonicalName).Append("` (Pro, ")
                .Append(listing.Status).Append(") — ").Append(listing.Delivers).Append('\n');
        }
    }

    /// <summary>
    /// Replaces the delimited section in <paramref name="content"/> with <paramref name="section"/>,
    /// or appends a new one (with markers) when none is present yet.
    /// </summary>
    public static string Splice(string content, string section)
    {
        var begin = content.IndexOf(BeginMarker, StringComparison.Ordinal);
        var end = content.IndexOf(EndMarker, StringComparison.Ordinal);
        var block = $"{BeginMarker}\n{section}{EndMarker}";
        return begin >= 0 && end > begin
            ? content[..begin] + block + content[(end + EndMarker.Length)..]
            : content.TrimEnd('\n') + "\n\n" + block + "\n";
    }
}
