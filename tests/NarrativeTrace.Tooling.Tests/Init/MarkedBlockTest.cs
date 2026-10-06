// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The managed block is found by its markers, and everything the installer does to a consumer file
/// is expressed as an edit of that region. These cases pin the marker rule itself — column 0,
/// outside fenced code — and the reversibility every <c>init</c> / <c>uninstall</c> round trip
/// depends on. Method names mirror the Java <c>MarkedBlockTest</c> so the two lists diff.
/// </summary>
public class MarkedBlockTest
{
    private const string Block =
        "<!-- narrativetrace:start NarrativeTrace.Skills/1.2.3 -->\n"
        + "## NarrativeTrace\n"
        + "<!-- narrativetrace:end -->\n";

    // --- finding the region -------------------------------------------------------------------

    [Fact]
    public void FindsOneRegionAndReadsItsCoordinate()
    {
        var text = "# Title\n\n" + Block;

        var scan = MarkedBlock.ScanText(text);

        Assert.Empty(scan.Problems);
        Assert.Single(scan.Regions);
        Assert.Equal("NarrativeTrace.Skills/1.2.3", scan.Regions[0].Coordinate);
        Assert.Equal(Block, text[scan.Regions[0].Start..scan.Regions[0].End]);
    }

    [Fact]
    public void FindsARegionWhoseOpeningMarkerCarriesNoCoordinate()
    {
        var scan = MarkedBlock.ScanText(
            "<!-- narrativetrace:start -->\nbody\n<!-- narrativetrace:end -->\n");

        Assert.Single(scan.Regions);
        Assert.Equal("", scan.Regions[0].Coordinate);
    }

    [Fact]
    public void ReadsNoCoordinateFromAnOpeningMarkerThatNeverCloses()
    {
        var scan = MarkedBlock.ScanText(
            "<!-- narrativetrace:start oops\nbody\n<!-- narrativetrace:end -->\n");

        Assert.Single(scan.Regions);
        Assert.Equal("", scan.Regions[0].Coordinate);
    }

    [Fact]
    public void FindsTwoRegionsSoTheCallerCanRefuseThem()
    {
        var scan = MarkedBlock.ScanText(Block + "\ntext\n\n" + Block);

        Assert.Equal(2, scan.Regions.Count);
        Assert.Empty(scan.Problems);
    }

