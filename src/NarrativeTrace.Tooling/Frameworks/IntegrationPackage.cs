// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// The NarrativeTrace package a row adds, and how a project can already be referencing it.
/// </summary>
/// <param name="PackageId">The NuGet id the row's add line installs.</param>
public sealed record IntegrationPackage(string PackageId)
{
    /// <summary>The package id, validated: a <c>NarrativeTrace.*</c> id.</summary>
    public string PackageId { get; init; } = PackageId is not null && IsFamilyId(PackageId)
        ? PackageId
        : throw new ArgumentException($"a row adds a NarrativeTrace package, got {PackageId}", nameof(PackageId));

    /// <summary>
    /// Whether a manifest references the package: a <c>PackageReference</c> to it, or a
    /// <c>ProjectReference</c> to its project file (a solution that builds NarrativeTrace from source).
    /// Comment-only text never counts.
    /// </summary>
    public bool ReferencedIn(string manifestText)
    {
        var package = ManifestPatterns.Package(PackageId);
        var project = ManifestPatterns.Project(PackageId);
        return ManifestElements.Read(manifestText).Any(element => package(element) || project(element));
    }

    /// <summary><c>NarrativeTrace.</c> then dot-separated words of ASCII letters and digits — an id NuGet accepts.</summary>
    private static bool IsFamilyId(string id) =>
        id.Split('.') is ["NarrativeTrace", .. var rest] && rest.Length > 0
        && rest.All(word => word.Length > 0 && word.All(char.IsAsciiLetterOrDigit));

    /// <summary>
    /// The command that adds the package, pinned to <paramref name="familyVersion"/> when the project's
    /// NarrativeTrace version is known — a second package at another version is a drifted install the
    /// doctor's own alignment check would fail next.
    /// </summary>
    public string AddCommand(string? familyVersion) =>
        familyVersion is null
            ? $"dotnet add package {PackageId}"
            : $"dotnet add package {PackageId} --version {familyVersion}";
}
