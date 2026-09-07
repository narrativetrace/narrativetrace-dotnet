// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Resolved ambient configuration for a trace run.
/// </summary>
/// <param name="Level">The capture level in force.</param>
/// <param name="Output">Whether per-test trace artifacts are written at all.</param>
/// <param name="OutputDir">The artifact root, or null for the caller's default.</param>
/// <param name="Format">The primary artifact format.</param>
/// <param name="CanonicalJson">
/// Whether to additionally write the per-test canonical entry array. Off by
/// default: it is a machine artifact for schema consumers, not something a
/// human reads next to the trace.
/// </param>
/// <param name="StructuralJson">
/// Whether to additionally write the value-free entry array (ADR-002 Level 1).
/// Off by default for the same reason.
/// </param>
/// <param name="Narration">
/// Whether narration is enabled at all. <b>On unless switched off</b> — the
/// veto exists so a deployment can silence the narrative bridges without
/// unwiring them.
/// </param>
public sealed record ResolvedConfig(
    TracingLevel Level,
    bool Output,
    string? OutputDir,
    OutputFormat Format,
    bool CanonicalJson = false,
    bool StructuralJson = false,
    bool Narration = true);

/// <summary>
/// Resolves configuration from <c>NARRATIVETRACE_*</c> environment
/// variables — the .NET-native override channel, keeping Core
/// zero-dependency. Invalid values degrade to defaults rather than
/// throwing, so bad config never crashes capture.
/// </summary>
public static class ConfigResolver
{
    /// <summary>
    /// Environment variable naming the <see cref="TracingLevel"/>. Parsed
    /// leniently — an unrecognized name falls back to the caller's default.
    /// </summary>
    public const string LevelKey = "NARRATIVETRACE_LEVEL";

    /// <summary>
    /// Environment variable enabling trace output. Only <c>"true"</c>
    /// (case-insensitive) and <c>"1"</c> enable it; every other value,
    /// <c>"yes"</c> and <c>"on"</c> included, reads as disabled.
    /// </summary>
    public const string OutputKey = "NARRATIVETRACE_OUTPUT";

    /// <summary>
    /// Environment variable naming the directory for trace artifacts. A blank
    /// value is treated as unset rather than as the current directory.
    /// </summary>
    public const string OutputDirKey = "NARRATIVETRACE_OUTPUT_DIR";

    /// <summary>
    /// Environment variable naming the <see cref="OutputFormat"/>. Unrecognized
    /// names fall back to <see cref="OutputFormat.Markdown"/>.
    /// </summary>
    public const string FormatKey = "NARRATIVETRACE_FORMAT";

    /// <summary>
    /// Environment variable enabling the per-test canonical entry array
    /// (<c>&lt;test&gt;.canonical.json</c>). Parsed like
    /// <see cref="OutputKey"/>. The Java runtime spells the same switch
    /// <c>narrativetrace.canonicalJson</c>.
    /// </summary>
    public const string CanonicalJsonKey = "NARRATIVETRACE_CANONICAL_JSON";

    /// <summary>
    /// Environment variable enabling the per-test value-free entry array
    /// (<c>&lt;test&gt;.structural.json</c>). Parsed like
    /// <see cref="OutputKey"/>. The Java runtime spells the same switch
    /// <c>narrativetrace.structuralJson</c>.
    /// </summary>
    public const string StructuralJsonKey = "NARRATIVETRACE_STRUCTURAL_JSON";

    /// <summary>
    /// Environment variable that switches narration off. The only value that
    /// disables it is <c>off</c> (case-insensitive); unset, blank and anything
    /// else leave narration on, mirroring Java's
    /// <c>narrativetrace.narration=off</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately not a boolean like <see cref="OutputKey"/>: this is a veto,
    /// so the safe reading of a typo is "still narrating", not "silently
    /// silenced".
    /// </remarks>
    public const string NarrationKey = "NARRATIVETRACE_NARRATION";

    /// <summary>
    /// Resolves configuration from process environment variables.
    /// </summary>
    public static ResolvedConfig Resolve(
        TracingLevel defaultLevel = TracingLevel.Detail)
    {
        return Resolve(Environment.GetEnvironmentVariable, defaultLevel);
    }

    /// <summary>
    /// Resolves configuration from an injected variable reader — the seam
    /// integrations and tests use to supply configuration without touching
    /// the process environment.
    /// </summary>
    public static ResolvedConfig Resolve(
        Func<string, string?> read,
        TracingLevel defaultLevel = TracingLevel.Detail)
    {
        return new ResolvedConfig(
            TracingLevelExtensions.FromName(
                read(LevelKey), defaultLevel),
            ParseBool(read(OutputKey)),
            NullIfBlank(read(OutputDirKey)),
            OutputFormatExtensions.FromName(
                read(FormatKey), OutputFormat.Markdown),
            ParseBool(read(CanonicalJsonKey)),
            ParseBool(read(StructuralJsonKey)),
            !IsOff(read(NarrationKey)));
    }

    private static bool ParseBool(string? value)
    {
        return value is not null
            && (string.Equals(
                    value.Trim(), "true",
                    StringComparison.OrdinalIgnoreCase)
                || value.Trim() == "1");
    }

    private static bool IsOff(string? value)
    {
        return value is not null
            && string.Equals(
                value.Trim(), "off", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NullIfBlank(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value!.Trim();
    }
}
