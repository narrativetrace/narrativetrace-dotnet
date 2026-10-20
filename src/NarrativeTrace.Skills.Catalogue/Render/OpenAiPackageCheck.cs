// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Opens a built OpenAI plugin ZIP and checks it against the layout the catalogue derived: its entries
/// equal <see cref="OpenAiPackageLayout.Entries"/>, its version is semantic and matches the release,
/// every <c>"./"</c>-relative path the manifest names resolves to a packaged entry, each icon the
/// manifest points at is square and within the vendor's size bounds, and no forbidden entry
/// (<c>hooks/</c>, <c>mcp.json</c>, a <c>.app.json</c> or a <c>.class</c>) is present.
/// </summary>
public static class OpenAiPackageCheck
{
    private const int MinIconPixels = 48;
    private const long MaxIconBytes = 5L * 1024 * 1024;
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex RelativePath = new("\"\\./([^\"]+)\"", RegexOptions.None, RegexTimeout);
    private static readonly Regex VersionLine = new("\"version\": \"([^\"]*)\"", RegexOptions.None, RegexTimeout);

    // \z, not $: .NET's $ also matches before a trailing newline, which would let "1.2.3\n" through;
    // [0-9], not \d: \d accepts every Unicode decimal digit.
    private static readonly Regex Semver = new(@"^[0-9]+\.[0-9]+\.[0-9]+\z", RegexOptions.None, RegexTimeout);

    /// <summary>Every way <paramref name="zipPath"/> departs from the package it must be; empty when it is exactly right.</summary>
    /// <param name="zipPath">The built ZIP.</param>
    /// <param name="expectedEntries">The entry list derived from the catalogue.</param>
    /// <param name="expectedVersion">The release version the manifest must carry.</param>
    /// <exception cref="InvalidOperationException">The ZIP holds no <c>plugin.json</c> to check.</exception>
    public static IReadOnlyList<string> Violations(string zipPath, IReadOnlyList<string> expectedEntries, string expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(zipPath);
        ArgumentNullException.ThrowIfNull(expectedEntries);
        ArgumentNullException.ThrowIfNull(expectedVersion);
        var problems = new List<string>();
        using var archive = ZipFile.OpenRead(zipPath);
        var actual = SortedEntryNames(archive);
        CheckEntrySet(expectedEntries, actual, problems);
        problems.AddRange(actual.Where(IsForbidden).Select(name => $"forbidden entry present: {name}"));
        var manifest = ReadText(archive, OpenAiPackageLayout.ManifestEntry);
        CheckVersion(manifest, expectedVersion, problems);
        CheckRelativePaths(manifest, actual, archive, problems);
        return problems;
    }

    /// <summary>Directory entries (a name ending in <c>/</c>) are a ZIP-format artifact, not packaged content.</summary>
    private static List<string> SortedEntryNames(ZipArchive archive) =>
        [.. archive.Entries.Where(e => !e.FullName.EndsWith('/')).Select(e => e.FullName).OrderBy(n => n, StringComparer.Ordinal)];

    private static void CheckEntrySet(IReadOnlyList<string> expected, List<string> actual, List<string> problems)
    {
        var expectedSorted = expected.OrderBy(n => n, StringComparer.Ordinal).ToList();
        if (!expectedSorted.SequenceEqual(actual, StringComparer.Ordinal))
        {
            problems.Add($"entries do not match the layout — expected [{string.Join(", ", expectedSorted)}] but found [{string.Join(", ", actual)}]");
        }
    }

    /// <summary>Case-insensitive: a case-insensitive file system unpacks <c>HOOKS/x</c> as the directory the vendor refuses.</summary>
    private static bool IsForbidden(string name) =>
        name.StartsWith("hooks/", StringComparison.OrdinalIgnoreCase)
        || name.Equals("mcp.json", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".app.json", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".class", StringComparison.OrdinalIgnoreCase);

    private static void CheckVersion(string manifest, string expectedVersion, List<string> problems)
    {
        if (!Semver.IsMatch(expectedVersion))
        {
            problems.Add($"version '{expectedVersion}' is not semantic (X.Y.Z, no suffix)");
        }

        var line = VersionLine.Match(manifest);
        if (!line.Success)
        {
            problems.Add("plugin.json carries no \"version\" key");
        }
        else if (line.Groups[1].Value != expectedVersion)
        {
            problems.Add($"plugin.json version '{line.Groups[1].Value}' does not equal {expectedVersion}");
        }
    }

    private static void CheckRelativePaths(string manifest, List<string> actual, ZipArchive archive, List<string> problems)
    {
        var present = new HashSet<string>(actual, StringComparer.Ordinal);
        foreach (var path in RelativePath.Matches(manifest).Select(m => m.Groups[1].Value))
        {
            if (!present.Contains(path))
            {
                problems.Add($"manifest path './{path}' resolves to no packaged entry");
            }
            else if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                CheckIcon(archive, path, problems);
            }
        }
    }

    private static void CheckIcon(ZipArchive archive, string path, List<string> problems)
    {
        var entry = archive.GetEntry(path)!;
        if (entry.Length > MaxIconBytes)
        {
            problems.Add($"{path} is larger than 5 MiB");
            return;
        }

        try
        {
            CheckIconDimensions(path, ReadBytes(entry), problems);
        }
        catch (ArgumentException)
        {
            problems.Add($"{path} is not a readable PNG");
        }
    }

    private static void CheckIconDimensions(string path, byte[] bytes, List<string> problems)
    {
        var width = PngDimensions.Width(bytes);
        var height = PngDimensions.Height(bytes);
        if (width != height)
        {
            problems.Add($"{path} is not square ({width}x{height})");
        }
        else if (width < MinIconPixels)
        {
            problems.Add($"{path} is smaller than 48px ({width}px)");
        }
    }

    private static string ReadText(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidOperationException($"{name} missing from the zip");
        return new UTF8Encoding(false).GetString(ReadBytes(entry));
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