    [Fact]
    public void ReportsAStartWithNoEndNamingTheLine()
    {
        var scan = MarkedBlock.ScanText("a\n<!-- narrativetrace:start -->\nb\n");

        Assert.Empty(scan.Regions);
        var problem = Assert.Single(scan.Problems);
        Assert.Contains("line 2", problem, StringComparison.Ordinal);
        Assert.Contains("no end", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAnEndWithNoStartNamingTheLine()
    {
        var scan = MarkedBlock.ScanText("a\n<!-- narrativetrace:end -->\n");

        var problem = Assert.Single(scan.Problems);
        Assert.Contains("line 2", problem, StringComparison.Ordinal);
        Assert.Contains("no start", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsASecondStartBeforeTheFirstEnd()
    {
        var scan = MarkedBlock.ScanText(
            "<!-- narrativetrace:start -->\n<!-- narrativetrace:start -->\n"
            + "<!-- narrativetrace:end -->\n");

        var problem = Assert.Single(scan.Problems);
        Assert.Contains("line 2", problem, StringComparison.Ordinal);
    }

    // --- near misses: what is NOT a marker -----------------------------------------------------

    [Fact]
    public void IgnoresAMarkerInsideAFencedCodeBlock()
    {
        var text = "# Docs\n\n```\n" + Block + "```\n";

        var scan = MarkedBlock.ScanText(text);

        Assert.Empty(scan.Regions);
        Assert.Empty(scan.Problems);
    }

    [Fact]
    public void IgnoresAMarkerInsideATildeFencedCodeBlock()
    {
        Assert.Empty(MarkedBlock.ScanText("~~~\n" + Block + "~~~\n").Regions);
    }

    [Fact]
    public void SeesAMarkerAgainAfterTheFenceCloses()
    {
        var text = "```\n<!-- narrativetrace:start -->\n```\n" + Block;

        Assert.Single(MarkedBlock.ScanText(text).Regions);
    }

    [Fact]
    public void IgnoresAnIndentedMarker()
    {
        var text = "  <!-- narrativetrace:start -->\n  <!-- narrativetrace:end -->\n";

        Assert.Empty(MarkedBlock.ScanText(text).Regions);
        Assert.Empty(MarkedBlock.ScanText(text).Problems);
    }

    [Fact]
    public void IgnoresThisRepositorysOwnSkillsMarkers()
    {
        var text = "<!-- narrativetrace:skills:start -->\nx\n<!-- narrativetrace:skills:end -->\n";

        Assert.Empty(MarkedBlock.ScanText(text).Regions);
        Assert.Empty(MarkedBlock.ScanText(text).Problems);
    }

    [Fact]
    public void FindsAMarkerOnTheFirstLineBehindAByteOrderMark()
    {
        var scan = MarkedBlock.ScanText("﻿" + Block);

        Assert.Single(scan.Regions);
    }

    [Fact]
    public void FindsARegionWhoseEndMarkerIsTheLastLineWithoutATrailingNewline()
    {
        var text = "<!-- narrativetrace:start -->\nbody\n<!-- narrativetrace:end -->";

        var scan = MarkedBlock.ScanText(text);

        Assert.Single(scan.Regions);
        Assert.Equal(text, text[scan.Regions[0].Start..scan.Regions[0].End]);
    }

    // --- editing ------------------------------------------------------------------------------

    [Fact]
    public void ReplacesOnlyTheRegionAndLeavesTheRestByteForByte()
    {
        var text = "# Title\n\n" + Block + "\ntail\n";
        var region = MarkedBlock.ScanText(text).Regions[0];

        var replaced = MarkedBlock.Replace(text, region, "<!-- new -->\n");

        Assert.Equal("# Title\n\n<!-- new -->\n\ntail\n", replaced);
    }

    [Fact]
    public void ReplacePreservesWindowsLineEndings()
    {
        var text = "# Title\r\n\r\n" + Block.Replace("\n", "\r\n", StringComparison.Ordinal);
        var region = MarkedBlock.ScanText(text).Regions[0];

        var replaced = MarkedBlock.Replace(text, region, MarkedBlock.WithEol("x\n", "\r\n"));

        Assert.Equal("# Title\r\n\r\nx\r\n", replaced);
    }

    [Fact]
    public void AppendsAfterExactlyOneBlankLine()
    {
        Assert.Equal("# Title\n\n" + Block, MarkedBlock.Append("# Title\n", Block));
    }

    [Fact]
    public void AppendsToAFileWithoutATrailingNewlineByAddingOne()
    {
        Assert.Equal("# Title\n\n" + Block, MarkedBlock.Append("# Title", Block));
    }

    [Fact]
    public void AppendsToAnEmptyFileWithoutALeadingBlankLine()
    {
        Assert.Equal(Block, MarkedBlock.Append("", Block));
    }

    [Fact]
    public void AppendsWithTheFilesOwnLineEnding()
    {
        var crlf = Block.Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.Equal("# Title\r\n\r\n" + crlf, MarkedBlock.Append("# Title\r\n", crlf));
    }

    [Fact]
    public void RemovingWhatWasAppendedRestoresTheFileByteForByte()
    {
        foreach (var original in new[] { "# Title\n", "a\n\n\n", "x\r\n", "# T\r\n\r\n", "" })
        {
            var block = MarkedBlock.WithEol(Block, MarkedBlock.EolOf(original));
            var appended = MarkedBlock.Append(original, block);

            var region = MarkedBlock.ScanText(appended).Regions[0];

            Assert.Equal(original, MarkedBlock.Remove(appended, region));
        }
    }

    [Fact]
    public void RemovingABlockAtTheStartOfTheFileLeavesTheRest()
    {
        var text = Block + "tail\n";
        var region = MarkedBlock.ScanText(text).Regions[0];

        Assert.Equal("tail\n", MarkedBlock.Remove(text, region));
    }

    // --- unfinished fences ----------------------------------------------------------------------

    [Fact]
    public void KnowsWhenAFileEndsInsideAFenceThatWasNeverClosed()
    {
        Assert.True(MarkedBlock.EndsInsideFence("# Title\n\n```\ncode\n"));
        Assert.True(MarkedBlock.EndsInsideFence("~~~\ncode\n"));
        Assert.False(MarkedBlock.EndsInsideFence("```\ncode\n```\n"));
        Assert.True(MarkedBlock.EndsInsideFence("```\na\n```\ntext\n```\nb\n"));
        Assert.False(MarkedBlock.EndsInsideFence("# Title\n"));
        Assert.False(MarkedBlock.EndsInsideFence(""));
        Assert.False(MarkedBlock.EndsInsideFence("  ```\nindented, not a fence\n"));
    }

    // --- line endings -------------------------------------------------------------------------

    [Fact]
    public void ReadsTheFilesLineEndingFromItsFirstTerminator()
    {
        Assert.Equal("\r\n", MarkedBlock.EolOf("a\r\nb\n"));
        Assert.Equal("\n", MarkedBlock.EolOf("a\nb\r\n"));
        Assert.Equal("\n", MarkedBlock.EolOf("no terminator"));
        Assert.Equal("\n", MarkedBlock.EolOf(""));
    }

    [Fact]
    public void ReadsTheLineEndingOfAFileThatStartsWithABlankLine()
    {
        Assert.Equal("\n", MarkedBlock.EolOf("\nx"));
        Assert.Equal("\r\n", MarkedBlock.EolOf("\r\nx"));
    }

    [Fact]
    public void TreatsALoneCarriageReturnAsAFinishedLine()
    {
        Assert.Equal("x\r\n" + Block, MarkedBlock.Append("x\r", Block));
    }

    [Fact]
    public void RewritesABlocksLineEndings()
    {
        Assert.Equal("a\r\nb\r\n", MarkedBlock.WithEol("a\nb\n", "\r\n"));
        Assert.Equal("a\nb\n", MarkedBlock.WithEol("a\r\nb\r\n", "\n"));
    }
}
