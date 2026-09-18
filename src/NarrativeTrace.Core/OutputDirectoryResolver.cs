// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Core;

/// <summary>
/// Resolves the on-disk layout for per-test trace artifacts —
/// <c>&lt;baseDir&gt;/traces/&lt;SimpleClassName&gt;/&lt;method-slug&gt;.md</c> — so every
/// test integration writes into the same structure as every NarrativeTrace runtime.
/// </summary>
public sealed class OutputDirectoryResolver
{
    /// <summary>
    /// The longest a single path element may be, in <b>bytes</b>.
    /// </summary>
    /// <remarks>
    /// 255 is what ext4, XFS, APFS and NTFS all allow, and the number is bytes rather than
    /// characters on every filesystem this library writes to except NTFS — which counts UTF-16
    /// units and is therefore never the tighter of the two for the names seen here. Counting
    /// characters would pass a 200-character CJK name and then fail the write at 600 bytes.
    /// Mirrors the java runtime's <c>OutputDirectoryResolver.MAX_COMPONENT_BYTES</c> — one scheme,
    /// java-defined, every port follows it.
    /// </remarks>
    private const int MaxComponentBytes = 255;

    /// <summary>
    /// Bytes held back from a file slug for the suffix a writer appends to it.
    /// </summary>
    /// <remarks>
    /// Java reserves 16 for its longest suffix, <c>.incomplete.nt</c> at 14 bytes; this runtime's
    /// own longest today is <c>.json</c> at 5, but the reserve is kept at the same value as the
    /// java runtime's rather than trimmed to what this port currently uses, so the two stay one
    /// scheme rather than two that happen to agree today.
    /// </remarks>
    private const int SuffixReserveBytes = 16;

    /// <summary>
    /// Bytes an invocation label may occupy inside an artifact name.
    /// </summary>
    /// <remarks>
    /// A display name is prose — a parameterized test's name template can
    /// interpolate arguments into it — so it is bounded before the method
    /// slug is, and the index it follows is never the part that gets
    /// truncated. 60 leaves a long name readable while keeping the whole
    /// element far below the component limit — the same bound every
    /// NarrativeTrace runtime uses.
    /// </remarks>
    private const int MaxLabelBytes = 60;

    /// <summary>
    /// Separates a method slug from its invocation discriminator.
    /// </summary>
    /// <remarks>
    /// The slug alphabet is <c>[a-z0-9_]</c>, so a hyphen can never appear
    /// inside either part: an ordinary method's artifact can never collide
    /// with an invocation's, and a reader (or a manifest consumer) can split
    /// the name back into method, index and label.
    /// </remarks>
    private const string InvocationSeparator = "-";

    private static readonly Regex CamelBoundary =
        new(@"([a-z])([A-Z])", RegexOptions.Compiled);
    private static readonly Regex NonSlugChar =
        new(@"[^a-z0-9_]", RegexOptions.Compiled);
    private static readonly Regex RunsOfUnderscore =
        new(@"_+", RegexOptions.Compiled);

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
        return ClassDirectory(Path.Combine(_baseDir, "traces"), testClassName);
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
    /// A per-test artifact in the <c>traces</c> tree, keyed by the full
    /// invocation identity: the rendered narrative in whichever format was
    /// chosen, and the JSON siblings written beside it.
    /// </summary>
    /// <param name="identity">Which test invocation the artifact belongs to.</param>
    /// <param name="suffix">The whole suffix including its dot, e.g. <c>.txt</c>, <c>.canonical.json</c>.</param>
    public string TraceArtifact(ArtifactIdentity identity, string suffix)
    {
        return Path.Combine(TraceDirectory(identity.TestClassName), identity.FileSlug() + suffix);
    }

    /// <summary>The Mermaid diagram of one invocation, in the <c>diagrams</c> tree.</summary>
    public string DiagramFile(ArtifactIdentity identity)
    {
        return Path.Combine(
            ClassDirectory(Path.Combine(_baseDir, "diagrams"), identity.TestClassName),
            identity.FileSlug() + ".mmd");
    }

    /// <summary>The last-green structural artifact of one invocation, in the <c>structural</c> tree.</summary>
    public string StructuralFile(ArtifactIdentity identity)
    {
        return Path.Combine(
            ClassDirectory(Path.Combine(_baseDir, "structural"), identity.TestClassName),
            identity.FileSlug() + ".nt");
    }

