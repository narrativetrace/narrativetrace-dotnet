// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>The assembled catalogue: every shipped skill and Pro-tier listing.</summary>
public static class SkillCatalogue
{
    /// <summary>Every skill this repository ships, in a stable order.</summary>
    public static readonly IReadOnlyList<Skill> Skills =
    [
        NarrativeTraceDoctorSkill.Definition,
        AddNarrativeTracingSkill.Definition,
        NarrativeTraceFeedbackSkill.Definition,
        AddNarrativeTraceClaritySkill.Definition,
        NarrativeTraceVerifySkill.Definition,
        NarrativeTraceDebugSkill.Definition,
    ];

    /// <summary>Every Pro-tier listing the catalogue advertises.</summary>
    public static readonly IReadOnlyList<ProListing> Pro = ProListings.All;

    /// <summary>
    /// How this repository presents <see cref="Skills"/> to a Claude Code plugin marketplace.
    /// The plugin's source is the rendered Claude-flavour pages' own directory
    /// (<c>./.claude</c>), so the plugin carries the skill pages and nothing else of this
    /// repository.
    /// </summary>
    public static readonly MarketplaceListing Marketplace = new(
        Name: "narrativetrace-dotnet",
        Owner: new MarketplaceOwner("NarrativeTrace", "https://narrativetrace.ai"),
        Description: "The NarrativeTrace agent skills for .NET.",
        PluginDescription:
            "Install NarrativeTrace in a .NET project and reach a first trace, diagnose an " +
            "install that traces nothing, score the names a trace renders, verify a change " +
            "by reading what the code actually did, and debug a wrong result from its values.",
        PluginSource: "./.claude",
        License: "Apache-2.0",
        Homepage: "https://narrativetrace.ai",
        Keywords: ["narrativetrace", "dotnet", "tracing", "observability", "agent-skills"]);
}
