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
        NarrativeTraceExplainSkill.Definition,
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
            "install that traces nothing, add or verify a Clarity naming report, verify a change " +
            "by reading what the code actually did, debug a wrong result from its values, explain " +
            "what an application actually does from a run of it, and report a defect in " +
            "NarrativeTrace itself with your approval.",
        PluginSource: "./.claude",
        License: "Apache-2.0",
        Homepage: "https://narrativetrace.ai",
        Keywords: ["narrativetrace", "dotnet", "tracing", "observability", "agent-skills"]);

    /// <summary>
    /// How this repository presents <see cref="Marketplace"/> to the OpenAI plugin directory (shared
    /// by ChatGPT and Codex): the same listing plus the directory's own display fields. The capabilities
    /// name what the skills do — run a local build, read and write files — not a network or data-access
    /// claim; the default prompts are drawn from the skills' own trigger phrases in <see cref="Skills"/>,
    /// never invented ones. The brand colour is the website brand bundle's tile colour, the same
    /// one the two icon assets sit on.
    /// </summary>
    public static readonly OpenAiListing OpenAiDirectory = new(
        Marketplace,
        displayName: "NarrativeTrace for .NET",
        shortDescription: "NarrativeTrace skills for .NET",
        category: "Developer Tools",
        capabilities: ["Read", "Write"],
        defaultPrompts: ["add narrative tracing", "check my NarrativeTrace setup", "debug this with the trace"],
        brandColor: "#00845a",
        websiteUrl: Marketplace.Homepage);
}