    /// <summary>
    /// The per-class directory of any artifact tree, under one sanitizing rule.
    /// </summary>
    /// <remarks>
    /// INTENT: <c>traces</c>, <c>diagrams</c>, <c>structural</c> and the
    /// committed-approval tree all key by test class, and each of them used
    /// to slice the simple name out itself. One rule, in one place, is what
    /// keeps the artifact trees from drifting apart. Mirrors the Java
    /// runtime's <c>classDirectory</c>.
    /// </remarks>
    /// <param name="root">The tree the artifact belongs to.</param>
    /// <param name="testClassName">Qualified or simple; never trusted to be either.</param>
    public static string ClassDirectory(string root, string testClassName)
    {
        var dot = testClassName.LastIndexOf('.');
        var simpleName = dot >= 0 ? testClassName[(dot + 1)..] : testClassName;
        return Path.Combine(root, ToDirectorySlug(simpleName));
    }

    /// <summary>
    /// Slugs a method name the same way Java does: split camelCase with an
    /// underscore, lower-case, then replace every remaining non-slug character
    /// with an underscore.
    /// </summary>
    public static string ToFileSlug(string methodName)
    {
        return Capped(RawFileSlug(methodName), MaxComponentBytes - SuffixReserveBytes);
    }

    /// <summary>
    /// The artifact base name for one invocation of a test method — the
    /// scheme <see cref="ArtifactIdentity"/> documents in full, and the one
    /// place it is computed.
    /// </summary>
    /// <remarks>
    /// An index of zero means "this method runs once", which is every
    /// ordinary test, and returns exactly what <see cref="ToFileSlug(string)"/>
    /// always returned: no existing artifact — or approved baseline beside it
    /// — moves. Otherwise the discriminator is appended and the
    /// <em>method</em> half absorbs any shortening, so the index a reader
    /// navigates by is never the part truncated away.
    /// </remarks>
    /// <param name="methodName">The test method's own name.</param>
    /// <param name="invocationIndex">1-based invocation number, or <c>0</c> for a method that runs once.</param>
    /// <param name="invocationLabel">The invocation's display name; may be blank.</param>
    internal static string ToFileSlug(string methodName, int invocationIndex, string invocationLabel)
    {
        if (invocationIndex <= 0)
        {
            return ToFileSlug(methodName);
        }

        var tail = InvocationTail(invocationIndex, invocationLabel);
        var budget = MaxComponentBytes - SuffixReserveBytes - Utf8Length(tail);
        return Capped(RawFileSlug(methodName), budget) + tail;
    }

    /// <summary><c>-002-find_tent</c>: the index a reader navigates by, then the label they recognize.</summary>
    private static string InvocationTail(int invocationIndex, string invocationLabel)
    {
        var index = InvocationSeparator + invocationIndex.ToString("D3", CultureInfo.InvariantCulture);
        var label = LabelSlug(invocationLabel);
        return label.Length == 0 ? index : index + InvocationSeparator + label;
    }

    /// <summary>
    /// A display name reduced to a readable name fragment: the shared slug
    /// rule, then runs of <c>_</c> collapsed and the ends trimmed. A label
    /// that slugs to nothing is dropped entirely — the index alone still
    /// names the invocation.
    /// </summary>
    private static string LabelSlug(string invocationLabel)
    {
        var collapsed = RunsOfUnderscore.Replace(RawFileSlug(invocationLabel), "_");
        var trimmed = collapsed.Trim('_');
        return Capped(trimmed, MaxLabelBytes);
    }

    /// <summary>The uncapped slug: camel-case split, lowercased, everything outside the alphabet replaced.</summary>
    private static string RawFileSlug(string name)
    {
        var split = CamelBoundary.Replace(name, "$1_$2").ToLowerInvariant();
        return NonSlugChar.Replace(split, "_");
    }

    /// <summary>
    /// Everything a path cannot carry, replaced: separators (which would write
    /// outside the directory given to <see cref="OutputDirectoryResolver"/>)
    /// and control characters and lone surrogates (neither of which
    /// <see cref="Path.Combine(string, string)"/> can round-trip safely on
    /// every platform). Nothing else — a class name keeps its own spelling,
    /// Unicode letters included. Deliberately narrower than
    /// <see cref="ToFileSlug(string)"/>: this only removes what a path cannot carry,
    /// so <c>OrderServiceTests</c> still resolves to <c>OrderServiceTests</c>
    /// and no existing artifact — or approved baseline beside it — moves.
    /// </summary>
    /// <param name="simpleName">The trace-directory segment, already reduced to its final dotted component.</param>
    /// <returns>The safe directory segment; <c>unnamed</c> when nothing legible survives.</returns>
    /// <remarks>
    /// A hostile <paramref name="simpleName"/> — a separator, a <c>..</c> traversal, or a value built
    /// from data the caller does not control — used to reach <see cref="Path.Combine(string, string, string)"/>
    /// unfiltered here, writing an artifact outside the directory it was given. This is that guard.
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
        var named = slug.Length == 0 || slug.All(c => c == '.') ? "unnamed" : slug;
        return Capped(named, MaxComponentBytes);
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

