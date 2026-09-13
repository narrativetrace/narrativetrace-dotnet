// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor;

/// <summary>
/// A pure, in-memory bag of everything the doctor checks read. Built once,
/// impurely, by <see cref="DoctorSnapshotBuilder"/> — every
/// <see cref="DoctorCheck"/> is then a pure function of this value, so each
/// check is unit-testable with zero disk or process I/O.
/// </summary>
/// <param name="Cwd">The directory the doctor was run from.</param>
/// <param name="DotNetRuntimeDescription">
/// The running host's framework description (e.g. <c>.NET 10.0.0</c>),
/// analogous to a Node CLI reporting its own <c>process.version</c>.
/// </param>
/// <param name="Env">
/// <c>NARRATIVETRACE_*</c> environment variables, by name. Absent keys mean
/// unset — never guess a default here; each check applies its own.
/// </param>
/// <param name="ProjectFiles">Every <c>*.csproj</c> under <see cref="Cwd"/>, path to content.</param>
/// <param name="SourceFiles">Every <c>*.cs</c> under <see cref="Cwd"/> (bounded walk), path to content.</param>
/// <param name="OutputFiles">Files under the configured trace-output directory, path to content.</param>
/// <param name="ApprovedDirFiles">Files under the configured approval-trace directory, path to content.</param>
/// <param name="AppSettingsFiles">Every <c>appsettings*.json</c> under <see cref="Cwd"/>, path to content.</param>
/// <param name="InstalledPackages">
/// Resolved <c>NarrativeTrace.*</c> package versions from NuGet restore
/// output (<c>obj/project.assets.json</c>), package id to version.
/// </param>
public sealed record DoctorSnapshot(
    string Cwd,
    string DotNetRuntimeDescription,
    IReadOnlyDictionary<string, string?> Env,
    IReadOnlyDictionary<string, string> ProjectFiles,
    IReadOnlyDictionary<string, string> SourceFiles,
    IReadOnlyDictionary<string, string> OutputFiles,
    IReadOnlyDictionary<string, string> ApprovedDirFiles,
    IReadOnlyDictionary<string, string> AppSettingsFiles,
    IReadOnlyDictionary<string, string> InstalledPackages);
