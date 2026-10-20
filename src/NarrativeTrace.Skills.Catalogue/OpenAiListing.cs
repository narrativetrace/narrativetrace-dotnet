// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills;

/// <summary>
/// The OpenAI plugin directory's own listing fields (the directory is shared by ChatGPT and Codex),
/// beside the vendor-neutral <see cref="MarketplaceListing"/> every registry shares.
/// <see cref="DisplayName"/> and <see cref="ShortDescription"/> carry the directory's 30-character
/// ceiling, enforced on construction and on <c>with</c>; everything else the directory's manifest
/// needs — name, description, author, homepage, license, keywords — comes from <see cref="Base"/>, so
/// this record never repeats a fact the vendor-neutral listing already states.
/// </summary>
/// <remarks>
/// No privacy-policy or terms-of-service field: both URLs are optional for a skills-only submission,
/// narrativetrace.ai carries neither page yet, and a field the renderer would never read is dead
/// weight. Add both back together with the rendering they would drive once the two pages exist.
/// </remarks>
public sealed record OpenAiListing
{
    /// <summary>The directory's own ceiling on <see cref="DisplayName"/> and <see cref="ShortDescription"/>.</summary>
    public const int MaxListingFieldLength = 30;

    private readonly string _displayName = string.Empty;
    private readonly string _shortDescription = string.Empty;
    private readonly IReadOnlyList<string> _capabilities = [];
    private readonly IReadOnlyList<string> _defaultPrompts = [];

    /// <summary>Creates a directory listing; throws <see cref="ArgumentException"/> when a 30-character field is longer.</summary>
    /// <param name="baseListing">The vendor-neutral listing this directory entry is drawn from.</param>
    /// <param name="displayName">Shown in the directory, at most 30 characters.</param>
    /// <param name="shortDescription">The one-line pitch, at most 30 characters.</param>
    /// <param name="category">The directory's own grouping for the listing (allowed values unverified).</param>
    /// <param name="capabilities">What the plugin declares it can do (allowed values unverified).</param>
    /// <param name="defaultPrompts">Example prompts shown to a user before they have typed anything.</param>
    /// <param name="brandColor">The listing's accent colour, a hex string.</param>
    /// <param name="websiteUrl">Where a reader goes to learn more.</param>
    public OpenAiListing(
        MarketplaceListing baseListing,
        string displayName,
        string shortDescription,
        string category,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<string> defaultPrompts,
        string brandColor,
        string websiteUrl)
    {
        Base = baseListing ?? throw new ArgumentNullException(nameof(baseListing));
        DisplayName = displayName;
        ShortDescription = shortDescription;
        Category = category;
        Capabilities = capabilities;
        DefaultPrompts = defaultPrompts;
        BrandColor = brandColor;
        WebsiteUrl = websiteUrl;
    }

    /// <summary>The vendor-neutral listing this directory entry is drawn from.</summary>
    public MarketplaceListing Base { get; init; }

    /// <summary>Shown in the directory, at most <see cref="MaxListingFieldLength"/> characters.</summary>
    public string DisplayName
    {
        get => _displayName;
        init => _displayName = RequireAtMost(value, nameof(DisplayName));
    }

    /// <summary>The one-line pitch, at most <see cref="MaxListingFieldLength"/> characters.</summary>
    public string ShortDescription
    {
        get => _shortDescription;
        init => _shortDescription = RequireAtMost(value, nameof(ShortDescription));
    }

    /// <summary>The directory's own grouping for the listing (allowed values unverified).</summary>
    public string Category { get; init; }

    /// <summary>What the plugin declares it can do (allowed values unverified); copied on assignment.</summary>
    public IReadOnlyList<string> Capabilities
    {
        get => _capabilities;
        init => _capabilities = [.. value];
    }

    /// <summary>Example prompts shown to a user before they have typed anything; copied on assignment.</summary>
    public IReadOnlyList<string> DefaultPrompts
    {
        get => _defaultPrompts;
        init => _defaultPrompts = [.. value];
    }

    /// <summary>The listing's accent colour, a hex string.</summary>
    public string BrandColor { get; init; }

    /// <summary>Where a reader goes to learn more.</summary>
    public string WebsiteUrl { get; init; }

    private static string RequireAtMost(string value, string field) =>
        value is null
            ? throw new ArgumentNullException(field)
            : value.Length <= MaxListingFieldLength
                ? value
                : throw new ArgumentException(
                    $"{field} must be at most {MaxListingFieldLength} characters, was {value.Length}", field);
}
