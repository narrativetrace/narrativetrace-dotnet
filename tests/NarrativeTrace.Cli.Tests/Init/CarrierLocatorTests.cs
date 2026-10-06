// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using NarrativeTrace.Cli.Init;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>
/// The launcher's carrier resolution (D4), the .NET analogue of Java's <c>CarrierLocator</c> tests:
/// the bundled carrier is read from this CLI assembly's embedded resources; <c>--from</c> opens a
/// directory, a <c>.nupkg</c>, or an <c>Id/Version</c> coordinate in the local NuGet cache; anything
/// else is refused by name. Zero network in every case.
/// </summary>
public sealed class CarrierLocatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-locator-" + Guid.NewGuid().ToString("N"));

    public CarrierLocatorTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void OpensTheCarrierBundledInThisCliAssembly()
    {
        var carrier = CarrierLocator.Open(null);

        Assert.StartsWith("NarrativeTrace.Skills/", carrier.Coordinate, StringComparison.Ordinal);
        Assert.Equal(2, carrier.Skills.Count);
        Assert.Contains(carrier.Skills, skill => skill.Name == "narrativetrace-doctor");
    }

    [Fact]
    public void OpensADirectoryCarrierNamedByFrom()
    {
        var dir = Path.Combine(_root, "dir-carrier");
        WriteCarrier(dir, version: null);

        var carrier = CarrierLocator.Open(dir);

        Assert.Equal("NarrativeTrace.Skills/unknown", carrier.Coordinate);
        Assert.Equal(2, carrier.Skills.Count);
    }

    [Fact]
    public void OpensANupkgArchiveStampedWithItsNuspecVersion()
    {
        var nupkg = WriteNupkg(Path.Combine(_root, "pkg.nupkg"), "4.5.6");

        var carrier = CarrierLocator.Open(nupkg);

        Assert.Equal("NarrativeTrace.Skills/4.5.6", carrier.Coordinate);
        Assert.Equal(2, carrier.Skills.Count);
    }

    [Fact]
    public void OpensACoordinateFromTheLocalNuGetCache()
    {
        var cache = Path.Combine(_root, "cache");
        WriteCarrier(Path.Combine(cache, "narrativetrace.skills", "0.1.5", "skills"), version: null);

        var carrier = CarrierLocator.Open("NarrativeTrace.Skills/0.1.5", cache);

        Assert.Equal("NarrativeTrace.Skills/0.1.5", carrier.Coordinate);
        Assert.Equal(2, carrier.Skills.Count);
    }

    [Fact]
    public void RefusesACoordinateThatIsNotInTheCache()
    {
        var message = Assert.Throws<ArgumentException>(
            () => CarrierLocator.Open("NarrativeTrace.Skills/9.9.9", Path.Combine(_root, "empty-cache"))).Message;

        Assert.Contains("NarrativeTrace.Skills/9.9.9", message, StringComparison.Ordinal);
        Assert.Contains("local NuGet cache", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAFromThatIsNeitherPathNorCoordinate()
    {
        var message = Assert.Throws<ArgumentException>(
            () => CarrierLocator.Open("not-a-real-thing", _root)).Message;

        Assert.Contains("no carrier at not-a-real-thing", message, StringComparison.Ordinal);
        Assert.Contains("--from takes a directory", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesATwoSegmentPathThatIsNotAVersionCoordinate()
    {
        // "foo/bar" has a slash but the version half is not a version — it is not mistaken for a
        // coordinate, so it earns the plain refusal, not a NuGet-cache miss message.
        var message = Assert.Throws<ArgumentException>(
            () => CarrierLocator.Open("foo/bar", _root)).Message;

        Assert.Contains("no carrier at foo/bar", message, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesACorruptArchiveWithoutLeakingTheZipError()
    {
        var bogus = Path.Combine(_root, "not-really.nupkg");
        File.WriteAllText(bogus, "this is not a zip");

        var message = Assert.Throws<ArgumentException>(() => CarrierLocator.Open(bogus)).Message;

        Assert.Contains("cannot read the carrier at", message, StringComparison.Ordinal);
    }

    private static void WriteCarrier(string root, string? version)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "catalogue.json"), Catalogue());
        WritePage(root, "agents", "narrativetrace-doctor");
        WritePage(root, "claude", "narrativetrace-doctor");
        WritePage(root, "agents", "add-narrative-tracing");
        WritePage(root, "claude", "add-narrative-tracing");
        if (version is not null)
        {
            File.WriteAllText(Path.Combine(root, "narrativetrace-version.properties"), "version=" + version + "\n");
        }
    }

    private static string WriteNupkg(string path, string version)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Entry(zip, "NarrativeTrace.Skills.nuspec",
            "<?xml version=\"1.0\"?><package><metadata><id>NarrativeTrace.Skills</id>"
            + "<version>" + version + "</version></metadata></package>");
        Entry(zip, "skills/catalogue.json", Catalogue());
        foreach (var flavour in new[] { "agents", "claude" })
        {
            foreach (var name in new[] { "narrativetrace-doctor", "add-narrative-tracing" })
            {
                Entry(zip, "skills/" + flavour + "/" + name + "/SKILL.md", Page(flavour, name));
            }
        }

        return path;
    }

    private static void Entry(ZipArchive zip, string name, string content)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name).Open());
        writer.Write(content);
    }

    private static void WritePage(string root, string flavour, string name)
    {
        var directory = Path.Combine(root, flavour, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), Page(flavour, name));
    }

    private static string Page(string flavour, string name) =>
        "---\nname: " + name + "\ndescription: d\n---\n\n" + name + " " + flavour + "\n";

    private static string Catalogue() =>
        "{\"runtime\": \"dotnet\", \"skills\": ["
        + Skill("narrativetrace-doctor") + ", " + Skill("add-narrative-tracing") + "]}";

    private static string Skill(string name) =>
        "{\"name\": \"" + name + "\", \"description\": \"d\","
        + " \"agents\": \"agents/" + name + "/SKILL.md\","
        + " \"claude\": \"claude/" + name + "/SKILL.md\"}";
}
