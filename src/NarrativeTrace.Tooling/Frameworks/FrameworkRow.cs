// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// One row of the framework table: marker → package → wiring → doctor check → Tier B case.
/// </summary>
/// <remarks>
/// A new framework is a new row, never new machinery: the doctor builds one check per
/// <see cref="CheckBinding.WiringCheck"/> row, and the documentation's framework table and
/// covered-frameworks line render from the same rows.
/// </remarks>
/// <param name="Id">The stable, kebab-case row id (<c>aspnetcore</c>, <c>xunit</c>, …) — a cross-port contract.</param>
/// <param name="Name">The framework as a reader names it.</param>
/// <param name="Marker">What in the project's manifests proves the framework is present.</param>
/// <param name="Package">The NarrativeTrace package the row adds.</param>
/// <param name="Wiring">What the project does beyond referencing the package, and how the doctor sees it.</param>
/// <param name="Check">Which doctor check observes the row.</param>
/// <param name="TierBCase">The Tier B eval case that exercises the row, or <see cref="NoTierBCase"/>.</param>
/// <param name="Guide">The repository-relative documentation page (and anchor) the row's finding links to.</param>
public sealed record FrameworkRow(
    string Id,
    string Name,
    FrameworkMarker Marker,
    IntegrationPackage Package,
    Wiring Wiring,
    CheckBinding Check,
    string TierBCase,
    string Guide)
{
    /// <summary>The <see cref="TierBCase"/> of a row no Tier B case exercises yet.</summary>
    public const string NoTierBCase = "none";

    /// <summary>The row's id, validated: kebab-case.</summary>
    public string Id { get; init; } = Kebab.IsKebab(Id)
        ? Id
        : throw new ArgumentException($"a row id is kebab-case, got {Id}", nameof(Id));

    /// <summary>The framework's name, validated: not blank.</summary>
    public string Name { get; init; } = string.IsNullOrWhiteSpace(Name)
        ? throw new ArgumentException("a row names its framework", nameof(Name))
        : Name;

    /// <summary>The row's check, validated: a wiring check only for wiring the doctor can see in source.</summary>
    public CheckBinding Check { get; init; } = Check is CheckBinding.WiringCheck && Wiring is not Wiring.Snippet
        ? throw new ArgumentException($"row {Id} earns a wiring check only for wiring the doctor can see in source", nameof(Check))
        : Check ?? throw new ArgumentNullException(nameof(Check));

    /// <summary>The row's Tier B case, validated: a case name or <see cref="NoTierBCase"/>.</summary>
    public string TierBCase { get; init; } = string.IsNullOrWhiteSpace(TierBCase)
        ? throw new ArgumentException($"row {Id} names a Tier B case or {NoTierBCase}", nameof(TierBCase))
        : TierBCase;
}
