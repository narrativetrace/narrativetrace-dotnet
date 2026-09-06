// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The output format for a per-test trace artifact, mirroring the Java
/// reference's <c>extensionForFormat</c> vocabulary.
/// </summary>
/// <remarks>
/// Distinct from <see cref="OutputFormat"/> on purpose: that one selects how a
/// trace is rendered in-process, this one selects what a test writes to disk and
/// therefore which file extension the artifact gets. The diagram members
/// (<see cref="Mermaid"/>, <see cref="PlantUml"/>) exist only here.
/// </remarks>
public enum TraceArtifactFormat
{
    /// <summary>Markdown artifact (<c>.md</c>) — the default.</summary>
    Markdown,

    /// <summary>Plain-text artifact (<c>.txt</c>).</summary>
    Text,

    /// <summary>Mermaid sequence diagram (<c>.mmd</c>), renderable inline by GitLab and GitHub.</summary>
    Mermaid,

    /// <summary>PlantUML sequence diagram (<c>.puml</c>), for toolchains that already run PlantUML.</summary>
    PlantUml,
}

/// <summary>
/// Lenient parsing for <see cref="TraceArtifactFormat"/>, matching the Java
/// reference's <c>narrativetrace.format</c> vocabulary. Unknown or blank names
/// degrade to the supplied fallback rather than throwing.
/// </summary>
public static class TraceArtifactFormatExtensions
{
    /// <summary>Parses a <c>narrativetrace.format</c> configuration value.</summary>
    /// <param name="name">
    /// A format name matched case-insensitively after trimming, against the
    /// lowercase Java vocabulary: <c>markdown</c>, <c>text</c>, <c>mermaid</c>,
    /// <c>plantuml</c>. Matching is exact apart from case and surrounding
    /// whitespace: <c>"PlantUML "</c> resolves, but any separator inside the
    /// word (<c>"plant-uml"</c>, <c>"plant_uml"</c>) degrades to the fallback.
    /// </param>
    /// <param name="fallback">The format to use when <paramref name="name"/> does not resolve.</param>
    /// <returns>The parsed format, or <paramref name="fallback"/>; never throws.</returns>
    public static TraceArtifactFormat FromName(
        string? name, TraceArtifactFormat fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        return name!.Trim().ToLowerInvariant() switch
        {
            "text" => TraceArtifactFormat.Text,
            "mermaid" => TraceArtifactFormat.Mermaid,
            "plantuml" => TraceArtifactFormat.PlantUml,
            "markdown" => TraceArtifactFormat.Markdown,
            _ => fallback,
        };
    }
}
