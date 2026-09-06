// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Rendering format for trace output, selected via configuration.
/// </summary>
/// <remarks>
/// Unlike <see cref="TracingLevel"/> these members carry no ordering — they name
/// alternative renderers, not increasing verbosity, so never compare them with
/// <c>&lt;</c> or <c>&gt;</c>. <see cref="Json"/> is the machine-readable
/// member: it is the only one whose output is a parsing contract rather than
/// prose for a human reader.
/// </remarks>
public enum OutputFormat
{
    /// <summary>Markdown with headings and nesting — the default, readable in a diff or a PR.</summary>
    Markdown,

    /// <summary>Plain indented text, for terminals and log sinks that render no markup.</summary>
    Text,

    /// <summary>Flowing narrative prose, optimized for reading rather than scanning.</summary>
    Prose,

    /// <summary>Structured JSON for machine consumers; the only format with a stable schema.</summary>
    Json,
}

/// <summary>
/// Lenient parsing for <see cref="OutputFormat"/>, for reading a format name out
/// of untrusted configuration without letting a typo crash capture.
/// </summary>
public static class OutputFormatExtensions
{
    /// <summary>
    /// Leniently parses a format name: null, blank, or unrecognized values
    /// degrade to <paramref name="fallback"/>.
    /// </summary>
    /// <param name="name">
    /// A format name, matched case-insensitively against the member names
    /// ("json", "Markdown", "PROSE"). Surrounding whitespace is trimmed.
    /// Numeric strings are rejected — "0" degrades to the fallback rather than
    /// resolving to <see cref="OutputFormat.Markdown"/>.
    /// </param>
    /// <param name="fallback">The format to use when <paramref name="name"/> does not resolve.</param>
    /// <returns>The parsed format, or <paramref name="fallback"/>; never throws.</returns>
    public static OutputFormat FromName(
        string? name, OutputFormat fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var trimmed = name!.Trim();
        return Enum.TryParse<OutputFormat>(
                trimmed, ignoreCase: true, out var format)
            && Enum.IsDefined(typeof(OutputFormat), format)
            && string.Equals(
                format.ToString(), trimmed,
                StringComparison.OrdinalIgnoreCase)
            ? format
            : fallback;
    }
}
