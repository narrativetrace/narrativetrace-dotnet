// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills;

/// <summary>
/// This runtime's registry identity: everything a Claude Code plugin-marketplace listing needs
/// that is not a skill page. One listing for this repository — the marketplace and its single
/// plugin share the name, because a marketplace name is unique per user and every runtime in the
/// family ships skills under the same canonical names.
/// </summary>
/// <remarks>
/// No version, deliberately: with a relative-path plugin source the installed version IS the
/// repository's commit, so a version literal here would drift on every release and say nothing
/// the commit does not.
/// </remarks>
/// <param name="Name">
/// The marketplace identifier AND the plugin's — what a user types after <c>@</c> when
/// installing, and (with no <c>plugin.json</c> in the tree) the plugin's manifest name too.
/// </param>
/// <param name="Owner">Who maintains it, shown in the listing.</param>
/// <param name="Description">The marketplace's own line, shown when browsing it.</param>
/// <param name="PluginDescription">The plugin entry's line, shown in the plugin list and details.</param>
/// <param name="PluginSource">
/// The plugin's directory, relative to the marketplace root (the repository root, the directory
/// holding <c>.claude-plugin/</c>) — the rendered Claude-flavour pages' own root, so the plugin
/// carries the skills and nothing else of the repository.
/// </param>
/// <param name="License">SPDX identifier of the pages the plugin ships.</param>
/// <param name="Homepage">Where a reader goes to learn what this is.</param>
/// <param name="Keywords">Free-form search terms for the listing.</param>
public sealed record MarketplaceListing(
    string Name,
    MarketplaceOwner Owner,
    string Description,
    string PluginDescription,
    string PluginSource,
    string License,
    string Homepage,
    IReadOnlyList<string> Keywords);

/// <summary>The maintainer behind a <see cref="MarketplaceListing"/>: a name, and where to read about them.</summary>
public sealed record MarketplaceOwner(string Name, string Url);
