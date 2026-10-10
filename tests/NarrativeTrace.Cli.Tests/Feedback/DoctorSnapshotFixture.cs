// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;

namespace NarrativeTrace.Cli.Tests.Feedback;

/// <summary>
/// Builds a minimal <see cref="DoctorSnapshot"/> for the feedback verb's tests, which only read the
/// installed packages and the output files.
/// </summary>
internal static class DoctorSnapshotFixture
{
    internal static DoctorSnapshot With(
        IReadOnlyDictionary<string, string>? installed = null,
        IReadOnlyDictionary<string, string>? output = null) =>
        new(
            Cwd: "/project",
            DotNetRuntimeDescription: ".NET 10.0.0",
            Env: new Dictionary<string, string?>(),
            ProjectFiles: new Dictionary<string, string> { ["app.csproj"] = "<Project></Project>" },
            SourceFiles: new Dictionary<string, string>(),
            OutputFiles: output ?? new Dictionary<string, string>(),
            ApprovedDirFiles: new Dictionary<string, string>(),
            AppSettingsFiles: new Dictionary<string, string>(),
            InstalledPackages: installed ?? new Dictionary<string, string>(),
            InstalledSkills: [],
            CarrierCatalogueSkills: [],
            ProjectResolvedFamilyVersion: null);
}
