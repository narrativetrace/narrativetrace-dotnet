// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The construction guards on the two actions milestone 2 added — <see cref="AdoptPage"/> and
/// <see cref="ReplaceLink"/>. A plan is data that can come from anywhere, so each guard is the line
/// between a hand-built plan and a nonsense write; these pin that every one of them trips.
/// </summary>
public sealed class ActionTest
{
    [Fact]
    public void AdoptPageRefusesAnEmptyPageToAdopt()
    {
        Assert.Contains("nothing to adopt",
            Assert.Throws<ArgumentException>(
                () => new AdoptPage(".agents/skills/doctor/SKILL.md", "", "stamped\n")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceLinkRefusesABlankTarget()
    {
        Assert.Contains("names what it pointed at",
            Assert.Throws<ArgumentException>(
                () => new ReplaceLink(".claude/skills/doctor", ".claude/skills/doctor/SKILL.md", "   ", "page\n")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceLinkRefusesAnEmptyPage()
    {
        Assert.Contains("never by an empty file",
            Assert.Throws<ArgumentException>(
                () => new ReplaceLink(".claude/skills/doctor", ".claude/skills/doctor/SKILL.md", "../x", "")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReplaceLinkRefusesAPageThatIsNotBehindTheLink()
    {
        Assert.Contains("not behind the link",
            Assert.Throws<ArgumentException>(
                () => new ReplaceLink(".claude/skills/doctor", ".agents/skills/doctor/SKILL.md", "../x", "page\n")).Message,
            StringComparison.Ordinal);
    }

    /// <summary>A sibling that merely shares the link's name prefix is not behind it — the guard needs a delimiter.</summary>
    [Fact]
    public void ReplaceLinkRefusesASiblingThatSharesTheLinksNamePrefix()
    {
        Assert.Throws<ArgumentException>(
            () => new ReplaceLink(".claude/skills/doc", ".claude/skills/doctor/SKILL.md", "../x", "page\n"));
    }
}
