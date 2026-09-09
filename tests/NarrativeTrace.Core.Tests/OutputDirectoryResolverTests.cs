// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class OutputDirectoryResolverTests
{
    private const int MaxComponentBytes = 255;
    [Fact]
    public void Trace_directory_uses_the_simple_class_name_under_traces()
    {
        var resolver = new OutputDirectoryResolver("out");

        Assert.Equal(
            Path.Combine("out", "traces", "BarTests"),
            resolver.TraceDirectory("Foo.Bar.BarTests"));
    }

    [Fact]
    public void Trace_directory_keeps_an_unqualified_class_name_as_is()
    {
        var resolver = new OutputDirectoryResolver("out");

        Assert.Equal(
            Path.Combine("out", "traces", "BarTests"),
            resolver.TraceDirectory("BarTests"));
    }

    [Theory]
    [InlineData("PlacesOrder", "places_order")]
    [InlineData("placesOrder", "places_order")]
    [InlineData("Places order", "places_order")]
    [InlineData("Handles-Order", "handles_order")]
    public void Trace_file_slugs_the_method_name(string method, string slug)
    {
        var resolver = new OutputDirectoryResolver("out");

        Assert.Equal(
            Path.Combine("out", "traces", "BarTests", slug + ".md"),
            resolver.TraceFile("BarTests", method));
    }

    /// <summary>
    /// Regression: the method name has always been slugged and the class name never was, so it
    /// reached <see cref="Path.Combine(string, string, string)"/> verbatim. A separator wrote
    /// outside the directory the caller gave. Found by the security suite's hostile corpus; the
    /// same defect class is pinned in every NarrativeTrace runtime — a class name carrying a
    /// separator can no longer escape the base directory.
    /// </summary>
    [Fact]
    public void A_class_name_carrying_a_separator_cannot_escape_the_base_directory()
    {
        var baseDir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "narrativetrace-base"));
        var resolver = new OutputDirectoryResolver(baseDir);

        var file = resolver.TraceFile("../../etc", "passwd");

        var normalized = Path.GetFullPath(file);
        Assert.StartsWith(Path.Combine(baseDir, "traces") + Path.DirectorySeparatorChar, normalized, StringComparison.Ordinal);
    }

    [Fact]
    public void A_class_name_carrying_a_control_character_resolves_instead_of_throwing()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile("com.example.A" + (char)0 + "B", "runs");

        Assert.Equal(Path.Combine("out", "traces", "A_B", "runs.md"), file);
    }

    [Fact]
    public void A_class_name_carrying_a_lone_surrogate_resolves_instead_of_throwing()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile("com.example.A" + '\uD800' + "B", "runs");

        Assert.Equal(Path.Combine("out", "traces", "A_B", "runs.md"), file);
    }

    [Fact]
    public void A_class_name_carrying_a_well_formed_surrogate_pair_keeps_it()
    {
        var resolver = new OutputDirectoryResolver("out");
        const string monkey = "🙈";

        var file = resolver.TraceFile("com.example.A" + monkey, "runs");

        Assert.Equal(Path.Combine("out", "traces", "A" + monkey, "runs.md"), file);
    }

    [Fact]
    public void A_class_name_with_unicode_letters_keeps_them_too()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile("com.example.ÜberTests", "runs");

        Assert.Equal(Path.Combine("out", "traces", "ÜberTests", "runs.md"), file);
    }

    [Fact]
    public void A_class_name_that_sanitizes_away_entirely_gets_a_name_rather_than_none()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile("com.example.", "runs");

        Assert.Equal(Path.Combine("out", "traces", "unnamed", "runs.md"), file);
    }

    /// <summary>
    /// Reachable only by calling the slug function directly: <see cref="OutputDirectoryResolver.TraceDirectory"/>
    /// always strips at the last dot first, so a dotted class name can never hand the slug a
    /// dots-only string. A caller with no dot at all (<c>".."</c> itself) can.
    /// </summary>
    [Fact]
    public void A_dots_only_segment_gets_a_name_rather_than_none()
    {
        Assert.Equal("unnamed", OutputDirectoryResolver.ToDirectorySlug(".."));
    }

    /// <summary>
    /// Regression: a name longer than a path element made every writer throw an
    /// <see cref="IOException"/> ("File name too long"), which is an observability failure
    /// becoming an application failure. Found by the security suite's hostile corpus; mirrors the
    /// java runtime's cap.
    /// </summary>
    [Fact]
    public void An_over_long_class_name_is_shortened_to_fit_a_path_element()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile(new string('a', 300), "runs");
        var directoryName = Path.GetFileName(Path.GetDirectoryName(file));

        Assert.Equal(MaxComponentBytes, ByteLength(directoryName));
    }

    /// <summary>The file half has to leave room for the longest suffix a writer appends.</summary>
    [Fact]
    public void An_over_long_method_name_leaves_room_for_the_longest_artifact_suffix()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile("Test", new string('a', 300));
        var fileName = Path.GetFileName(file);

        Assert.EndsWith(".md", fileName, StringComparison.Ordinal);
        Assert.True(
            ByteLength(fileName) + ".incomplete.nt".Length - ".md".Length <= MaxComponentBytes);
    }

    /// <summary>
    /// A name that is short in characters and long in bytes: 200 three-byte characters are 600
    /// bytes, and a cap that counted characters would pass this and then fail the write.
    /// </summary>
    [Fact]
    public void A_multibyte_class_name_is_measured_in_bytes_rather_than_characters()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile(new string('中', 200), "runs");
        var directoryName = Path.GetFileName(Path.GetDirectoryName(file));

        Assert.True(ByteLength(directoryName) <= MaxComponentBytes);
    }

    /// <summary>Truncation must not cut a surrogate pair in half and leave an unencodable path element.</summary>
    [Fact]
    public void An_over_long_name_of_surrogate_pairs_is_cut_on_a_character_boundary()
    {
        var resolver = new OutputDirectoryResolver("out");
        const string monkey = "🙈";

        var file = resolver.TraceFile(string.Concat(Enumerable.Repeat(monkey, 200)), "runs");
        var directoryName = Path.GetFileName(Path.GetDirectoryName(file))!;

        Assert.False(
            HasUnpairedSurrogate(directoryName),
            "half a surrogate pair encodes to no bytes, so the path would not be writable");
        Assert.True(ByteLength(directoryName) <= MaxComponentBytes);
    }

    /// <summary>
    /// Truncation alone is a silent overwrite: two long names sharing a prefix would land on one
    /// artifact, so one scenario's approved baseline would judge another scenario's trace.
    /// </summary>
    [Fact]
    public void Two_over_long_names_differing_only_past_the_limit_stay_apart()
    {
        var resolver = new OutputDirectoryResolver("out");
        var prefix = new string('a', 300);

        var first = resolver.TraceFile(prefix + "one", "runs");
        var second = resolver.TraceFile(prefix + "two", "runs");

        Assert.NotEqual(first, second);
    }

    /// <summary>The shortening is a function of the name, so an artifact keeps its place between runs.</summary>
    [Fact]
    public void Shortening_the_same_name_twice_gives_the_same_path()
    {
        var resolver = new OutputDirectoryResolver("out");

        Assert.Equal(
            resolver.TraceFile(new string('b', 400), new string('c', 400)),
            resolver.TraceFile(new string('b', 400), new string('c', 400)));
    }

    /// <summary>A name that already fits is untouched, so no existing artifact moves.</summary>
    [Fact]
    public void A_name_that_fits_is_not_shortened()
    {
        var resolver = new OutputDirectoryResolver("out");

        var file = resolver.TraceFile(new string('a', 255), new string('b', 239));

        Assert.Equal(new string('a', 255), Path.GetFileName(Path.GetDirectoryName(file)));
        Assert.Equal(new string('b', 239) + ".md", Path.GetFileName(file));
    }

    /// <summary>
    /// Every width the UTF-8 counter distinguishes, taken at its boundary: the last two-byte code
    /// point and the first three-byte one, the last three-byte one and the first four-byte one.
    /// </summary>
    /// <remarks>
    /// The assertion is a range, not an upper bound, on purpose. An upper bound alone passes for a
    /// counter that over-estimates every character and truncates to nothing; the lower bound
    /// (three bytes of slack, the most a four-byte character can waste) says the element is also as
    /// long as it is allowed to be. U+0080 itself cannot be tested here — it is a C1 control, so
    /// the path rule replaces it with an underscore before the counter sees it.
    /// </remarks>
    [Theory]
    [InlineData(0x00e9)]
    [InlineData(0x07ff)]
    [InlineData(0x0800)]
    [InlineData(0xffff)]
    [InlineData(0x10000)]
    public void An_over_long_name_of_any_character_width_fills_its_path_element_without_overflowing(int codePoint)
    {
        var resolver = new OutputDirectoryResolver("out");
        var unit = char.ConvertFromUtf32(codePoint);
        var name = string.Concat(Enumerable.Repeat(unit, 300));

        var file = resolver.TraceFile(name, "runs");
        var directoryName = Path.GetFileName(Path.GetDirectoryName(file));

        var length = ByteLength(directoryName);
        Assert.True(
            length is >= (MaxComponentBytes - 3) and <= MaxComponentBytes,
            $"U+{codePoint:X4} repeated: expected {MaxComponentBytes - 3}..{MaxComponentBytes}, got {length}");
    }

    private static int ByteLength(string? component) => Encoding.UTF8.GetByteCount(component ?? "");

    /// <summary>
    /// Whether <paramref name="value"/> carries a surrogate half with no partner — the char-level
    /// equivalent of java's code-point check (a well-formed pair merges into one code point outside
    /// the surrogate range; an orphaned half does not).
    /// </summary>
    private static bool HasUnpairedSurrogate(string value)
    {
        var i = 0;
        while (i < value.Length)
        {
            if (char.IsHighSurrogate(value[i]))
            {
                if (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1]))
                {
                    return true;
                }

                i += 2;
                continue;
            }

            if (char.IsLowSurrogate(value[i]))
            {
                return true;
            }

            i++;
        }

        return false;
    }
}
