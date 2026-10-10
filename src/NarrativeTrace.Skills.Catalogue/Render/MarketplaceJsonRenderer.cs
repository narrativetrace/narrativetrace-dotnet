// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders <c>.claude-plugin/marketplace.json</c>: the file that makes this repository a plugin
/// marketplace carrying one plugin — the rendered Claude-flavour skill pages, rooted at the
/// <c>.claude/</c> directory they already sit in. Committed build output, like every other render
/// target: the typed <see cref="MarketplaceListing"/> is the only place this content is written.
/// </summary>
public static class MarketplaceJsonRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Renders <c>marketplace.json</c> for <paramref name="listing"/>.</summary>
    public static string Render(MarketplaceListing listing)
    {
        var document = new MarketplaceDocument(
            listing.Name,
            new OwnerDocument(listing.Owner.Name, listing.Owner.Url),
            listing.Description,
            [ToPluginEntry(listing)]);
        var json = JsonSerializer.Serialize(document, Options).Replace("\r\n", "\n", StringComparison.Ordinal);
        return json + "\n";
    }

    private static PluginEntryDocument ToPluginEntry(MarketplaceListing listing) => new(
        listing.Name,
        listing.PluginSource,
        listing.PluginDescription,
        listing.License,
        listing.Homepage,
        listing.Keywords);

    private sealed record MarketplaceDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("owner")] OwnerDocument Owner,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("plugins")] IReadOnlyList<PluginEntryDocument> Plugins);

    private sealed record OwnerDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("url")] string Url);

    private sealed record PluginEntryDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("license")] string License,
        [property: JsonPropertyName("homepage")] string Homepage,
        [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords);
}
