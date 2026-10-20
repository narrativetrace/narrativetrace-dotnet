// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders <c>plugin.json</c> for the OpenAI plugin directory (shared by ChatGPT and Codex): the
/// versionless text that is committed build output (<see cref="Render(OpenAiListing)"/>, pinned by
/// the <c>skills lint</c> drift check), and the packaged text a release ZIP carries
/// (<see cref="Render(OpenAiListing, string)"/>). Both go through one builder, so the two differ only
/// by the <c>"version"</c> line.
/// </summary>
/// <remarks>
/// Field names and the manifest shape follow the vendor's own plugin schema
/// (<c>https://agent-plugins.org/schemas/1.0.0/plugin.schema.json</c>) and its
/// <c>extensions.com.openai.interface</c> listing fields. It skips every key this repository does not
/// use: no <c>hooks</c> or <c>apps</c> (the plugin is skills-only), no <c>onboardingSkill</c>, no
/// <c>requires_local_executor</c> (the desktop-only declaration waits on a cloud trial), and no
/// <c>privacyPolicyURL</c>/<c>termsOfServiceURL</c> (narrativetrace.ai carries neither page yet).
/// </remarks>
public static class OpenAiPluginJsonRenderer
{
    private const string SchemaUrl = "https://agent-plugins.org/schemas/1.0.0/plugin.schema.json";

    /// <summary>The icon paths, relative to the plugin root — the ZIP layout and the manifest agree on them.</summary>
    internal const string LogoPath = "./assets/logo.png";

    /// <summary>The composer icon's path, relative to the plugin root.</summary>
    internal const string ComposerIconPath = "./assets/composer-icon.png";

    /// <summary>
    /// Appended to the plugin description for the directory's <c>longDescription</c>: states the
    /// environment a skills-only plugin needs, and bounds the claim to what a run actually showed — the
    /// same rule the verify and explain skills hold their own reports to.
    /// </summary>
    private const string EnvironmentSentence =
        "It reads what one run actually did, and needs the .NET SDK and network access for the first package restore.";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>The committed, versionless text — the installed version IS the release that shipped it.</summary>
    /// <param name="listing">The directory listing to render.</param>
    public static string Render(OpenAiListing listing) => Build(listing, version: null);

    /// <summary>The packaged text a release ZIP carries, with the release version inserted.</summary>
    /// <param name="listing">The directory listing to render.</param>
    /// <param name="version">The release version; blank is refused.</param>
    public static string Render(OpenAiListing listing, string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("version must not be blank", nameof(version));
        }

        return Build(listing, version);
    }

    private static string Build(OpenAiListing listing, string? version)
    {
        ArgumentNullException.ThrowIfNull(listing);
        var json = JsonSerializer.Serialize(ToDocument(listing, version), Options).Replace("\r\n", "\n", StringComparison.Ordinal);
        return json + "\n";
    }

    private static ManifestDocument ToDocument(OpenAiListing listing, string? version) => new(
        SchemaUrl,
        listing.Base.Name,
        version,
        listing.Base.PluginDescription,
        new AuthorDocument(listing.Base.Owner.Name),
        listing.Base.Homepage,
        listing.Base.License,
        listing.Base.Keywords,
        new ExtensionsDocument(new OpenAiDocument(ToInterface(listing))));

    private static InterfaceDocument ToInterface(OpenAiListing listing) => new(
        listing.DisplayName,
        listing.ShortDescription,
        $"{listing.Base.PluginDescription} {EnvironmentSentence}",
        listing.Base.Owner.Name,
        listing.Category,
        listing.Capabilities,
        listing.DefaultPrompts,
        listing.BrandColor,
        listing.WebsiteUrl,
        LogoPath,
        ComposerIconPath);

    private sealed record ManifestDocument(
        [property: JsonPropertyName("$schema")] string Schema,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("version"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Version,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("author")] AuthorDocument Author,
        [property: JsonPropertyName("homepage")] string Homepage,
        [property: JsonPropertyName("license")] string License,
        [property: JsonPropertyName("keywords")] IReadOnlyList<string> Keywords,
        [property: JsonPropertyName("extensions")] ExtensionsDocument Extensions);

    private sealed record AuthorDocument([property: JsonPropertyName("name")] string Name);

    private sealed record ExtensionsDocument([property: JsonPropertyName("com.openai")] OpenAiDocument ComOpenAi);

    private sealed record OpenAiDocument([property: JsonPropertyName("interface")] InterfaceDocument Interface);

    private sealed record InterfaceDocument(
        [property: JsonPropertyName("displayName")] string DisplayName,
        [property: JsonPropertyName("shortDescription")] string ShortDescription,
        [property: JsonPropertyName("longDescription")] string LongDescription,
        [property: JsonPropertyName("developerName")] string DeveloperName,
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("capabilities")] IReadOnlyList<string> Capabilities,
        [property: JsonPropertyName("defaultPrompt")] IReadOnlyList<string> DefaultPrompt,
        [property: JsonPropertyName("brandColor")] string BrandColor,
        [property: JsonPropertyName("websiteURL")] string WebsiteUrl,
        [property: JsonPropertyName("logo")] string Logo,
        [property: JsonPropertyName("composerIcon")] string ComposerIcon);
}
