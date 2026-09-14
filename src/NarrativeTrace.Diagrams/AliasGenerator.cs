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
        candidate = GuardReservedWord(candidate);
        candidate = ResolveConflict(candidate, existing);
        existing[className] = candidate;
        return candidate;
    }

    // A bare alias is emitted unquoted on every arrow and, on the left of "as", in every
    // participant declaration — both sequence grammars this alias serves. A class name whose
    // candidate collides with one of the grammars' own reserved words would hand back the exact
    // word the parser reserves for something else — "end" closes a Mermaid block outright rather
    // than naming a participant, and PlantUML's own "as" would collide with the two-initial path
    // (e.g. "ArithmeticSum" extracts to "AS"), which is exactly why this guard runs on every
    // candidate — the two-initial and single-initial paths above, not only the all-lowercase
    // fallback — rather than being folded into just one of them. Sourced independently for each
    // grammar, verified 2026-09-13, cross-port finding: Mermaid's
    // reserved set is every single-word literal lexer rule in sequenceDiagram.jison
    // (mermaid-js/mermaid, case-insensitive grammar; "title" is included defensively even though
    // its rule only fires with same-line trailing text today); PlantUML's is every keyword its own
    // sequence-diagram documentation (plantuml.com/sequence-diagram) names for participant
    // declarations, flow-control blocks, annotations and formatting, plus "as" and "order" (both
    // load-bearing in the declaration line itself). Both are guarded here because this one
    // generator's alias is emitted, unquoted, by both grammars — never split by call site, since a
    // Mermaid-only guard would leave PlantUML's larger reserved set unprotected and vice versa.
    private static readonly HashSet<string> ReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Mermaid sequenceDiagram.jison
        "sequencediagram", "participant", "actor", "create", "destroy", "box", "loop", "rect",
        "opt", "alt", "else", "par", "par_over", "and", "critical", "option", "break", "end",
        "links", "link", "properties", "details", "over", "note", "activate", "deactivate",
        "autonumber", "off", "title",
        // PlantUML sequence-diagram documentation (plantuml.com/sequence-diagram)
        "boundary", "control", "entity", "database", "collections", "queue", "group", "ref",
        "return", "hide", "show", "skinparam", "header", "footer", "newpage", "mainframe",
        "partition", "as", "order",
    };

    private static string GuardReservedWord(string candidate) =>
        ReservedAliases.Contains(candidate) ? candidate + "_" : candidate;

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
