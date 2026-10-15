// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>The installer's own reading of each detected platform's pointer — what the doctor judges.</summary>
public sealed class PointerReadingTest
{
    private const string Coordinate = "NarrativeTrace.Skills/1.2.3";
    private const string RulePath = ".cursor/rules/narrativetrace.mdc";

    [Fact]
    public void ReadsNothingForAProjectOnNoPlatformThatNeedsAPointer()
    {
        var state = ProjectState.NewBuilder().DetectedPlatform("antigravity").DetectedPlatform("claude-code").Build();

        Assert.Empty(PointerReading.ReadAll(state));
    }

    [Fact]
    public void ADetectedPlatformWhosePointerIsMissingReadsAsAbsent()
    {
        var reading = Assert.Single(PointerReading.ReadAll(ProjectState.NewBuilder().DetectedPlatform("cursor").Build()));

        Assert.Equal("cursor", reading.PlatformId);
        Assert.Equal("Cursor", reading.PlatformName);
        Assert.Equal(RulePath, reading.Path);
        Assert.Equal(PointerReading.PointerStatus.Absent, reading.Status);
    }

    [Fact]
    public void AStampedSectionReadsWithItsCoordinate()
    {
        var state = ProjectState.NewBuilder().DetectedPlatform("cursor")
            .PointerFile(RulePath, CursorRule.Render("t", Coordinate)).Build();

        var reading = PointerReading.ReadAll(state)[0];

        Assert.Equal(PointerReading.PointerStatus.Section, reading.Status);
        Assert.Equal(Coordinate, reading.Coordinate);
    }

    [Fact]
    public void AnUnstampedSectionReadsAsASectionWithNoCoordinate()
    {
        var state = ProjectState.NewBuilder().DetectedPlatform("cursor")
            .PointerFile(RulePath, CursorRule.Render("t", "")).Build();

        var reading = PointerReading.ReadAll(state)[0];

        Assert.Equal(PointerReading.PointerStatus.Section, reading.Status);
        Assert.Equal("", reading.Coordinate);
    }

    [Fact]
    public void AFileWithoutExactlyOneSectionReadsAsForeign()
    {
        var none = ProjectState.NewBuilder().DetectedPlatform("replit").PointerFile("replit.md", "# mine\n").Build();
        var two = ProjectState.NewBuilder().DetectedPlatform("replit").PointerFile(
            "replit.md",
            "<!-- narrativetrace:start -->\n<!-- narrativetrace:end -->\n<!-- narrativetrace:start -->\n<!-- narrativetrace:end -->\n")
            .Build();
        var unclosed = ProjectState.NewBuilder().DetectedPlatform("replit")
            .PointerFile("replit.md", "<!-- narrativetrace:start -->\n").Build();

        Assert.Equal(PointerReading.PointerStatus.Foreign, PointerReading.ReadAll(none)[0].Status);
        Assert.Equal(PointerReading.PointerStatus.Foreign, PointerReading.ReadAll(two)[0].Status);
        Assert.Equal(PointerReading.PointerStatus.Foreign, PointerReading.ReadAll(unclosed)[0].Status);
    }

    [Fact]
    public void APointerBehindALinkReadsAsLinkedWhateverElseIsKnown()
    {
        var state = ProjectState.NewBuilder().DetectedPlatform("replit").PointerBehindLink("replit.md").Build();

        Assert.Equal(PointerReading.PointerStatus.Linked, PointerReading.ReadAll(state)[0].Status);
    }

    [Fact]
    public void ReadsOneReadingPerDetectedPlatformInTableOrder()
    {
        var state = ProjectState.NewBuilder().DetectedPlatform("replit").DetectedPlatform("cursor").Build();

        Assert.Equal(["cursor", "replit"], PointerReading.ReadAll(state).Select(reading => reading.PlatformId));
    }

    [Fact]
    public void ReadingNeedsAProjectState()
    {
        Assert.Throws<ArgumentException>(() => PointerReading.ReadAll(null!));
    }
}
