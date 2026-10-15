// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// A refused carrier tells the reader what is wrong and where, whole: each refusal's message is pinned
/// to its exact text, so a message that loses its tail or its subject is a failing test.
/// </summary>
public sealed class CarrierMessageTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-carrier-msg-" + Guid.NewGuid().ToString("N"));

    public CarrierMessageTest() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string MessageOf(System.Action act) => Assert.Throws<ArgumentException>(act).Message;

    [Fact]
    public void A_missing_source_is_named()
    {
        var missing = Path.Combine(_root, "missing");

        Assert.Equal("no carrier at " + missing, MessageOf(() => Carrier.Open(missing)));
    }

    [Fact]
    public void A_file_source_is_refused_as_not_an_exploded_directory()
    {
        var file = Path.Combine(_root, "carrier.zip");
        File.WriteAllText(file, "not a directory");

        Assert.Equal(
            "no carrier at " + file + " — this milestone reads an exploded directory carrier, not an archive",
            MessageOf(() => Carrier.Open(file)));
    }

    [Fact]
    public void A_carrier_without_its_catalogue_names_its_coordinate_and_the_file()
    {
        Assert.Equal(
            "the carrier NarrativeTrace.Skills/1.0.0 carries no catalogue.json",
            MessageOf(() => Carrier.FromSource("1.0.0", _ => null)));
    }

    [Fact]
    public void An_unusable_catalogue_names_the_carrier_and_the_reason()
    {
        var message = MessageOf(() => Carrier.FromSource("1.0.0", _ => "{\"runtime\":"));

        Assert.StartsWith("the carrier NarrativeTrace.Skills/1.0.0's catalogue.json is unusable: ", message, StringComparison.Ordinal);
        Assert.Contains("malformed JSON", message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_listed_page_the_carrier_does_not_carry_is_named_with_its_skill_and_path()
    {
        var exploded = Carriers.Write(Path.Combine(_root, "missing-page"));
        File.Delete(Path.Combine(exploded, "agents", Carriers.AddTracing, "SKILL.md"));

        Assert.Equal(
            "the carrier NarrativeTrace.Skills/unknown lists " + Carriers.AddTracing + " at agents/" + Carriers.AddTracing
            + "/SKILL.md, which it does not carry",
            MessageOf(() => Carrier.Open(exploded)));
    }

    [Fact]
    public void A_skill_the_carrier_does_not_carry_is_named_with_the_carriers_coordinate()
    {
        var carrier = Carriers.Open(Path.Combine(_root, "body"), "2.0.0");
        var stranger = new SkillEntry("stranger", "d", "agents/x/SKILL.md", "claude/x/SKILL.md");

        Assert.Equal(
            "this carrier does not carry stranger (NarrativeTrace.Skills/2.0.0)",
            MessageOf(() => carrier.Body(stranger, SkillFlavour.Agents)));
    }

    [Theory]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    public void A_version_that_is_a_path_segment_is_refused_with_the_coordinate_it_would_form(string version)
    {
        Assert.Equal(
            "a carrier is stamped with its own coordinate, and \"NarrativeTrace.Skills/" + version
            + "\" does not name one — a version must not contain a path segment",
            MessageOf(() => Carrier.FromSource(version, _ => "x")));
    }

    [Theory]
    [InlineData("1.0.0..2")]
    [InlineData("...")]
    [InlineData("v.")]
    public void A_version_that_only_contains_dots_is_still_a_version(string version)
    {
        var carrier = Carrier.FromSource(version, name => File.ReadAllText(Path.Combine(Carriers.Write(Path.Combine(_root, "dots" + version.Length)), name)));

        Assert.Equal("NarrativeTrace.Skills/" + version, carrier.Coordinate);
    }

    [Fact]
    public void A_file_with_no_catalogue_entry_in_a_directory_that_exists_is_refused_not_opened_half()
    {
        var empty = Path.Combine(_root, "empty");
        Directory.CreateDirectory(empty);

        Assert.Equal(
            "the carrier NarrativeTrace.Skills/unknown carries no catalogue.json",
            MessageOf(() => Carrier.Open(empty)));
    }
}
