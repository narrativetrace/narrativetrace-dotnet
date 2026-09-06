// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// Assigns short, stable participant aliases (such as <c>C</c> or <c>SR2</c>)
/// to class names for the sequence-diagram renderers.
/// </summary>
/// <remarks>
/// An alias is the upper-case initials of the class name (the first two, or
/// the whole name when it has fewer than two capitals). Collisions are
/// resolved by appending an incrementing numeric suffix starting at <c>2</c>.
/// The <paramref name="existing"/> map is both the cache of already-assigned
/// aliases and the source of truth for detecting conflicts, so repeated calls
/// for the same class name return the same alias.
/// </remarks>
internal static class AliasGenerator
{
    /// <summary>
    /// Returns the alias for <paramref name="className"/>, creating and
    /// caching a new collision-free one in <paramref name="existing"/> on
    /// first use.
    /// </summary>
    /// <param name="className">The fully qualified or simple class name.</param>
    /// <param name="existing">
    /// The class-name-to-alias map; read for a cached alias and updated with
    /// any newly generated one.
    /// </param>
    /// <returns>The short alias assigned to <paramref name="className"/>.</returns>
    public static string Generate(
        string className,
        Dictionary<string, string> existing)
    {
        if (existing.TryGetValue(className, out var cached))
        {
            return cached;
        }

        var candidate = ExtractInitials(className);
        candidate = ResolveConflict(candidate, existing);
        existing[className] = candidate;
        return candidate;
    }

    private static string ExtractInitials(string name)
    {
        var initials = string.Concat(
            name.Where(char.IsUpper));

        if (initials.Length >= 2)
        {
            return initials.Substring(0, 2);
        }

        return initials.Length == 1 ? initials : SafeToken(name);
    }

    // A class name with no uppercase letters falls back to being its own
    // alias, and — unlike a participant's display name — an alias is never
    // quoted at its use site: every arrow line emits it bare. A name with a
    // space, a colon, or a control character (a hostile input, or simply an
    // unusual identifier such as an F# name declared with backticks) would
    // otherwise reach an arrow unquoted and break the line. Ordinary
    // all-lowercase names (letters/digits only) pass through byte for byte —
    // AliasGeneratorTests pins "scheduler" staying "scheduler".
    private const int MaxFallbackLength = 20;

    private static string SafeToken(string name)
    {
        var sb = new StringBuilder(Math.Min(name.Length, MaxFallbackLength));
        for (var i = 0; i < name.Length && sb.Length < MaxFallbackLength; i++)
        {
            sb.Append(char.IsLetterOrDigit(name[i]) ? name[i] : '_');
        }

        return sb.Length == 0 ? "N" : sb.ToString();
    }

    private static string ResolveConflict(
        string candidate,
        Dictionary<string, string> existing)
    {
        if (!existing.ContainsValue(candidate))
        {
            return candidate;
        }

        var suffix = 2;
        while (existing.ContainsValue(
            candidate + suffix))
        {
            suffix++;
        }

        return candidate + suffix;
    }
}
