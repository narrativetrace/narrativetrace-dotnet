// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// How much narrative a trace captures, from fully off to per-parameter detail.
/// </summary>
/// <remarks>
/// The members are <b>ordered by increasing verbosity</b> and every gate in the
/// library is a <c>&gt;=</c> comparison against them, so the numeric order is
/// part of the contract: inserting a member anywhere but the end changes which
/// events existing configuration captures. Compare with
/// <see cref="TracingLevelExtensions.IsEnabled"/> rather than <c>==</c> — an
/// equality check silently drops everything captured at a higher level.
/// </remarks>
public enum TracingLevel
{
    /// <summary>Capture nothing; every gate is closed.</summary>
    Off,

    /// <summary>Capture only the paths that threw. The lowest active level.</summary>
    Errors,

    /// <summary>Capture entry and exit per traced method, without the narrative body.</summary>
    Summary,

    /// <summary>Capture the full narrative — the default for readable traces.</summary>
    Narrative,

    /// <summary>Capture the narrative plus parameter values. The most verbose level.</summary>
    Detail,
}

/// <summary>
/// Ordering-aware helpers for <see cref="TracingLevel"/> gates and for reading a
/// level out of untrusted configuration.
/// </summary>
public static class TracingLevelExtensions
{
    /// <summary>Whether this level captures anything at all.</summary>
    /// <param name="level">The configured level.</param>
    /// <returns><see langword="true"/> for every level except <see cref="TracingLevel.Off"/>.</returns>
    /// <remarks>
    /// Use this as the cheap early-out before building any event payload:
    /// <see cref="TracingLevel.Errors"/> is the lowest active level, so a level
    /// that is not active can skip capture entirely.
    /// </remarks>
    public static bool IsActive(this TracingLevel level)
    {
        return level >= TracingLevel.Errors;
    }

    /// <summary>Whether the configured level is verbose enough to capture something.</summary>
    /// <param name="current">The level in effect, typically from configuration.</param>
    /// <param name="required">The minimum level the event being considered needs.</param>
    /// <returns><see langword="true"/> when <paramref name="current"/> is at least <paramref name="required"/>.</returns>
    /// <remarks>
    /// Prefer this to comparing levels with <c>==</c>: a check for exactly
    /// <see cref="TracingLevel.Narrative"/> would also drop the event when the
    /// user asked for the strictly more verbose <see cref="TracingLevel.Detail"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// if (config.Level.IsEnabled(TracingLevel.Detail))
    /// {
    ///     capture.RecordParameters(args);
    /// }
    /// </code>
    /// </example>
    public static bool IsEnabled(
        this TracingLevel current,
        TracingLevel required)
    {
        return current >= required;
    }

    /// <summary>
    /// Leniently parses a level name for ambient configuration: null,
    /// blank, or unrecognized values degrade to <paramref name="fallback"/>
    /// rather than throwing, so bad config never crashes capture.
    /// </summary>
    /// <param name="name">
    /// A level name, matched case-insensitively against the member names
    /// ("off", "Errors", "DETAIL"). Surrounding whitespace is trimmed. Numeric
    /// strings are rejected — "3" degrades to the fallback rather than
    /// resolving to <see cref="TracingLevel.Narrative"/>.
    /// </param>
    /// <param name="fallback">The level to use when <paramref name="name"/> does not resolve.</param>
    /// <returns>The parsed level, or <paramref name="fallback"/>; never throws.</returns>
    public static TracingLevel FromName(
        string? name, TracingLevel fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var trimmed = name!.Trim();
        return Enum.TryParse<TracingLevel>(
                trimmed, ignoreCase: true, out var level)
            && Enum.IsDefined(typeof(TracingLevel), level)
            && string.Equals(
                level.ToString(), trimmed,
                StringComparison.OrdinalIgnoreCase)
            ? level
            : fallback;
    }
}
