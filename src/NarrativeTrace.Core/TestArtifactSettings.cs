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
/// <param name="Enabled">Whether artifacts are written at all.</param>
/// <param name="Directory">The artifact root.</param>
/// <param name="Format">The primary artifact format.</param>
/// <param name="EntryArtifacts">
/// Which machine-readable entry arrays to write beside the trace file. Both
/// off by default, matching the Java edition's opt-in switches.
/// </param>
public sealed record TestArtifactSettings(
    bool Enabled,
    string Directory,
    TraceArtifactFormat Format,
    EntryArtifacts EntryArtifacts = default)
{
    /// <summary>
    /// The directory used when none is configured — relative, so it lands under
    /// the test run's working directory.
    /// </summary>
    public const string DefaultDirectory = "narrativetrace-output";

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
            new EntryArtifacts(resolved.CanonicalJson, resolved.StructuralJson));
    }
}
