// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Locates the repository root from the test assembly's output directory.
/// </summary>
/// <remarks>
/// Mirrors <c>Build.Tests.RepositoryPath</c> (<c>tests/BuildScript.Tests/RepositoryPath.cs</c>) and
/// Java's <c>Formats.readSchema()</c>, which reads the canonical schema from where it lives rather
/// than keeping a copy that can drift — this runtime's "where it lives" is
/// <c>tests/NarrativeTrace.Core.Tests/Schemas/</c>, since there is no <c>src/</c>-level copy.
/// </remarks>
internal static class RepositoryPath
{
    /// <summary>The directory holding <c>NarrativeTrace.sln</c>.</summary>
    /// <exception cref="InvalidOperationException">No ancestor of the test output directory holds the solution.</exception>
    internal static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "NarrativeTrace.sln")))
            dir = dir.Parent;
        return dir?.FullName
            ?? throw new InvalidOperationException("NarrativeTrace.sln not found above " + AppContext.BaseDirectory);
    }
}