    /// <summary>
    /// The slug, shortened to fit a path element when it does not.
    /// </summary>
    /// <remarks>
    /// A name longer than the filesystem allows made the writers throw
    /// <see cref="IOException"/> ("File name too long" / <c>ENAMETOOLONG</c>) — an observability
    /// failure becoming an application failure, which this library does not do. A name is data
    /// here, not an identifier: <see cref="OutputDirectoryResolver"/> is reachable through public
    /// writer APIs whose callers include integrations that pass a scenario name or an HTTP route
    /// rather than a reflected type name.
    /// <para>
    /// The truncated form keeps eight hex characters of the full slug's hash, because truncation
    /// alone is a silent overwrite: two long names sharing a prefix would land on one artifact, and
    /// one scenario's approved baseline would then judge another's trace. The hash is Java's own
    /// specified <c>String.hashCode()</c> formula (<c>h = 31*h + c</c> over UTF-16 code units),
    /// reimplemented here rather than <see cref="string.GetHashCode()"/> — .NET randomizes that
    /// per process for hash-flooding resistance, which would make the same over-long name resolve
    /// to a <em>different</em> truncated artifact on every run, exactly the instability an approved
    /// baseline cannot tolerate. Java's formula has no such randomization and is specified to give
    /// the same result forever, so mirroring it (rather than inventing a different stable hash) is
    /// what makes the disambiguator actually stable, and keeps one hashing scheme rather than one
    /// per port.
    /// </para>
    /// <para>
    /// Nothing under the limit is touched, so no existing artifact — or approved baseline beside it
    /// — moves.
    /// </para>
    /// </remarks>
    private static string Capped(string slug, int maxBytes)
    {
        if (Utf8Length(slug) <= maxBytes)
        {
            return slug;
        }

        var suffix = "_" + JavaStringHashCode(slug).ToString("x8", CultureInfo.InvariantCulture);
        return TruncateToBytes(slug, maxBytes - suffix.Length) + suffix;
    }

    /// <summary>
    /// Java's <c>String.hashCode()</c>, specified as <c>s[0]*31^(n-1) + s[1]*31^(n-2) + ... +
    /// s[n-1]</c> over UTF-16 code units — the same code units <c>foreach (var c in value)</c>
    /// walks here. 32-bit overflow wraps silently in both languages, so this produces the exact
    /// value the java runtime's own disambiguator would for the same string.
    /// </summary>
    private static int JavaStringHashCode(string value)
    {
        var hash = 0;
        foreach (var c in value)
        {
            hash = (31 * hash) + c;
        }

        return hash;
    }

    private static int Utf8Length(string value) => Encoding.UTF8.GetByteCount(value);

    /// <summary>
    /// The longest prefix of <paramref name="value"/> that encodes to at most
    /// <paramref name="maxBytes"/>, cut on a character boundary so a surrogate pair is never split
    /// in half.
    /// </summary>
    private static string TruncateToBytes(string value, int maxBytes)
    {
        var bytes = 0;
        var index = 0;
        while (index < value.Length)
        {
            var isPair = char.IsHighSurrogate(value[index])
                && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1]);
            var codePoint = isPair ? char.ConvertToUtf32(value[index], value[index + 1]) : value[index];
            var width = Utf8Width(codePoint);
            if (bytes + width > maxBytes)
            {
                break;
            }

            bytes += width;
            index += isPair ? 2 : 1;
        }

        return value[..index];
    }

    /// <summary>
    /// UTF-8 bytes one code point costs. A lone surrogate reaches the three-byte branch and
    /// actually encodes to fewer (.NET's default UTF-8 encoder replaces it with a single
    /// substitute byte), so the count is an over-estimate there — which shortens the name rather
    /// than overflowing the element, the direction an estimate here has to err in.
    /// </summary>
    private static int Utf8Width(int codePoint)
    {
        if (codePoint < 0x80)
        {
            return 1;
        }

        if (codePoint < 0x800)
        {
            return 2;
        }

        return codePoint < 0x10000 ? 3 : 4;
    }
}
