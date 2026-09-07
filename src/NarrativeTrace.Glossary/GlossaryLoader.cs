// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Locates and reads the committed glossary for live translation.
/// </summary>
/// <remarks>
/// <para>
/// How live translation finds its glossary without any command-line
/// plumbing. A deployed application carries its <c>glossary.json</c> beside
/// its own assemblies; the <see cref="PathKey"/> environment variable points
/// to a file instead when the glossary lives outside the deployment (e.g. a
/// committed repository-root <c>glossary.json</c>). An absent glossary is a
/// normal state — translation simply stays off, the owner-requirement pattern
/// the vocabulary check already uses — while a configured-but-missing
/// override is a configuration error and fails fast.
/// </para>
/// <para>
/// <b>Platform note.</b> Java resolves the classpath resource
/// <c>glossary.json</c> at the resource root. .NET's counterpart of "beside
/// the deployed artifact" is the application base directory, which is also
/// what a published app's content files land in — so that is the probe, and
/// it needs no packaging step from the consumer.
/// </para>
/// <para>
/// This is the <b>runtime</b> half of glossary discovery, deliberately
/// separate from <see cref="GlossarySettings"/>: that one answers "which file
/// should this test run harvest into", searching upward from the working
/// directory, and may legitimately find nothing in a deployed process.
/// </para>
/// </remarks>
public static class GlossaryLoader
{
    /// <summary>Environment key pointing at a glossary file outside the deployment.</summary>
    public const string PathKey = "NARRATIVETRACE_GLOSSARY_PATH";

    /// <summary>Conventional glossary file name beside the deployed application.</summary>
    internal const string BaseDirectoryFileName = "glossary.json";

    /// <summary>
    /// Loads the glossary from the process environment and the application
    /// base directory.
    /// </summary>
    /// <returns>The glossary, or null when none is present.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="PathKey"/> points to a missing file.
    /// </exception>
    /// <exception cref="ArgumentException">A located glossary is malformed.</exception>
    /// <exception cref="IOException">A located glossary cannot be read.</exception>
    public static Glossary? Load()
    {
        return Load(Environment.GetEnvironmentVariable, AppContext.BaseDirectory);
    }

    /// <summary>
    /// Loads the glossary: the <see cref="PathKey"/> override first, else the
    /// file beside the application.
    /// </summary>
    /// <param name="readEnv">Environment reader; must not be null.</param>
    /// <param name="baseDirectory">
    /// Directory holding the deployed application; must not be blank.
    /// </param>
    /// <returns>
    /// The glossary, or null when no override is configured and no file
    /// exists.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="readEnv"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="baseDirectory"/> is blank, or a located glossary is
    /// malformed.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <see cref="PathKey"/> points to a missing file.
    /// </exception>
    /// <exception cref="IOException">A located glossary cannot be read.</exception>
    public static Glossary? Load(Func<string, string?> readEnv, string baseDirectory)
    {
        if (readEnv is null)
        {
            throw new ArgumentNullException(nameof(readEnv));
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException(
                "baseDirectory must not be blank", nameof(baseDirectory));
        }

        var overridePath = readEnv(PathKey);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return ReadOverride(overridePath!);
        }

        // Path.Combine's traversal hazard is a *trailing* segment that reroots the path;
        // the tainted value here is the leading directory and the trailing segment is a
        // compile-time constant, so the read is always <baseDirectory>/glossary.json and
        // no caller-supplied value can name a different file. Path.GetFileName — the only
        // sanitizer the rule accepts — would discard the directory and break discovery.
        var beside = Path.Combine(baseDirectory, BaseDirectoryFileName);
        return File.Exists(beside)
            ? GlossaryJsonReader.Read(File.ReadAllText(beside)) // nosemgrep: csharp.lang.security.filesystem.unsafe-path-combine.unsafe-path-combine
            : null;
    }

    /// <summary>
    /// A configured override that does not exist is a configuration error,
    /// not an absent glossary: somebody asked for a specific file and did not
    /// get it.
    /// </summary>
    private static Glossary ReadOverride(string path)
    {
        if (!File.Exists(path))
        {
            throw new InvalidOperationException(
                $"{PathKey} points to a missing file: {path}");
        }

        return GlossaryJsonReader.Read(File.ReadAllText(path));
    }
}
