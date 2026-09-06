// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NarrativeTrace.Build;

/// <summary>Translation status a language declares in the manifest — see <see cref="I18nManifestSupport"/>.</summary>
internal enum I18nStatus
{
    Complete,
    InProgress,
}

internal static class I18nStatusExtensions
{
    /// <summary>The manifest's own spelling of the status, for problem messages (<c>"complete"</c> / <c>"in-progress"</c>).</summary>
    public static string Label(this I18nStatus status) =>
        status == I18nStatus.Complete ? "complete" : "in-progress";
}

/// <summary>
/// One language's coordinates in the manifest — directory, sibling index,
/// root README, declared status.
/// </summary>
/// <remarks>
/// <see cref="Directory"/> is descriptive metadata (the language's primary
/// translated-document home, e.g. <c>documentation/guides/es</c>) kept for
/// schema parity with the Java golden source; unlike Java's flat layout, no
/// check in this port resolves a document's translated path against it —
/// <see cref="I18nDocument.Translations"/> already carries a full path
/// relative to <c>documentation/</c>, because this port's documents nest
/// under two different directories (funnel pages and guides). See
/// the i18n terminology conventions, "The i18n manifest" section.
/// </remarks>
internal sealed record I18nLanguage(
    string Code,
    string DisplayName,
    string Directory,
    string Index,
    string RootReadme,
    I18nStatus Status);

/// <summary>
/// One user document's English source and the native filename each language
/// has translated it to, if any. Translation values are paths relative to
/// <c>documentation/</c> — not bare filenames — because this port's user docs
/// nest two levels deep (<c>documentation/*.md</c> funnel pages alongside
/// <c>documentation/guides/*.md</c> task guides), unlike the Java golden
/// source's single flat <c>documentation/&lt;lang&gt;/</c> directory.
/// </summary>
internal sealed record I18nDocument(string Source, IReadOnlyDictionary<string, string> Translations);

/// <summary>The parsed manifest: every declared language plus the document set translation coverage is measured against.</summary>
internal sealed record I18nManifest(
    string SourceLanguage,
    IReadOnlyList<I18nLanguage> Languages,
    IReadOnlyList<I18nDocument> Documents)
{
    /// <summary>The declared language with this code, or <c>null</c> when the manifest does not declare it.</summary>
    public I18nLanguage? Language(string code) => Languages.FirstOrDefault(l => l.Code == code);
}

/// <summary>
/// Loads <c>documentation/i18n/manifest.json</c> — the machine-readable
/// declaration of which languages this repository translates into, where
/// each language's index and root README live, and which English user
/// documents are in scope for translation. Every other i18n check
/// (completeness, index/menu integrity, the review dashboard) reads this
/// manifest rather than re-deriving the same facts.
/// </summary>
/// <remarks>
/// The manifest lives under <c>documentation/</c>, so it survives the public
/// snapshot, unlike the i18n terminology conventions (the human-facing
/// translation guide, an internal build/tooling document).
/// <see cref="TranslationCheckSupport"/>
/// only treats a BCP-47-shaped directory name as a language directory, so
/// <c>documentation/i18n/</c> is never mistaken for one.
/// </remarks>
internal static class I18nManifestSupport
{
    /// <summary>Where the manifest lives, relative to the repository root.</summary>
    public const string ManifestRelativePath = "documentation/i18n/manifest.json";

    /// <summary>
    /// Parses the manifest at <paramref name="repoRoot"/>/<see cref="ManifestRelativePath"/>,
    /// or <c>null</c> when the file is absent — callers degrade gracefully
    /// rather than failing the build over a manifest that has not been
    /// introduced yet.
    /// </summary>
    /// <remarks>
    /// A present-but-malformed manifest is a configuration error, not a
    /// graceful-degradation case: this throws (fail fast and loud), it does
    /// not return <c>null</c>.
    /// </remarks>
    public static I18nManifest? LoadOrNull(string repoRoot)
    {
        var file = Path.Combine(repoRoot, ManifestRelativePath);
        if (!File.Exists(file))
            return null;

        using var document = JsonDocument.Parse(File.ReadAllText(file));
        var root = document.RootElement;
        return new I18nManifest(
            SourceLanguage: root.TryGetProperty("sourceLanguage", out var sourceLanguage)
                ? sourceLanguage.GetString() ?? "en"
                : "en",
            Languages: root.GetProperty("languages").EnumerateArray().Select(ParseLanguage).ToList(),
            Documents: root.GetProperty("documents").EnumerateArray().Select(ParseDocument).ToList());
    }

    private static I18nLanguage ParseLanguage(JsonElement raw) =>
        new(
            Code: raw.GetProperty("code").GetString()!,
            DisplayName: raw.GetProperty("displayName").GetString()!,
            Directory: raw.GetProperty("directory").GetString()!,
            Index: raw.GetProperty("index").GetString()!,
            RootReadme: raw.GetProperty("rootReadme").GetString()!,
            Status: ParseStatus(raw.GetProperty("status").GetString()!));

    private static I18nStatus ParseStatus(string raw) => raw switch
    {
        "complete" => I18nStatus.Complete,
        "in-progress" => I18nStatus.InProgress,
        _ => throw new InvalidOperationException(
            $"{ManifestRelativePath}: unknown language status '{raw}' (expected 'complete' or 'in-progress')"),
    };

    private static I18nDocument ParseDocument(JsonElement raw)
    {
        var translations = raw.TryGetProperty("translations", out var map)
            ? map.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString() ?? string.Empty)
            : new Dictionary<string, string>();
        return new I18nDocument(raw.GetProperty("source").GetString()!, translations);
    }
}
