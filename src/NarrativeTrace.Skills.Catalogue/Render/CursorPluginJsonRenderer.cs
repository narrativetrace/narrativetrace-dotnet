// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders the Cursor plugin manifest pair — <c>.cursor-plugin/plugin.json</c> and
/// <c>.cursor-plugin/marketplace.json</c> — from the SAME <see cref="MarketplaceListing"/> that
/// renders <c>.claude-plugin/marketplace.json</c>.
/// </summary>
/// <remarks>
/// <para>INTENT: one catalogue, one listing, two vendors' files. The plugin is the repository root and
/// carries the skills as the open-standard <c>.agents/skills</c> directory Cursor reads natively plus
/// the one rule file that points at them — no commands, hooks, agents or MCP servers (Phase 8 D3).
/// Committed build output like every render target; <c>skills lint</c> pins both files against a
/// fresh render.</para>
/// <para>Field names follow Cursor's plugin reference (verified 2026-10-10): <c>name</c> is required,
/// <c>author</c> takes <c>name</c>/<c>email</c> only, a <c>skills</c> or <c>rules</c> path replaces
/// folder discovery, and a marketplace entry's <c>source</c> is the plugin directory relative to the
/// repository root — <c>.</c> for a repository that is itself the one plugin. No <c>version</c> key,
/// for the reason <see cref="MarketplaceListing"/> states.</para>
/// </remarks>
public static class CursorPluginJsonRenderer
{
    /// <summary>The skills directory the plugin points at: the open-standard flavour Cursor reads natively.</summary>
    internal const string SkillsPath = "./.agents/skills/";

    /// <summary>The one rule the plugin carries, the same file <c>init</c> writes into a Cursor project.</summary>
    internal const string RulePath = "./.cursor/rules/narrativetrace.mdc";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Renders <c>.cursor-plugin/plugin.json</c> for <paramref name="listing"/>.</summary>
    public static string RenderPlugin(MarketplaceListing listing) => Serialize(new PluginDocument(
        listing.Name,
        listing.PluginDescription,
        new AuthorDocument(listing.Owner.Name),
        listing.License,
        listing.Homepage,
        listing.Keywords,
        SkillsPath,
        RulePath));

    /// <summary>Renders <c>.cursor-plugin/marketplace.json</c> for <paramref name="listing"/>.</summary>
    public static string RenderMarketplace(MarketplaceListing listing) => Serialize(new MarketplaceDocument(
        listing.Name,
        new AuthorDocument(listing.Owner.Name),
        new MetadataDocument(listing.Description),
        [new PluginEntryDocument(
            listing.Name, ".", listing.PluginDescription, listing.License, listing.Homepage, listing.Keywords)]));

    private static string Serialize(object document) =>
        JsonSerializer.Serialize(document, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";

    private sealed record AuthorDocument([property: JsonPropertyName("name")] string Name);

    private sealed record MetadataDocument([property: JsonPropertyName("description")] string Description);

    private sealed record PluginDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("author")] AuthorDocument Author,
        [property: JsonPropertyName("license")] string License,
        [property: JsonPropertyName("homepage")] string Homepage,
        [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords,
        [property: JsonPropertyName("skills")] string Skills,
        [property: JsonPropertyName("rules")] string Rules);

    private sealed record MarketplaceDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("owner")] AuthorDocument Owner,
        [property: JsonPropertyName("metadata")] MetadataDocument Metadata,
        [property: JsonPropertyName("plugins")] IReadOnlyList<PluginEntryDocument> Plugins);

    private sealed record PluginEntryDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("license")] string License,
        [property: JsonPropertyName("homepage")] string Homepage,
        [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords);
}
