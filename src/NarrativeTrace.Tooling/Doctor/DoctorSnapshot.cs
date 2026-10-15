// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Frameworks;
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Tooling.Doctor;

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
/// <param name="ProjectFiles">
/// Every MSBuild project manifest under <see cref="Cwd"/> — <c>*.csproj</c>, <c>*.fsproj</c>,
/// <c>*.vbproj</c>, <c>Directory.Build.props</c> and <c>Directory.Build.targets</c> — path to content.
/// </param>
/// <param name="SourceFiles">Every <c>*.cs</c> under <see cref="Cwd"/> (bounded walk), path to content.</param>
/// <param name="OutputFiles">Files under the configured trace-output directory, path to content.</param>
/// <param name="ApprovedDirFiles">Files under the configured approval-trace directory, path to content.</param>
/// <param name="AppSettingsFiles">Every <c>appsettings*.json</c> under <see cref="Cwd"/>, path to content.</param>
/// <param name="InstalledPackages">
/// Resolved <c>NarrativeTrace.*</c> package versions from NuGet restore
/// output (<c>obj/project.assets.json</c>), package id to version.
/// </param>
/// <param name="InstalledSkills">
/// Every agent-skill directory found under either install root, read through the installer's own
/// <see cref="ProjectStateReader"/> — there is one reader of a project's skill directories, not a
/// doctor-shaped second copy of it that could disagree about what "ours" means.
/// </param>
/// <param name="CarrierCatalogueSkills">
/// Every skill name the resolved carrier's catalogue lists, or empty when the carrier could not be
/// opened.
/// </param>
/// <param name="ProjectResolvedFamilyVersion">
/// The single NarrativeTrace family version this project resolves (the version guard's input,
/// read the same way <see cref="ProjectStateReader"/> reads it), or <see langword="null"/> when
/// none can be singled out — no restore output, no <c>NarrativeTrace.*</c> package, or a drifted
/// install. <see langword="null"/> is "cannot tell", never "nothing installed".
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
    IReadOnlyDictionary<string, string> InstalledPackages,
    IReadOnlyList<InstalledSkill> InstalledSkills,
    IReadOnlyList<string> CarrierCatalogueSkills,
    string? ProjectResolvedFamilyVersion)
{
    /// <summary>
    /// Every <c>*.runsettings</c> under <see cref="Cwd"/>, path to content — where a test run's
    /// environment variables (approval mode among them) are set for <c>dotnet test</c>. Empty by
    /// default, so a snapshot built without it reads as "no run settings".
    /// </summary>
    public IReadOnlyDictionary<string, string> RunSettingsFiles { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// One reading per detected platform whose row has a pointer file, in platform-table order — the
    /// installer's own reading, so the doctor and <c>init</c> agree on what "present" means. Empty by
    /// default: a project on no platform that needs a pointer has nothing to be missing.
    /// </summary>
    public IReadOnlyList<PointerReading> PointerReadings { get; init; } = [];

    /// <summary>
    /// Every project manifest's text, each stripped of its own comments and CDATA sections, then joined
    /// — what a framework-table marker or a package reference is looked for in. Stripped one file at a
    /// time, so an unterminated comment in one manifest cannot hide a reference in the next.
    /// </summary>
    public string ManifestText() =>
        string.Join("\n", ProjectFiles.Values.Select(ManifestPatterns.WithoutCommentsAndCData));

    /// <summary>
    /// Every source file's text with its C# comments removed — what wiring evidence is looked for in,
    /// so commented-out wiring is not wiring. String literals stay: a source name such as
    /// <c>AddSource("NarrativeTrace")</c> is evidence only inside one.
    /// </summary>
    public IReadOnlyList<string> SourceTexts() =>
        SourceFiles.Values.Select(CSharpSource.WithoutComments).ToList();
}
