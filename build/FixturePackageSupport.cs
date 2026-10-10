// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

/// <summary>
/// The Phase 7 Tier B fixtures' third-party dependencies, kept in the repository as an ENCODED
/// bundle (gzip, then base64) and unpacked into a temp directory only while
/// <c>./build.sh SkillsEvalFeed</c> packs them.
/// </summary>
/// <remarks>
/// A trial agent runs as the same user and can read this repository; the first verify trial followed
/// the package name into <c>skills-evals/fixture-packages</c> and read the very source whose behaviour
/// the case hides (cross-port item 9). No file name or plain-text line here carries the package's
/// names, so neither a name search nor a grep leads there. The bundle is one file per section:
/// <c>@@file &lt;relative path&gt;</c>, the content, <c>@@end</c>.
/// </remarks>
public static class FixturePackageSupport
{
    private const string FileMarker = "@@file ";
    private const string EndMarker = "@@end";

    /// <summary>The bundle text of <paramref name="files"/> (relative path to content), encoded.</summary>
    public static string Encode(IReadOnlyDictionary<string, string> files)
    {
        var bundle = new StringBuilder();
        foreach (var (path, content) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            bundle.Append(FileMarker).Append(path.Replace('\\', '/')).Append('\n')
                .Append(content.Replace("\r\n", "\n").TrimEnd('\n')).Append('\n')
                .Append(EndMarker).Append('\n');
        }

        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(bundle.ToString());
            gzip.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(compressed.ToArray(), Base64FormattingOptions.InsertLineBreaks) + "\n";
    }

    /// <summary>The files of an encoded bundle, relative path to content.</summary>
    /// <exception cref="InvalidDataException">The bundle is malformed or names a path outside its root.</exception>
    public static IReadOnlyDictionary<string, string> Decode(string encoded)
    {
        using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(encoded)), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return Sections(reader.ReadToEnd().Split('\n'));
    }

    /// <summary>Writes an encoded bundle's files under <paramref name="target"/>.</summary>
    public static void Unpack(string encoded, string target)
    {
        foreach (var (path, content) in Decode(encoded))
        {
            var file = Path.Combine(target, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, content + "\n");
        }
    }

    private static Dictionary<string, string> Sections(string[] lines)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        var i = 0;
        while (i < lines.Length && lines[i].Length > 0)
        {
            var path = SafePath(lines[i]);
            var end = Array.IndexOf(lines, EndMarker, i + 1);
            if (end < 0)
            {
                throw new InvalidDataException("bundle section without " + EndMarker + ": " + path);
            }

            files[path] = string.Join("\n", lines[(i + 1)..end]);
            i = end + 1;
        }

        return files;
    }

    private static string SafePath(string header)
    {
        if (!header.StartsWith(FileMarker, StringComparison.Ordinal))
        {
            throw new InvalidDataException("bundle line is not a section header: " + header);
        }

        var path = header[FileMarker.Length..];
        if (path.Length == 0 || Path.IsPathRooted(path) || path.Split('/').Contains(".."))
        {
            throw new InvalidDataException("bundle path escapes its root: " + path);
        }

        return path;
    }
}
