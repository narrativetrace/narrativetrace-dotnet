// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The provenance line decides, years later, whether a skill directory is ours. These cases pin
/// where it goes (after the frontmatter, never before), what counts as one (column 0, whole line),
/// and that stamping is idempotent. Method names mirror the Java <c>ProvenanceTest</c>.
/// </summary>
public class ProvenanceTest
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";

    private const string Page =
        "---\nname: narrativetrace-doctor\ndescription: d\n---\n\nBody\n";

    [Fact]
    public void ReadsBackTheCoordinateItWrote()
    {
        var stamped = Provenance.Stamp(Page, Coordinate);

        Assert.Equal(Coordinate, Provenance.CoordinateIn(stamped));
    }

    [Fact]
    public void PutsTheLineDirectlyAfterTheFrontmatterNeverBeforeIt()
    {
        var stamped = Provenance.Stamp(Page, Coordinate);

        Assert.Equal(
            "---\nname: narrativetrace-doctor\ndescription: d\n---\n"
            + Provenance.Line(Coordinate)
            + "\n\nBody\n",
            stamped);
        Assert.StartsWith("---\n", stamped, StringComparison.Ordinal);
    }

    [Fact]
    public void PutsTheLineAtTheTopOfAPageWithoutFrontmatter()
    {
        var stamped = Provenance.Stamp("Body only\n", Coordinate);

        Assert.Equal(Provenance.Line(Coordinate) + "\n\nBody only\n", stamped);
        Assert.Equal(Coordinate, Provenance.CoordinateIn(stamped));
    }

    [Fact]
    public void TreatsAnUnclosedFrontmatterFenceAsNoFrontmatter()
    {
        var stamped = Provenance.Stamp("---\nname: x\nbody\n", Coordinate);

        Assert.StartsWith(Provenance.Line(Coordinate), stamped, StringComparison.Ordinal);
    }

    [Fact]
    public void StampingTwiceIsStampingOnce()
    {
        var once = Provenance.Stamp(Page, Coordinate);

        Assert.Equal(once, Provenance.Stamp(once, Coordinate));
    }

    [Fact]
    public void RestampingReplacesAnOlderCoordinate()
    {
        var older = Provenance.Stamp(Page, "NarrativeTrace.Skills/0.0.9");

        var newer = Provenance.Stamp(older, Coordinate);

        Assert.Equal(Coordinate, Provenance.CoordinateIn(newer));
        Assert.DoesNotContain("0.0.9", newer, StringComparison.Ordinal);
        Assert.Equal(Provenance.Stamp(Page, Coordinate), newer);
    }

    [Fact]
    public void KeepsTheFilesLineEndings()
    {
        var stamped = Provenance.Stamp(Page.Replace("\n", "\r\n", StringComparison.Ordinal), Coordinate);

        Assert.DoesNotContain("\r\r", stamped, StringComparison.Ordinal);
        Assert.Contains("---\r\n" + Provenance.Line(Coordinate) + "\r\n", stamped, StringComparison.Ordinal);
        Assert.Equal(Coordinate, Provenance.CoordinateIn(stamped));
    }

    [Fact]
    public void IgnoresALineThatOnlyLooksLikeProvenance()
    {
        var indented = "---\nx\n---\n  " + Provenance.Line(Coordinate) + "\n";
        var truncated = "---\nx\n---\n" + Provenance.Prefix + Coordinate + "\n";
        var empty = "---\nx\n---\n" + Provenance.Prefix + Provenance.Suffix.Trim() + "\n";

        Assert.Null(Provenance.CoordinateIn(indented));
        Assert.Null(Provenance.CoordinateIn(truncated));
        Assert.Null(Provenance.CoordinateIn(empty));
        Assert.Null(Provenance.CoordinateIn(""));
    }

    /// <summary>Prefix and suffix meet with nothing between them: a provenance line naming no carrier.</summary>
    [Fact]
    public void ReadsNoCoordinateFromALineThatNamesNone()
    {
        var empty = Provenance.Prefix + Provenance.Suffix;

        Assert.Null(Provenance.CoordinateIn("---\nx\n---\n" + empty + "\n"));
        Assert.Equal(
            "---\nx\n---\n" + Provenance.Line(Coordinate) + "\n",
            Provenance.Stamp("---\nx\n---\n" + empty + "\n", Coordinate));
    }

    [Fact]
    public void StampsAnEmptyPage()
    {
        Assert.Equal(Provenance.Line(Coordinate) + "\n\n", Provenance.Stamp("", Coordinate));
    }

    /// <summary>
    /// A page somebody concatenated carries two provenance lines. The FIRST one wins, and that is the
    /// coordinate staleness is judged against — so a second, older line appended below cannot make a
    /// current install look stale, or an older one look current.
    /// </summary>
    [Fact]
    public void ReadsTheFirstCoordinateWhenAPageCarriesTwo()
    {
        const string second = "NarrativeTrace.Skills/0.0.1";
        var page =
            "---\nx\n---\n" + Provenance.Line(Coordinate) + "\nbody\n" + Provenance.Line(second) + "\n";

        Assert.Equal(Coordinate, Provenance.CoordinateIn(page));
        Assert.Equal(
            "---\nx\n---\n" + Provenance.Line(second) + "\nbody\n",
            Provenance.Stamp(page, second));
    }

    [Fact]
    public void FindsTheLineInAPageWhoseLastLineHasNoTerminator()
    {
        Assert.Equal(
            Coordinate,
            Provenance.CoordinateIn("---\nx\n---\n" + Provenance.Line(Coordinate)));
    }
}
