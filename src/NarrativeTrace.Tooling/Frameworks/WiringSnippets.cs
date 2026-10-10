// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;

namespace NarrativeTrace.Tooling.Frameworks;

/// <summary>
/// The wiring lines of every source-wired framework row, read from <c>wiring-snippets.md</c> embedded
/// in this assembly.
/// </summary>
/// <remarks>
/// <para>INTENT: the doctor's fix must carry the exact lines a project adds, and those lines must never
/// be typed — they are a compiled, tested fixture's. The resource holds one <c>## &lt;row id&gt;</c>
/// section per row, each wrapping a fenced block in the same <c>&lt;!-- snippet: path region=… --&gt;</c>
/// markers the documentation uses, so the build's <c>SnippetSync</c> writes the fixture's current text
/// into it and <c>SnippetCheck</c> fails the build when the two drift. The assembly then carries the
/// text to wherever the doctor runs, with no repository in sight.</para>
/// </remarks>
public static class WiringSnippets
{
    /// <summary>The resource's logical name inside this assembly.</summary>
    public const string ResourceName = "NarrativeTrace.Tooling.Frameworks.wiring-snippets.md";

    private const string Heading = "## ";
    private const string Fence = "```";

    private static readonly Lazy<IReadOnlyDictionary<string, SnippetEntry>> Loaded = new(Load);

    /// <summary>One row's snippet: the fixture its marker names, the region (<see langword="null"/> for a whole file), and the block's body.</summary>
    /// <param name="Fixture">The repository-relative fixture path.</param>
    /// <param name="Region">The region name, or <see langword="null"/>.</param>
    /// <param name="Body">The fenced block's lines, each newline-terminated.</param>
    public sealed record SnippetEntry(string Fixture, string? Region, string Body);

    /// <summary>Every section, by row id, in document order.</summary>
    public static IReadOnlyDictionary<string, SnippetEntry> Entries => Loaded.Value;

    /// <summary>The wiring lines of the row with this id.</summary>
    public static string Text(string rowId) => Entry(rowId).Body;

    /// <summary>The parsed entry for this row.</summary>
    /// <exception cref="InvalidOperationException">
    /// The resource has no section for the row — a table row without its snippet is a build defect the
    /// drift test exists to catch, never a runtime case.
    /// </exception>
    public static SnippetEntry Entry(string rowId) =>
        Entries.TryGetValue(rowId, out var entry)
            ? entry
            : throw new InvalidOperationException($"wiring-snippets.md has no section for row {rowId}");

    /// <summary>
    /// Parses the resource's text: each <c>## id</c> section's snippet marker and the fenced block right
    /// after it. A fenced block is skipped whole wherever it appears, so a heading INSIDE one is body,
    /// never a new section; an unterminated block is never an entry. Line-based on purpose — no pattern
    /// walks the document.
    /// </summary>
    internal static IReadOnlyDictionary<string, SnippetEntry> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var entries = new Dictionary<string, SnippetEntry>(StringComparer.Ordinal);
        var i = 0;
        while (i < lines.Length)
        {
            if (lines[i].StartsWith(Fence, StringComparison.Ordinal))
            {
                i = ClosingFence(lines, i + 1) + 1;
            }
            else if (lines[i].StartsWith(Heading, StringComparison.Ordinal))
            {
                i = ReadSection(lines, i + 1, lines[i][Heading.Length..].Trim(), entries);
            }
            else
            {
                i++;
            }
        }

        return entries;
    }

    /// <summary>
    /// Reads one section from the line after its heading up to its marked block, recording the entry.
    /// Returns where the scan resumes: after the block, or at the first heading or unmarked fence.
    /// </summary>
    private static int ReadSection(string[] lines, int from, string id, Dictionary<string, SnippetEntry> entries)
    {
        var i = from;
        while (i < lines.Length && !IsHeadingOrFence(lines[i]))
        {
            if (Marker(lines[i]) is { } marker && i + 1 < lines.Length && lines[i + 1].StartsWith(Fence, StringComparison.Ordinal))
            {
                var close = ClosingFence(lines, i + 2);
                if (close == lines.Length)
                {
                    return close;
                }

                var body = string.Concat(lines[(i + 2)..close].Select(line => line + "\n"));
                entries.TryAdd(id, marker with { Body = body });
                return close + 1;
            }

            i++;
        }

        return i;
    }

    private static bool IsHeadingOrFence(string line) =>
        line.StartsWith(Heading, StringComparison.Ordinal) || line.StartsWith(Fence, StringComparison.Ordinal);

    /// <summary>The first line at or after <paramref name="from"/> that closes a fence, or the line count when none does.</summary>
    private static int ClosingFence(string[] lines, int from)
    {
        var i = from;
        while (i < lines.Length && !lines[i].StartsWith(Fence, StringComparison.Ordinal))
        {
            i++;
        }

        return i;
    }

    /// <summary><c>&lt;!-- snippet: path [region=name] [mask=…] --&gt;</c> as an entry with an empty body; <see langword="null"/> for any other line.</summary>
    private static SnippetEntry? Marker(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length < "<!---->".Length
            || !trimmed.StartsWith("<!--", StringComparison.Ordinal) || !trimmed.EndsWith("-->", StringComparison.Ordinal))
        {
            return null;
        }

        var words = trimmed[4..^3].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words[0] != "snippet:")
        {
            return null;
        }

        var region = words.Skip(2).FirstOrDefault(word => word.StartsWith("region=", StringComparison.Ordinal));
        return new SnippetEntry(words[1], region?["region=".Length..], string.Empty);
    }

    private static IReadOnlyDictionary<string, SnippetEntry> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"{ResourceName} is missing from the assembly");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    }
}
