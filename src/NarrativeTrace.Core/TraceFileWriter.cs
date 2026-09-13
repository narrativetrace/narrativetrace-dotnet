// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;

namespace NarrativeTrace.Core;

/// <summary>
/// Minimal shared file writer for trace artifacts: creates the parent
/// directory and writes UTF-8 bytes that substitute rather than refuse
/// whatever the charset cannot represent.
/// </summary>
/// <remarks>
/// <para>
/// Narration and exception-message text never pass through
/// <see cref="ControlEscape"/> (that text is prose an author wrote, and the
/// renderers are meant to show it), so an unpaired surrogate can still reach
/// this last line before the filesystem. .NET's default UTF-8 encoder raises
/// on one — a value the application merely returned could fail the run that
/// traced it. This substitutes the replacement character instead: an
/// artifact with one U+FFFD in it is a readable artifact; a failed write is a
/// failed build.
/// </para>
/// <para>
/// Extracted from <see cref="TraceArtifactWriter"/> so every writer that
/// touches disk — the trace artifact writer, <see cref="NarrativeApproval"/>,
/// <see cref="ScenarioManifest"/> — shares one encoding policy rather than
/// each hand-rolling its own.
/// </para>
/// </remarks>
public static class TraceFileWriter
{
    private static readonly Encoding SubstitutingUtf8 = Encoding.GetEncoding(
        "utf-8",
        new EncoderReplacementFallback("�"),
        new DecoderReplacementFallback("�"));

    /// <summary>Writes <paramref name="content"/> to <paramref name="path"/>, creating parent directories as needed.</summary>
    /// <exception cref="IOException">The directory or file could not be written.</exception>
    /// <exception cref="UnauthorizedAccessException">The process lacks permission to write to <paramref name="path"/>.</exception>
    /// <remarks>Overwrites an existing file without warning.</remarks>
    public static void Write(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir!);
        }

        File.WriteAllBytes(path, SubstitutingUtf8.GetBytes(content));
    }
}
