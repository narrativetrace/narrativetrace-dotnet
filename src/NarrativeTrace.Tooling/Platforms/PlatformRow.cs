// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Tooling.Frameworks;

namespace NarrativeTrace.Tooling.Platforms;

/// <summary>
/// One row of the platform table: marker → pointer → install path for a stranger → evidence lane.
/// </summary>
/// <remarks>
/// A new platform is a new row, never new machinery: <c>init</c>, <c>uninstall</c>, the doctor, the
/// Cursor plugin manifest and the documentation page all read the same rows.
/// </remarks>
/// <param name="Id">The stable, kebab-case row id (<c>cursor</c>, <c>antigravity</c>, …) — a cross-port contract.</param>
/// <param name="Name">The platform as a reader names it.</param>
/// <param name="MarkerPaths">
/// Project-relative paths whose existence proves the platform is in use; any one is enough. Matched
/// as exact paths, so a near-miss such as <c>.cursorx/</c> proves nothing.
/// </param>
/// <param name="Pointer">What <c>init</c> writes for this platform.</param>
/// <param name="InstallPath">How a stranger gets the skills on this platform, as the page prints it.</param>
/// <param name="AgentFinds">What the platform's agent will find in a project that ran <c>init</c>.</param>
/// <param name="EvidenceLane">The Tier B lane id that exercises the platform, or <see cref="Manual"/>.</param>
/// <param name="Verified">
/// The date the row's platform facts were last checked against the vendor's own documentation,
/// <c>yyyy-mm-dd</c>.
/// </param>
public sealed partial record PlatformRow(
    string Id,
    string Name,
    IReadOnlyList<string> MarkerPaths,
    Pointer Pointer,
    string InstallPath,
    string AgentFinds,
    string EvidenceLane,
    string Verified)
{
    /// <summary>The <see cref="EvidenceLane"/> of a platform no automated trial exercises.</summary>
    public const string Manual = "manual";

    /// <summary>The row's id, validated: kebab-case.</summary>
    public string Id { get; init; } = Kebab.IsKebab(Id)
        ? Id
        : throw new ArgumentException($"a row id is kebab-case, got {Id}", nameof(Id));

    /// <summary>The platform's name, validated: not blank.</summary>
    public string Name { get; init; } = Text(Name, Id, "name");

    /// <summary>The marker paths, validated: at least one, or the row could never be detected.</summary>
    public IReadOnlyList<string> MarkerPaths { get; init; } = MarkerPaths is { Count: > 0 }
        ? [.. MarkerPaths]
        : throw new ArgumentException($"row {Id} has no marker path", nameof(MarkerPaths));

    /// <summary>The pointer, validated: present (use <see cref="Platforms.Pointer.None"/> for "none").</summary>
    public Pointer Pointer { get; init; } = Pointer
        ?? throw new ArgumentException($"row {Id} leaves its pointer column empty", nameof(Pointer));

    /// <summary>The install path, validated: not blank.</summary>
    public string InstallPath { get; init; } = Text(InstallPath, Id, "install path");

    /// <summary>The agent-finds text, validated: not blank.</summary>
    public string AgentFinds { get; init; } = Text(AgentFinds, Id, "agent-finds text");

    /// <summary>The evidence lane, validated: a lane id or <see cref="Manual"/>.</summary>
    public string EvidenceLane { get; init; } = Kebab.IsKebab(EvidenceLane)
        ? EvidenceLane
        : throw new ArgumentException($"row {Id} names a lane id or {Manual}", nameof(EvidenceLane));

    /// <summary>The verification date, validated: <c>yyyy-mm-dd</c>.</summary>
    public string Verified { get; init; } = Verified is not null && IsoDate().IsMatch(Verified)
        ? Verified
        : throw new ArgumentException($"row {Id} carries a yyyy-mm-dd verification date", nameof(Verified));

    private static string Text(string? value, string id, string column) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"row {id} leaves its {column} empty", column)
            : value;

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}\z")]
    private static partial Regex IsoDate();
}
