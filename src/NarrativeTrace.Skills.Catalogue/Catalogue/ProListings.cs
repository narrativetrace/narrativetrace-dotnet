// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// Pro-tier mentions rendered into the catalogue. Every <see cref="ProListing.FeatureGuideStatusText"/>
/// here must appear verbatim in <c>documentation/feature-guide.md</c> — the Tier A lint that
/// keeps this listing from drifting from the source of truth. Never names the Pro repository or
/// prices; the mark is "Pro" plus status, nothing more (no-port-framing / tier-split-is-private
/// house rules).
/// </summary>
public static class ProListings
{
    /// <summary>Every Pro-tier listing this catalogue advertises.</summary>
    public static readonly IReadOnlyList<ProListing> All =
    [
        new ProListing(
            CanonicalName: "narrativetrace-pro-aggregate",
            Prompt: "summarize event-stream traffic across a run, not just one call tree",
            Delivers: "flow summaries and dependency-graph diagrams over aggregated event streams",
            Needs: "a Pro license",
            ComesFrom: null,
            Status: "shipped",
            FeatureGuideStatusText: "`EventAggregator` — Pro"),
        new ProListing(
            CanonicalName: "narrativetrace-mcp",
            Prompt: "let an agent query NarrativeTrace output through an MCP tool instead of grepping files",
            Delivers: "MCP tool handlers over rendered traces",
            Needs: "a Pro license",
            ComesFrom: null,
            Status: "planned",
            FeatureGuideStatusText: "Planned (Pro, gated)"),
    ];
}
