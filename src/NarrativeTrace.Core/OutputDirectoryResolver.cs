// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;

namespace NarrativeTrace.Core;

/// <summary>
/// Resolves the on-disk layout for per-test trace artifacts —
/// <c>&lt;baseDir&gt;/traces/&lt;SimpleClassName&gt;/&lt;method-slug&gt;.md</c> — so every
/// test integration writes into the same structure as the Java reference.
/// </summary>
public sealed class OutputDirectoryResolver
{
    private static readonly Regex CamelBoundary =
        new(@"([a-z])([A-Z])", RegexOptions.Compiled);
    private static readonly Regex NonSlugChar =
        new(@"[^a-z0-9_]", RegexOptions.Compiled);

    private readonly string _baseDir;

    /// <summary>Creates a resolver rooted at a base directory.</summary>
    /// <param name="baseDir">
    /// The root every artifact path is built under. Taken as given — it is
    /// neither created, validated, nor made absolute here, so a relative path
    /// resolves against the working directory at write time.
    /// </param>
    public OutputDirectoryResolver(string baseDir)
    {
        _baseDir = baseDir;
    }

    /// <summary>The root directory, exactly as supplied to the constructor.</summary>
    public string BaseDir => _baseDir;

    /// <summary>
    /// The directory a class's traces live in: <c>traces/&lt;SimpleClassName&gt;</c>.
    /// A dotted class name is reduced to its final segment.
    /// </summary>
    public string TraceDirectory(string testClassName)
    {
        var dot = testClassName.LastIndexOf('.');
        var simpleName = dot >= 0
            ? testClassName.Substring(dot + 1)
            : testClassName;
        return Path.Combine(_baseDir, "traces", ToDirectorySlug(simpleName));
    }

    /// <summary>
    /// The Markdown trace file for a test: <c>traces/&lt;Class&gt;/&lt;slug&gt;.md</c>.
    /// </summary>
    public string TraceFile(string testClassName, string testMethodName)
    {
        return Path.Combine(
            TraceDirectory(testClassName), ToFileSlug(testMethodName) + ".md");
    }

    /// <summary>
    /// Slugs a method name the same way Java does: split camelCase with an
    /// underscore, lower-case, then replace every remaining non-slug character
    /// with an underscore.
    /// </summary>
    public static string ToFileSlug(string methodName)
    {
        var split = CamelBoundary.Replace(methodName, "$1_$2").ToLowerInvariant();
        return NonSlugChar.Replace(split, "_");
    }

    /// <summary>
    /// Everything a path cannot carry, replaced: separators (which would write
    /// outside the directory given to <see cref="OutputDirectoryResolver"/>)
    /// and control characters and lone surrogates (neither of which
    /// <see cref="Path.Combine(string, string)"/> can round-trip safely on
    /// every platform). Nothing else — a class name keeps its own spelling,
    /// Unicode letters included. Deliberately narrower than
    /// <see cref="ToFileSlug"/>: this only removes what a path cannot carry,
    /// so <c>OrderServiceTests</c> still resolves to <c>OrderServiceTests</c>
    /// and no existing artifact — or approved baseline beside it — moves.
    /// </summary>
    /// <param name="simpleName">The trace-directory segment, already reduced to its final dotted component.</param>
    /// <returns>The safe directory segment; <c>unnamed</c> when nothing legible survives.</returns>
    /// <remarks>
    /// A hostile <paramref name="simpleName"/> — a separator, a <c>..</c> traversal, or a value built
    /// from data the caller does not control — used to reach <see cref="Path.Combine(string, string, string)"/>
    /// unfiltered here, writing an artifact outside the directory it was given. This is that guard,
    /// mirroring the Java edition's <c>toDirectorySlug</c>.
    /// </remarks>
    public static string ToDirectorySlug(string simpleName)
    {
        var sb = new System.Text.StringBuilder(simpleName.Length);
        var index = 0;
        while (index < simpleName.Length)
        {
            index = AppendPathSafe(sb, simpleName, index);
        }

        var slug = sb.ToString();
        return slug.Length == 0 || slug.All(c => c == '.') ? "unnamed" : slug;
    }

    /// <summary>Appends one character, or one well-formed surrogate pair, replacing what a path cannot carry.</summary>
    /// <returns>The index to read from next.</returns>
    private static int AppendPathSafe(System.Text.StringBuilder sb, string name, int index)
    {
        var c = name[index];
        if (char.IsHighSurrogate(c) && index + 1 < name.Length && char.IsLowSurrogate(name[index + 1]))
        {
            sb.Append(c).Append(name[index + 1]);
            return index + 2;
        }

        sb.Append(IsPathSafe(c) ? c : '_');
        return index + 1;
    }

    /// <summary>
    /// A lone surrogate is refused for the same reason a control character is: it encodes to no
    /// well-formed bytes, so a filesystem call can raise or mis-encode before the file is ever
    /// written.
    /// </summary>
    private static bool IsPathSafe(char c) =>
        !char.IsControl(c) && !char.IsSurrogate(c)
        && c != '/' && c != '\\'
        && c != Path.DirectorySeparatorChar && c != Path.AltDirectorySeparatorChar;
}
