// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The carrier index is read by hand, so the reader is tested against hand-written catalogues, well-
/// formed and malformed: a malformed carrier is refused before any work, naming the entry. Method
/// names mirror the Java <c>CatalogueReaderTest</c>.
/// </summary>
/// <remarks>
/// Java's <c>readsTheRealCheckedInCatalogue</c> is not ported here: this repo carries no rendered
/// <c>catalogue.json</c> yet — the embedded-resource carrier is Phase 3 milestone 1, which has not
/// landed. That case lands with the carrier.
/// </remarks>
public class CatalogueReaderTest
{
    private static string Catalogue(string skills) =>
        "{\"runtime\": \"dotnet\", \"skills\": [" + skills + "]}";

    private const string Doctor =
        "{\"name\": \"narrativetrace-doctor\", \"description\": \"Diagnoses an install.\","
        + " \"agents\": \"agents/narrativetrace-doctor/SKILL.md\","
        + " \"claude\": \"claude/narrativetrace-doctor/SKILL.md\"}";

    [Fact]
    public void ReadsTheRuntimeAndOneSkillFromACatalogue()
    {
        var catalogue = CatalogueReader.Read(Catalogue(Doctor));

        Assert.Equal("dotnet", catalogue.Runtime);
        Assert.Equal(
            new SkillEntry(
                "narrativetrace-doctor",
                "Diagnoses an install.",
                "agents/narrativetrace-doctor/SKILL.md",
                "claude/narrativetrace-doctor/SKILL.md"),
            Assert.Single(catalogue.Skills));
    }

    [Fact]
    public void FindsASkillByNameAndReportsAnUnknownOneAsEmpty()
    {
        var catalogue = CatalogueReader.Read(Catalogue(Doctor));

        Assert.NotNull(catalogue.Skill("narrativetrace-doctor"));
        Assert.Null(catalogue.Skill("narrativetrace-doctorx"));
    }

    // --- refusals: a malformed carrier is refused before any work, naming the entry --------------

    [Fact]
    public void RefusesACatalogueThatNamesTheSameSkillTwice()
    {
        var message = Assert.Throws<ArgumentException>(
            () => CatalogueReader.Read(Catalogue(Doctor + "," + Doctor))).Message;

        Assert.Contains("names a skill twice", message, StringComparison.Ordinal);
        Assert.Contains("narrativetrace-doctor", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASkillMissingAFlavourPathAndNamesIt()
    {
        var noClaude =
            "{\"name\": \"add-narrative-tracing\", \"description\": \"d\", \"agents\": \"agents/a/SKILL.md\"}";

        var message = Assert.Throws<ArgumentException>(
            () => CatalogueReader.Read(Catalogue(noClaude))).Message;

        Assert.Contains("add-narrative-tracing", message, StringComparison.Ordinal);
        Assert.Contains("\"claude\"", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASkillWhoseNameIsNotAString()
    {
        Assert.Contains("skill has no \"name\" string field",
            Assert.Throws<ArgumentException>(
                () => CatalogueReader.Read(Catalogue("{\"name\": {}}"))).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASkillThatIsNotAnObject()
    {
        Assert.Contains("skill must be a JSON object",
            Assert.Throws<ArgumentException>(
                () => CatalogueReader.Read(Catalogue("\"narrativetrace-doctor\""))).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACatalogueWithoutASkillsArray()
    {
        Assert.Contains("\"skills\" must be a JSON array",
            Assert.Throws<ArgumentException>(
                () => CatalogueReader.Read("{\"runtime\": \"dotnet\"}")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACatalogueWithoutARuntime()
    {
        Assert.Contains("catalogue has no \"runtime\" string field",
            Assert.Throws<ArgumentException>(
                () => CatalogueReader.Read("{\"skills\": []}")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACatalogueBuiltWithoutASkillList()
    {
        Assert.Contains("at least one skill",
            Assert.Throws<ArgumentException>(() => new SkillCatalogue("dotnet", null!)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASkillEntryWithoutAFlavour()
    {
        var entry = new SkillEntry("n", "d", "agents/n/SKILL.md", "claude/n/SKILL.md");

        Assert.Equal("agents/n/SKILL.md", entry.PathFor(SkillFlavour.Agents));
        Assert.Equal("claude/n/SKILL.md", entry.PathFor(SkillFlavour.Claude));
        Assert.Contains("a flavour must be given",
            Assert.Throws<ArgumentException>(() => entry.PathFor((SkillFlavour)999)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EachFlavourKnowsWhereItInstalls()
    {
        Assert.Equal(".agents/skills", SkillFlavour.Agents.InstallRoot());
        Assert.Equal(".claude/skills", SkillFlavour.Claude.InstallRoot());
    }

    [Fact]
    public void RefusesACatalogueThatCarriesNoSkillAtAll()
    {
        Assert.Contains("at least one skill",
            Assert.Throws<ArgumentException>(
                () => CatalogueReader.Read("{\"runtime\": \"dotnet\", \"skills\": []}")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACatalogueThatIsNotAnObjectAtAll()
    {
        Assert.Contains("catalogue must be a JSON object",
            Assert.Throws<ArgumentException>(() => CatalogueReader.Read("[]")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASkillWhoseDescriptionIsBlank()
    {
        var blank = "{\"name\": \"n\", \"description\": \"  \", \"agents\": \"a\", \"claude\": \"c\"}";

        Assert.Contains("description must not be blank",
            Assert.Throws<ArgumentException>(() => CatalogueReader.Read(Catalogue(blank))).Message,
            StringComparison.Ordinal);
    }
}
