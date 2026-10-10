// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The rule that decides whether a page nobody stamped is ours after all — and, just as important,
/// the near misses it must NOT accept. Every row here is a page a registry, a person or another
/// release could really leave in a project. Method names mirror the Java <c>AdoptionTest</c>.
/// </summary>
public sealed class AdoptionTest
{
    private const string Page = "---\nname: doctor\ndescription: What doctor does.\n---\n\nbody\n";

    [Fact]
    public void AcceptsThePageTheCarrierRenders()
    {
        Assert.True(Adoption.IsAdoptable(Page, Page));
    }

    /// <summary>A checkout with the other line ending is the same page: that is the one thing normalised.</summary>
    [Fact]
    public void AcceptsThePageWithTheOtherLineEnding()
    {
        Assert.True(Adoption.IsAdoptable(Page.Replace("\n", "\r\n", StringComparison.Ordinal), Page));
        Assert.True(Adoption.IsAdoptable(Page.Replace('\n', '\r'), Page));
    }

    [Fact]
    public void RefusesAPageWithOneTrailingSpace()
    {
        Assert.False(Adoption.IsAdoptable(Page.Replace("body", "body ", StringComparison.Ordinal), Page));
    }

    [Fact]
    public void RefusesAPageWhoseFrontmatterKeysWereReordered()
    {
        var reordered = "---\ndescription: What doctor does.\nname: doctor\n---\n\nbody\n";

        Assert.False(Adoption.IsAdoptable(reordered, Page));
    }

    [Fact]
    public void RefusesAPageThatLostItsFinalNewline()
    {
        Assert.False(Adoption.IsAdoptable(Page.TrimEnd(), Page));
    }

    [Fact]
    public void RefusesAnotherReleasesWording()
    {
        Assert.False(Adoption.IsAdoptable(Page.Replace("body", "older body", StringComparison.Ordinal), Page));
    }

    /// <summary>A skill directory with no page at all is somebody's empty directory, never a copy of ours.</summary>
    [Fact]
    public void RefusesAnEmptyPageEvenAgainstAnEmptyRendering()
    {
        Assert.False(Adoption.IsAdoptable("", ""));
        Assert.False(Adoption.IsAdoptable("", Page));
    }

    [Fact]
    public void RefusesToCompareWithNothing()
    {
        Assert.Throws<ArgumentException>(() => Adoption.IsAdoptable(null!, Page));
        Assert.Throws<ArgumentException>(() => Adoption.IsAdoptable(Page, null!));
    }
}
