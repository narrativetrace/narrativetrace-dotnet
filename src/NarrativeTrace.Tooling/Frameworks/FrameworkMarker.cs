// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// What in a project's manifests proves a framework is present — the first column of the table.
/// </summary>
/// <remarks>
/// <see cref="Manifest"/> patterns run over the start tags (<see cref="ManifestElements"/>) of every project file the doctor
/// read (<c>*.csproj</c>, <c>*.fsproj</c>, <c>*.vbproj</c>, <c>Directory.Build.props</c>/<c>.targets</c>),
/// never over a restored package graph: the doctor is text-and-manifest only and never runs a build.
/// <see cref="DeferTo"/> names rows that take precedence: a row whose marker matches stands down while
/// any row it defers to is present too (DI auto-wrap is the wrong advice for a web app, whose own row
/// wraps services against the request's context).
/// </remarks>
/// <param name="Description">The marker in a reader's words, as the docs and the doctor print it.</param>
/// <param name="Manifest">
/// Patterns any one of which proves presence; empty for a row never detected from a manifest (an
/// owner opt-in, or a row whose presence is an absence).
/// </param>
/// <param name="DeferTo">Ids of rows that take precedence when their own marker matches too.</param>
public sealed record FrameworkMarker(string Description, IReadOnlyList<ManifestPattern> Manifest, IReadOnlyList<string> DeferTo)
{
    /// <summary>The description, validated: not blank.</summary>
    public string Description { get; init; } = string.IsNullOrWhiteSpace(Description)
        ? throw new ArgumentException("a marker's description must not be blank", nameof(Description))
        : Description;
}
