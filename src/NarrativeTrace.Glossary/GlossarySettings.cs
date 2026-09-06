// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Locates the repository's committed <c>glossary.json</c> for the suite
/// harvest hook.
/// </summary>
/// <remarks>
/// The glossary feature is opt-in by file presence: no file, no harvesting.
/// An explicit <see cref="EnvKey"/> path wins; otherwise the file is searched
/// upward from the start directory (tests usually run from a build output
/// folder several levels below the repository root, where the glossary
/// lives — the same upward convention as <c>.git</c> discovery).
/// </remarks>
public static class GlossarySettings
{
    /// <summary>
    /// Environment key holding an explicit glossary file path, or
    /// <see cref="OffValue"/> to disable harvesting entirely.
    /// </summary>
    public const string EnvKey = "NARRATIVETRACE_GLOSSARY";

    /// <summary>
    /// Sentinel <see cref="EnvKey"/> value that disables the glossary feature,
    /// overriding the upward file search (needed by suites that must never
    /// harvest, e.g. unit tests of the harvesting hook itself).
    /// </summary>
    public const string OffValue = "off";

    /// <summary>Conventional glossary file name at the repository root (ADR-011).</summary>
    public const string DefaultFileName = "glossary.json";

    /// <summary>Resolves the glossary file path for this run.</summary>
    /// <param name="readEnv">Environment reader; must not be null.</param>
    /// <param name="startDirectory">Directory the upward search starts from; must not be blank.</param>
    /// <returns>
    /// The path from <see cref="EnvKey"/> when set (verbatim, even if the file
    /// does not exist — the caller reports a missing explicit file), else the
    /// nearest <c>glossary.json</c> in <paramref name="startDirectory"/> or an
    /// ancestor, else null (feature off).
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="readEnv"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="startDirectory"/> is blank.</exception>
    public static string? ResolveFile(
        Func<string, string?> readEnv, string startDirectory)
    {
        if (readEnv is null)
        {
            throw new ArgumentNullException(nameof(readEnv));
        }

        if (string.IsNullOrWhiteSpace(startDirectory))
        {
            throw new ArgumentException(
                "startDirectory must not be blank", nameof(startDirectory));
        }

        var explicitPath = readEnv(EnvKey);
        if (string.Equals(explicitPath, OffValue, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        return FindUpward(startDirectory);
    }

    private static string? FindUpward(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultFileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent!;
        }

        return null;
    }
}
