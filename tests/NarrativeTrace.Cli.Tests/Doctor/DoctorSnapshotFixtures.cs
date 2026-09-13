// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;

namespace NarrativeTrace.Cli.Tests.Doctor;

/// <summary>
/// Builds baseline <see cref="DoctorSnapshot"/> values for check tests. Every
/// check test overrides only the field(s) it needs.
/// </summary>
internal static class DoctorSnapshotFixtures
{
    /// <summary>An empty snapshot with no project, source, or installed-package data.</summary>
    public static DoctorSnapshot Empty()
    {
        return new DoctorSnapshot(
            Cwd: "/project",
            DotNetRuntimeDescription: ".NET 10.0.0",
            Env: new Dictionary<string, string?>(),
            ProjectFiles: new Dictionary<string, string>(),
            SourceFiles: new Dictionary<string, string>(),
            OutputFiles: new Dictionary<string, string>(),
            ApprovedDirFiles: new Dictionary<string, string>(),
            AppSettingsFiles: new Dictionary<string, string>(),
            InstalledPackages: new Dictionary<string, string>());
    }

    /// <summary>A snapshot with one <c>.csproj</c> so the "has a project" checks find something.</summary>
    public static DoctorSnapshot WithProject(string content = "<Project></Project>")
    {
        return Empty() with { ProjectFiles = Files(("app.csproj", content)) };
    }

    /// <summary>A snapshot with the given source files.</summary>
    public static DoctorSnapshot WithSource(params (string Path, string Content)[] files)
    {
        return Empty() with { SourceFiles = Files(files) };
    }

    /// <summary>A snapshot with the given installed package versions.</summary>
    public static DoctorSnapshot WithPackages(params (string Id, string Version)[] packages)
    {
        return Empty() with { InstalledPackages = Files(packages) };
    }

    private static Dictionary<string, string> Files(params (string Key, string Value)[] entries)
    {
        return entries.ToDictionary(e => e.Key, e => e.Value);
    }
}
