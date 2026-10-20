// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.IO.Compression;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class OpenAiPackageCheckTests : IDisposable
{
    private const string ManifestTemplate =
        "{\n  \"name\": \"narrativetrace-x\",\n  \"version\": \"{0}\",\n  \"extensions\": {\n    \"com.openai\": {\n" +
        "      \"interface\": {\n        \"logo\": \"./assets/logo.png\",\n        \"composerIcon\": \"./assets/composer-icon.png\"\n" +
        "      }\n    }\n  }\n}\n";

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nt-openai-check-" + Guid.NewGuid().ToString("N"));

    public OpenAiPackageCheckTests()
    {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static Dictionary<string, byte[]> ValidEntries(string version) => new()
    {
        ["plugin.json"] = System.Text.Encoding.UTF8.GetBytes(ManifestTemplate.Replace("{0}", version, StringComparison.Ordinal)),
        ["skills/narrativetrace-doctor/SKILL.md"] = "page"u8.ToArray(),
        ["assets/logo.png"] = PngFixtures.Flat(64, 64, 0x4a),
        ["assets/composer-icon.png"] = PngFixtures.Flat(64, 64, 0x4a),
    };

    private static List<string> ExpectedFor(Dictionary<string, byte[]> entries) =>
        [.. entries.Keys.OrderBy(k => k, StringComparer.Ordinal)];

    private string Zip(Dictionary<string, byte[]> entries, bool withDirectoryEntries = false)
    {
        var path = Path.Combine(_dir, "package.zip");
        File.Delete(path);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        if (withDirectoryEntries)
        {
            archive.CreateEntry("skills/");
            archive.CreateEntry("assets/");
        }

        foreach (var (name, bytes) in entries)
        {
            using var stream = archive.CreateEntry(name).Open();
            stream.Write(bytes);
        }

        return path;
    }

    private IReadOnlyList<string> Violations(Dictionary<string, byte[]> entries, string version = "1.2.3", List<string>? expected = null) =>
        OpenAiPackageCheck.Violations(Zip(entries), expected ?? ExpectedFor(entries), version);

    [Fact]
    public void A_valid_package_has_no_violations()
    {
        Assert.Empty(Violations(ValidEntries("1.2.3")));
    }

    [Fact]
    public void Directory_entries_a_zip_format_artifact_are_not_unexpected_content()
    {
        var entries = ValidEntries("1.2.3");

        Assert.Empty(OpenAiPackageCheck.Violations(Zip(entries, withDirectoryEntries: true), ExpectedFor(entries), "1.2.3"));
    }

    [Fact]
    public void Flags_a_missing_expected_entry_and_an_unexpected_extra_one()
    {
        var entries = ValidEntries("1.2.3");
        var expected = ExpectedFor(entries);
        entries.Remove("skills/narrativetrace-doctor/SKILL.md");
        Assert.Contains(Violations(entries, expected: expected), v => v.Contains("entries do not match", StringComparison.Ordinal));

        var extra = ValidEntries("1.2.3");
        extra["skills/stray/SKILL.md"] = "x"u8.ToArray();
        Assert.Contains(Violations(extra, expected: ExpectedFor(ValidEntries("1.2.3"))), v => v.Contains("entries do not match", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("hooks/pre-install.js")]
    [InlineData("mcp.json")]
    [InlineData("widget.app.json")]
    [InlineData("Hooks.class")]
    public void Flags_every_forbidden_entry_by_name(string forbidden)
    {
        var entries = ValidEntries("1.2.3");
        entries[forbidden] = "x"u8.ToArray();

        Assert.Contains(Violations(entries), v => v.Contains("forbidden entry present: " + forbidden, StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_flag_near_miss_siblings_of_the_forbidden_names()
    {
        var entries = ValidEntries("1.2.3");
        foreach (var name in new[] { "hooksworthy/file.js", "notmcp.json", "widgetapp.json", "NotesClass.txt", "skills/x/hooks/note.md" })
        {
            entries[name] = "x"u8.ToArray();
        }

        Assert.DoesNotContain(Violations(entries), v => v.Contains("forbidden entry present", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("1.2.3-SNAPSHOT")]
    [InlineData("1.2")]
    [InlineData("1.2.3.4")]
    [InlineData("v1.2.3")]
    [InlineData("1.2.3\n")]
    public void Flags_a_non_semver_version(string version)
    {
        var entries = ValidEntries("1.2.3");

        Assert.Contains(Violations(entries, version), v => v.Contains("not semantic", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_a_manifest_version_that_does_not_equal_the_expected_one_and_a_manifest_without_one()
    {
        Assert.Contains(Violations(ValidEntries("9.9.9")), v => v.Contains("does not equal", StringComparison.Ordinal));

        var versionless = ValidEntries("1.2.3");
        versionless["plugin.json"] = System.Text.Encoding.UTF8.GetBytes("{\n  \"name\": \"x\"\n}\n");
        Assert.Contains(Violations(versionless), v => v.Contains("carries no \"version\" key", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_a_manifest_path_that_resolves_to_no_entry()
    {
        var expected = ExpectedFor(ValidEntries("1.2.3"));
        var entries = ValidEntries("1.2.3");
        entries.Remove("assets/logo.png");

        Assert.Contains(Violations(entries, expected: expected), v => v.Contains("./assets/logo.png", StringComparison.Ordinal) && v.Contains("no packaged entry", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_an_icon_larger_than_five_mebibytes_and_accepts_one_of_exactly_that_size()
    {
        var tooBig = ValidEntries("1.2.3");
        tooBig["assets/logo.png"] = new byte[(5 * 1024 * 1024) + 1];
        Assert.Contains(Violations(tooBig), v => v.Contains("5 MiB", StringComparison.Ordinal));

        var exact = ValidEntries("1.2.3");
        var padded = new byte[5 * 1024 * 1024];
        PngFixtures.Flat(64, 64, 0x4a).CopyTo(padded, 0);
        exact["assets/logo.png"] = padded;
        Assert.DoesNotContain(Violations(exact), v => v.Contains("5 MiB", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_a_non_square_icon()
    {
        var entries = ValidEntries("1.2.3");
        entries["assets/logo.png"] = PngFixtures.Flat(64, 32, 0x4a);

        Assert.Contains(Violations(entries), v => v.Contains("not square (64x32)", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_an_icon_under_forty_eight_pixels_and_accepts_exactly_forty_eight()
    {
        var small = ValidEntries("1.2.3");
        small["assets/logo.png"] = PngFixtures.Flat(47, 47, 0x4a);
        Assert.Contains(Violations(small), v => v.Contains("smaller than 48px (47px)", StringComparison.Ordinal));

        var exact = ValidEntries("1.2.3");
        exact["assets/logo.png"] = PngFixtures.Flat(48, 48, 0x4a);
        Assert.Empty(Violations(exact));
    }

    [Fact]
    public void Reports_an_icon_that_is_not_a_png_instead_of_throwing()
    {
        var entries = ValidEntries("1.2.3");
        entries["assets/logo.png"] = "not a png at all, just text bytes"u8.ToArray();

        Assert.Contains(Violations(entries), v => v.Contains("assets/logo.png is not a readable PNG", StringComparison.Ordinal));
    }

    [Fact]
    public void Throws_when_the_package_carries_no_manifest_at_all()
    {
        var entries = ValidEntries("1.2.3");
        var expected = ExpectedFor(entries);
        entries.Remove("plugin.json");
        var zip = Zip(entries);

        var ex = Assert.Throws<InvalidOperationException>(() => OpenAiPackageCheck.Violations(zip, expected, "1.2.3"));

        Assert.Contains("plugin.json", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("HOOKS/pre-install.js")]
    [InlineData("MCP.json")]
    [InlineData("widget.APP.JSON")]
    [InlineData("Hooks.CLASS")]
    public void Flags_a_forbidden_entry_whatever_its_case_because_case_insensitive_file_systems_unpack_it_as_the_same_name(string forbidden)
    {
        var entries = ValidEntries("1.2.3");
        entries[forbidden] = "x"u8.ToArray();

        Assert.Contains(Violations(entries), v => v.Contains("forbidden entry present: " + forbidden, StringComparison.Ordinal));
    }

    [Fact]
    public void Checks_an_icon_whose_manifest_path_has_an_upper_case_extension()
    {
        var entries = ValidEntries("1.2.3");
        entries["plugin.json"] = System.Text.Encoding.UTF8.GetBytes(
            ManifestTemplate.Replace("{0}", "1.2.3", StringComparison.Ordinal).Replace("logo.png", "LOGO.PNG", StringComparison.Ordinal));
        entries["assets/LOGO.PNG"] = PngFixtures.Flat(64, 32, 0x4a);
        entries.Remove("assets/logo.png");

        Assert.Contains(Violations(entries), v => v.Contains("assets/LOGO.PNG is not square", StringComparison.Ordinal));
    }

    [Fact]
    public void Flags_a_duplicate_entry_and_a_path_traversal_entry_as_a_layout_mismatch()
    {
        var expected = ExpectedFor(ValidEntries("1.2.3"));
        var path = Path.Combine(_dir, "dup.zip");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach (var (name, bytes) in ValidEntries("1.2.3").Append(new("assets/logo.png", PngFixtures.Flat(64, 64, 0x4a))).Append(new("../evil.txt", "x"u8.ToArray())))
            {
                using var stream = archive.CreateEntry(name).Open();
                stream.Write(bytes);
            }
        }

        Assert.Contains(OpenAiPackageCheck.Violations(path, expected, "1.2.3"), v => v.Contains("entries do not match", StringComparison.Ordinal));
    }

    [Fact]
    public void Rejects_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => OpenAiPackageCheck.Violations(null!, [], "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => OpenAiPackageCheck.Violations("x.zip", null!, "1.2.3"));
        Assert.Throws<ArgumentNullException>(() => OpenAiPackageCheck.Violations("x.zip", [], null!));
    }
}
