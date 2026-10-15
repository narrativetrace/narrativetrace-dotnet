// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;

namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The closed vocabulary of deliveries a Tier B case may declare — the step that puts this
/// repository's rendered skill pages in place before the agent starts: a REGISTRY tool, or our own
/// installer run by the harness so a case can ask what a session finds AFTER an install without the
/// install being the agent's work.
///
/// <para>A closed enum rather than a command string in <c>case.json</c>: a case file is data, and
/// data that can name any executable is a shell this harness does not have. Each constant stands
/// for the exact argv a documented registry line amounts to (see <see cref="RegistryPreSteps"/>),
/// so the case replays what a reader runs. The id a case carries
/// (<c>"registry": "npx-skills"</c>) names one of these; an unknown id is an error at the parse
/// site, never a silently skipped pre-step.</para>
/// </summary>
public enum RegistryPreStep
{
    /// <summary>The Claude Code plugin marketplace: added, then its one plugin installed at user scope.</summary>
    ClaudeMarketplace,

    /// <summary><c>npx skills add</c>: the open-standard pages installed into the project itself.</summary>
    NpxSkills,

    /// <summary>
    /// Our own installer — <c>init</c> from the CLI built out of this checkout — run by the harness in
    /// the scratch project. Nothing is staged: the carrier is the one bundled with the build, this
    /// checkout's own rendered pages, so a case measures the install under test and never a published
    /// package that predates it, nor the agent's own reading of the install prompt.
    /// </summary>
    NarrativetraceInit,
}

/// <summary>Helpers over <see cref="RegistryPreStep"/>: its case-file id, parsing, and the commands it runs.</summary>
public static class RegistryPreSteps
{
    /// <summary>What a case's <c>case.json</c> names this delivery by.</summary>
    public static string Id(this RegistryPreStep step) =>
        step switch
        {
            RegistryPreStep.ClaudeMarketplace => "claude-marketplace",
            RegistryPreStep.NpxSkills => "npx-skills",
            RegistryPreStep.NarrativetraceInit => "narrativetrace-init",
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "unknown registry"),
        };

    /// <summary>The delivery <paramref name="id"/> names, or <c>null</c> when nothing in the vocabulary matches it.</summary>
    public static RegistryPreStep? Parse(string? id) =>
        id switch
        {
            "claude-marketplace" => RegistryPreStep.ClaudeMarketplace,
            "npx-skills" => RegistryPreStep.NpxSkills,
            "narrativetrace-init" => RegistryPreStep.NarrativetraceInit,
            _ => null,
        };

    /// <summary>
    /// Everything this delivery runs before the agent starts: the staging of
    /// <see cref="StagedSnapshot.RegistrySurface"/> out of <c>HEAD</c> of the repository at
    /// <paramref name="repoRoot"/>, then the registry tool's own documented commands against the
    /// staged tree.
    ///
    /// <para>The staged tree stands in for the GitHub shorthand a reader types, because the public
    /// repository's own <c>main</c> carries the pages of the release BEFORE the one these cases
    /// describe — the same reason the design sequences everything a user runs against GitHub after
    /// the carrier release.</para>
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Commands(
        this RegistryPreStep step, string repoRoot, string stagedSnapshot)
    {
        if (step == RegistryPreStep.NarrativetraceInit)
        {
            return [["dotnet", CliDll.Require(repoRoot), "init"]];
        }

        var all = new List<IReadOnlyList<string>>(StagedSnapshot.StagingCommands(repoRoot, stagedSnapshot));
        all.AddRange(RegistryCommands(step, stagedSnapshot));
        return all;
    }

    /// <summary>The tool's own lines — what the documentation tells a reader to run, and nothing beside.</summary>
    private static IReadOnlyList<IReadOnlyList<string>> RegistryCommands(RegistryPreStep step, string staged)
    {
        var plugin = SkillCatalogue.Marketplace.Name;
        var installId = plugin + "@" + plugin;
        return step switch
        {
            RegistryPreStep.ClaudeMarketplace =>
            [
                ["claude", "plugin", "marketplace", "add", staged],
                ["claude", "plugin", "install", installId],
                ["claude", "plugin", "details", installId],
            ],
            RegistryPreStep.NpxSkills =>
            [
                ["npx", "--yes", "skills", "add", staged, "-y"],
            ],
            RegistryPreStep.NarrativetraceInit => throw new InvalidOperationException("the installer stages nothing"),
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "unknown registry"),
        };
    }
}
