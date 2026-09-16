// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The resolved per-test artifact-output settings — whether writing is enabled,
/// the target directory, and the format — read from the <c>NARRATIVETRACE_*</c>
/// environment. Shared by the xUnit and NUnit integrations so both gate output
/// identically.
/// </summary>
/// <param name="Enabled">
/// Whether artifacts are written at all. <b>On by default</b> (owner ruling,
/// 2026-09-11) — set <see cref="ConfigResolver.OutputKey"/> to <c>false</c>
/// to opt out.
/// </param>
/// <param name="Directory">The artifact root.</param>
/// <param name="Format">The primary artifact format.</param>
/// <param name="EntryArtifacts">
/// Which machine-readable entry arrays to write beside the trace file. Both
/// off by default, matching the Java runtime's opt-in switches.
/// </param>
/// <param name="ApprovalEnabled">
/// Approval mode <i>(since 0.1.5, unreleased)</i>: when on, a passing test
/// whose traced structure differs from its committed <c>*.approved.nt</c>
/// baseline fails with a readable diff. Off by default.
/// </param>
/// <param name="ApprovedDir">
/// Directory of committed approval baselines <i>(since 0.1.5, unreleased)</i>.
/// </param>
public sealed record TestArtifactSettings(
    bool Enabled,
    string Directory,
    TraceArtifactFormat Format,
    EntryArtifacts EntryArtifacts = default,
    bool ApprovalEnabled = false,
    string ApprovedDir = TestArtifactSettings.DefaultApprovedDir)
{
    /// <summary>
    /// The directory used when <see cref="ConfigResolver.OutputDirKey"/> isn't
    /// set: <c>TestResults/</c> is the one location every .NET test convention
    /// already treats as ephemeral — it's what <c>dotnet test
    /// --results-directory</c> and Visual Studio/Rider write TRX and coverage
    /// output into, and this repository's own <c>.gitignore</c> already
    /// excludes it — nested one level under <c>narrativetrace/</c> so this
    /// runtime's files don't collide with those. Relative, so it lands under
    /// the test run's working directory.
    /// </summary>
    public const string DefaultDirectory = "TestResults/narrativetrace";

    /// <summary>
    /// The directory used when <see cref="ConfigResolver.ApprovedDirKey"/>
    /// isn't set. Deliberately <b>not</b> under <see cref="DefaultDirectory"/>:
    /// approved traces are committed, reviewed files, not ephemeral output, so
    /// they must never share a root with something <c>.gitignore</c> excludes.
    /// Mirrors the Java runtime's <c>src/test/narratives</c> default, adapted
    /// to this port's flatter test-project layout (no <c>src/test</c>
    /// segregation).
    /// </summary>
    public const string DefaultApprovedDir = "narratives";

    /// <summary>Resolves artifact settings from a configuration source.</summary>
    /// <param name="read">
    /// Reads a <c>NARRATIVETRACE_*</c> variable by name, returning
    /// <see langword="null"/> when unset. Inject
    /// <see cref="Environment.GetEnvironmentVariable(string)"/> in production and
    /// a dictionary lookup in tests, so tests never mutate process state.
    /// </param>
    /// <returns>
    /// The resolved settings. Never throws: unparseable values degrade to
    /// defaults, so misconfiguration disables or defaults output rather than
    /// failing the test run.
    /// </returns>
    public static TestArtifactSettings Resolve(Func<string, string?> read)
    {
        var resolved = ConfigResolver.Resolve(read);
        return new TestArtifactSettings(
            resolved.Output,
            resolved.OutputDir ?? DefaultDirectory,
            TraceArtifactFormatExtensions.FromName(
                read(ConfigResolver.FormatKey), TraceArtifactFormat.Markdown),
            new EntryArtifacts(resolved.CanonicalJson, resolved.StructuralJson),
            resolved.Approval,
            resolved.ApprovedDir ?? DefaultApprovedDir);
    }
}
