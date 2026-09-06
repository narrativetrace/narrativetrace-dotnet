// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class OutputDirectoryResolverTests
{
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
    /// outside the directory the caller gave. Found by the security suite's hostile corpus, fixed
    /// on the Java side, and fixed here too — a class name carrying a separator can no longer
    /// escape the base directory.
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
}
