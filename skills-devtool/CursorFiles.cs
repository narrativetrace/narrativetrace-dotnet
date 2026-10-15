// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Render;
using NarrativeTrace.Tooling.Init;
using SkillCatalogue = NarrativeTrace.Skills.Catalogue.SkillCatalogue;
using NarrativeTrace.Tooling.Platforms;

namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// The repository files that make this repository a Cursor plugin (Phase 8 D3): the two manifests,
/// rendered from the catalogue's one listing, and the one rule, rendered from the platform table.
/// </summary>
/// <remarks>
/// INTENT: <c>skills render</c> writes exactly these and <c>skills lint</c> compares exactly these, so
/// the list is typed once. The rule ships UNSTAMPED — a plugin copy names no release — which is also
/// what lets <c>init</c> adopt it byte for byte in a project that installed the plugin first.
/// </remarks>
internal static class CursorFiles
{
    /// <summary>Every file, repository-relative with <c>/</c> separators, with its freshly rendered content.</summary>
    internal static IReadOnlyList<(string Path, string Content)> All() =>
    [
        (".cursor-plugin/plugin.json", CursorPluginJsonRenderer.RenderPlugin(SkillCatalogue.Marketplace)),
        (".cursor-plugin/marketplace.json", CursorPluginJsonRenderer.RenderMarketplace(SkillCatalogue.Marketplace)),
        (".cursor/rules/narrativetrace.mdc", CursorRule.Render(RuleText(), "")),
    ];

    private static string RuleText() => ((Pointer.Rule)PlatformTable.Row("cursor")!.Pointer).Text;
}
