// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// A carrier is opened whole and validated before any plan exists; a broken one is refused with a
/// message naming the entry. Method names mirror the directory-based cases of the Java
/// <c>CarrierTest</c>. The jar/NuGet-cache sources and the file-name coordinate parsing Java tests do
/// not port: this port reads an exploded directory and stamps a fixed-Id coordinate
/// (<c>NarrativeTrace.Skills/&lt;version&gt;</c>, D6), with the archive/embedded/cache sources layered
/// on at the entry point in a later Phase 3 milestone.
/// </summary>
public sealed class CarrierTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-carrier-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _subject;

    public CarrierTest()
    {
        Directory.CreateDirectory(_root);
        _subject = Carriers.Open(Path.Combine(_root, "subject"), "1.2.3");
        Assert.True(_subject.Invariant());
    }

    public void Dispose()
    {
        Assert.True(_subject.Invariant());
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Dir(string name) => Path.Combine(_root, name);

    // --- opening ------------------------------------------------------------------------------

    [Fact]
    public void OpensAnExplodedDirectoryStampedWithItsVersion()
    {
        var carrier = Carriers.Open(Dir("stamped"), "9.9.9");

        Assert.Equal("NarrativeTrace.Skills/9.9.9", carrier.Coordinate);
        Assert.Equal(2, carrier.Skills.Count);
    }

    [Fact]
    public void OpensAnExplodedDirectoryFromTheResourceRootOrFromTheCarrierRoot()
    {
        var outer = Dir("either");
        Carriers.Write(Path.Combine(outer, Carrier.CarrierRoot));

        var fromResourceRoot = Carrier.Open(outer);
        var fromCarrierRoot = Carrier.Open(Path.Combine(outer, Carrier.CarrierRoot));

        Assert.Equal(fromCarrierRoot.Skills, fromResourceRoot.Skills);
        Assert.Equal("NarrativeTrace.Skills/unknown", fromResourceRoot.Coordinate);
    }

    [Fact]
    public void HandsOutEachFlavourBodyExactlyAsTheCarrierCarriesIt()
    {
        var carrier = Carriers.Open(Dir("bodies"), "1.0.0");
        var doctor = carrier.Catalogue.Skill(Carriers.Doctor)!;

        Assert.Equal(
            Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription),
            carrier.Body(doctor, SkillFlavour.Agents));
        Assert.Equal(
            Carriers.PageBody("claude", Carriers.Doctor, Carriers.DoctorDescription),
            carrier.Body(doctor, SkillFlavour.Claude));
    }

    [Fact]
    public void ReadsTheVersionFromTheCarriersOwnPropertiesWhenItCarriesOne()
    {
        Assert.Equal(
            "NarrativeTrace.Skills/9.9.9",
            Carriers.Open(Dir("versioned"), "9.9.9").Coordinate);
    }

    [Fact]
    public void FallsBackToAnUnknownVersionWhenThePropertiesFileNamesNone()
    {
        var exploded = Carriers.Write(Dir("noversion"));
        File.WriteAllText(Path.Combine(exploded, "narrativetrace-version.properties"), "# no version here\n");

        Assert.Equal("NarrativeTrace.Skills/unknown", Carrier.Open(exploded).Coordinate);
    }

    [Fact]
    public void FallsBackToAnUnknownVersionWhenThePropertiesLeaveItEmpty()
    {
        var exploded = Carriers.Write(Dir("emptyversion"));
        File.WriteAllText(Path.Combine(exploded, "narrativetrace-version.properties"), "version=\n");

        Assert.Equal("NarrativeTrace.Skills/unknown", Carrier.Open(exploded).Coordinate);
    }

    // --- refusals: a broken carrier is refused before any work --------------------------------

    [Fact]
    public void RefusesAVersionThatWouldClimbOutOfATargetDirectory()
    {
        var exploded = Carriers.Write(Dir("climb"));
        File.WriteAllText(Path.Combine(exploded, "narrativetrace-version.properties"), "version=../../etc\n");

        Assert.Contains("must not contain a path segment",
            Assert.Throws<ArgumentException>(() => Carrier.Open(exploded)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesASourceThatDoesNotExist()
    {
        Assert.Contains("nothing-here",
            Assert.Throws<ArgumentException>(() => Carrier.Open(Dir("nothing-here"))).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesADirectoryWithNoCatalogue()
    {
        var empty = Dir("empty");
        Directory.CreateDirectory(empty);

        Assert.Contains("carries no catalogue.json",
            Assert.Throws<ArgumentException>(() => Carrier.Open(empty)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACarrierWhoseCatalogueListsAMissingFileAndNamesTheEntry()
    {
        var exploded = Carriers.Write(Dir("missingpage"));
        File.Delete(Path.Combine(exploded, "claude", Carriers.Doctor, "SKILL.md"));

        var message = Assert.Throws<ArgumentException>(() => Carrier.Open(exploded)).Message;
        Assert.Contains(Carriers.Doctor, message, StringComparison.Ordinal);
        Assert.Contains("claude/" + Carriers.Doctor + "/SKILL.md", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACarrierWhoseCatalogueIsMalformedAndNamesTheFile()
    {
        var exploded = Carriers.Write(Dir("malformed"));
        File.WriteAllText(Path.Combine(exploded, "catalogue.json"), "{\"runtime\":");

        var message = Assert.Throws<ArgumentException>(() => Carrier.Open(exploded)).Message;
        Assert.Contains("catalogue.json", message, StringComparison.Ordinal);
        Assert.Contains("malformed JSON", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesToHandOutABodyForASkillTheCarrierDoesNotList()
    {
        var carrier = Carriers.Open(Dir("stranger"), "1.0.0");
        var stranger = new SkillEntry("stranger", "d", "agents/x/SKILL.md", "claude/x/SKILL.md");

        Assert.Contains("stranger",
            Assert.Throws<ArgumentException>(() => carrier.Body(stranger, SkillFlavour.Agents)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void KeepsItsSkillListImmutable()
    {
        var carrier = Carriers.Open(Dir("immutable"), "1.0.0");

        Assert.Throws<NotSupportedException>(() => ((IList<SkillEntry>)carrier.Skills).Clear());
    }

    [Fact]
    public void RefusesANullPathAndANullSkill()
    {
        Assert.Contains("a carrier path must be given",
            Assert.Throws<ArgumentException>(() => Carrier.Open(null!)).Message,
            StringComparison.Ordinal);
        Assert.Contains("a skill must be given",
            Assert.Throws<ArgumentException>(() => _subject.Body(null!, SkillFlavour.Agents)).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void HoldsItsInvariantOnceOpen()
    {
        Assert.True(Carriers.Open(Dir("invariant"), "1.0.0").Invariant());
    }

    // --- FromSource: the seam the entry point's non-directory carriers open through ------------

    [Fact]
    public void FromSourceReadsCarrierRelativePagesAndStampsTheGivenVersion()
    {
        var root = Carriers.Write(Dir("from-source"));

        var carrier = Carrier.FromSource(
            "7.7.7", name => File.ReadAllText(Path.Combine(root, name)));

        Assert.Equal("NarrativeTrace.Skills/7.7.7", carrier.Coordinate);
        Assert.Equal(2, carrier.Skills.Count);
        Assert.Equal(
            Carriers.PageBody("agents", Carriers.Doctor, Carriers.DoctorDescription),
            carrier.Body(carrier.Catalogue.Skill(Carriers.Doctor)!, SkillFlavour.Agents));
    }

    [Fact]
    public void FromSourceRefusesAMissingReaderAndAMissingVersion()
    {
        Assert.Contains("a carrier reader must be given",
            Assert.Throws<ArgumentException>(() => Carrier.FromSource("1.0.0", null!)).Message,
            StringComparison.Ordinal);
        Assert.Contains("does not name one",
            Assert.Throws<ArgumentException>(() => Carrier.FromSource("bad/version", _ => "x")).Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FromSourceRefusesACarrierMissingItsCatalogue()
    {
        Assert.Contains("catalogue.json",
            Assert.Throws<ArgumentException>(
                () => Carrier.FromSource("1.0.0", _ => null)).Message,
            StringComparison.Ordinal);
    }
}
