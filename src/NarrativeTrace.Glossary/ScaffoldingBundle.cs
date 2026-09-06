// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Resources;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Per-locale renderer scaffolding shipped with the library.
/// </summary>
/// <remarks>
/// <para>
/// Translated trace views need locale variants of the renderer's fixed
/// vocabulary ("returns", "throws", fork/join labels, the glossary-gaps
/// heading) independent of any project glossary — a glossary translates a
/// project's own words, never the renderer's.
/// </para>
/// <para>
/// Backed by the <c>Scaffolding</c> resources: the neutral set is embedded in
/// this assembly and each locale ships as a satellite assembly, the .NET
/// counterpart of Java's <c>scaffolding*.properties</c> resource bundles. A
/// locale without a satellite falls back to the English neutral set — never
/// to the host's current UI culture, because a trace rendered "in Spanish"
/// must read the same on every machine that renders it.
/// </para>
/// </remarks>
public sealed class ScaffoldingBundle
{
    private static readonly ResourceManager Resources = new(
        "NarrativeTrace.Glossary.Scaffolding", typeof(ScaffoldingBundle).Assembly);

    private readonly CultureInfo culture;

    private ScaffoldingBundle(CultureInfo culture)
    {
        this.culture = culture;
    }

    /// <summary>
    /// Loads the scaffolding bundle for a locale tag (e.g. <c>es</c>,
    /// <c>zh-CN</c>). Unknown and unparseable locales resolve to the English
    /// neutral bundle.
    /// </summary>
    /// <param name="localeTag">BCP-47 locale tag; must not be blank.</param>
    /// <returns>The bundle for that locale.</returns>
    /// <exception cref="ArgumentException"><paramref name="localeTag"/> is null or blank.</exception>
    public static ScaffoldingBundle ForLocale(string localeTag)
    {
        if (string.IsNullOrWhiteSpace(localeTag))
        {
            throw new ArgumentException("localeTag must not be blank", nameof(localeTag));
        }

        return new ScaffoldingBundle(CultureOf(localeTag));
    }

    /// <summary>Label for a successful return ("returns").</summary>
    public string Returns => Label("returns");

    /// <summary>Label for a thrown exception ("throws").</summary>
    public string Throws => Label("throws");

    /// <summary>Label for a call that never completed ("incomplete").</summary>
    public string Incomplete => Label("incomplete");

    /// <summary>Label for a fork marker ("fork").</summary>
    public string Fork => Label("fork");

    /// <summary>Label for a join marker ("join").</summary>
    public string Join => Label("join");

    /// <summary>Label for a group of concurrent tasks ("tasks").</summary>
    public string Tasks => Label("tasks");

    /// <summary>Heading introducing fire-and-forget branches ("In the background:").</summary>
    public string Background => Label("background");

    /// <summary>Heading of the glossary-gaps footer ("Glossary gaps").</summary>
    public string GapsHeading => Label("gapsHeading");

    /// <summary>
    /// A tag the platform cannot parse names a locale that has no bundle,
    /// which is the same answer as a locale that simply ships none: the
    /// invariant culture, hence the English neutral set. Java's
    /// <c>Locale.forLanguageTag</c> is equally total.
    /// </summary>
    private static CultureInfo CultureOf(string localeTag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(localeTag);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    /// <summary>
    /// A key missing from every bundle would be a packaging fault, not a
    /// translation gap, so it fails loudly rather than rendering as an empty
    /// label inside somebody's trace.
    /// </summary>
    private string Label(string key)
    {
        return Resources.GetString(key, culture)
            ?? throw new MissingManifestResourceException(
                $"scaffolding resource '{key}' is missing from the neutral bundle");
    }
}
