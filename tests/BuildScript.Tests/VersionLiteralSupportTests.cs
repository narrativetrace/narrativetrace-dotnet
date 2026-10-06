// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers <see cref="VersionLiteralSupport"/> — "no version talk anywhere" about NarrativeTrace's
/// own versions (owner ruling 2026-09-24, narrowed 2026-09-25): two rules and no third — a
/// NarrativeTrace install coordinate must equal the build's version, and a since-marker or banner
/// is forbidden. Somebody else's version in prose passes.
/// </summary>
public sealed class VersionLiteralSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-version-literal").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private string Read(string relativePath) => File.ReadAllText(Path.Combine(_repo, relativePath));

    private void Guide(string body) => Write("documentation/guide.md", body);

    private IReadOnlyList<string> Problems(string version = "0.1.5") =>
        VersionLiteralSupport.Check(_repo, version);

    // ---------------------------------------------------------------------------------------
    // Sync — every coordinate form
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Sync_rewrites_a_package_reference_coordinate()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.5\" />\n",
            Read("documentation/guide.md"));
    }

    [Fact]
    public void Sync_rewrites_a_package_reference_that_wraps_after_the_version()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.MSBuild\" Version=\"0.1.2\"\n"
            + "                  PrivateAssets=\"all\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("<PackageReference Include=\"NarrativeTrace.MSBuild\" Version=\"0.1.5\"\n"
            + "                  PrivateAssets=\"all\" />\n", Read("documentation/guide.md"));
    }

    [Fact]
    public void Sync_rewrites_a_dotnet_add_package_coordinate()
    {
        Guide("dotnet add package NarrativeTrace.Proxy --version 0.1.2\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("dotnet add package NarrativeTrace.Proxy --version 0.1.5\n",
            Read("documentation/guide.md"));
    }

    [Fact]
    public void Sync_rewrites_a_translated_mirror()
    {
        Write("documentation/es/guia.md",
            "<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.5\" />\n",
            Read("documentation/es/guia.md"));
    }

    [Fact]
    public void Sync_leaves_a_third_party_package_reference_alone()
    {
        Guide("<PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"9.0.0\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("<PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"9.0.0\" />\n",
            Read("documentation/guide.md"));
    }

    [Fact]
    public void Sync_treats_a_near_miss_package_id_as_third_party()
    {
        Guide("<PackageReference Include=\"NarrativeTraceX.Core\" Version=\"0.1.2\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("<PackageReference Include=\"NarrativeTraceX.Core\" Version=\"0.1.2\" />\n",
            Read("documentation/guide.md"));
    }

    [Fact]
    public void Sync_reports_every_file_it_rewrote_and_nothing_else()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");
        Write("documentation/untouched.md", "nothing to see\n");

        var changed = VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal(new[] { "documentation/guide.md: install coordinate -> 0.1.5" }, changed);
    }

    [Fact]
    public void Sync_is_idempotent()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");
        var second = VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Empty(second);
    }

    [Fact]
    public void Sync_keeps_a_file_without_a_trailing_newline_as_it_is()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.5\" />",
            Read("documentation/guide.md"));
    }

    [Fact]
    public void Sync_restamps_a_mirror_whose_english_source_it_rewrote()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");
        Write("documentation/es/guia.md",
            "<!-- source: documentation/guide.md blob 000000000000 | translated: 2026-01-01 -->\n"
            + "<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        var expected = TranslationCheckSupport.GitBlobHash(
            File.ReadAllBytes(Path.Combine(_repo, "documentation/guide.md")))[..12];
        Assert.Equal(
            $"<!-- source: documentation/guide.md blob {expected} | translated: 2026-01-01 -->",
            Read("documentation/es/guia.md").Split('\n')[0]);
    }

    [Fact]
    public void Sync_leaves_a_mirror_of_an_untouched_source_stamped()
    {
        Write("documentation/guide.md", "no coordinate here\n");
        var header = "<!-- source: documentation/guide.md blob 000000000000 | translated: 2026-01-01 -->";
        Write("documentation/es/guia.md", $"{header}\nhola\n");

        VersionLiteralSupport.Sync(_repo, "0.1.5");

        Assert.Equal(header, Read("documentation/es/guia.md").Split('\n')[0]);
    }

    // ---------------------------------------------------------------------------------------
    // Check — coordinates
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Check_fails_on_a_coordinate_at_the_wrong_version_naming_file_and_line()
    {
        Guide("intro\n<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");

        Assert.StartsWith("documentation/guide.md:2: NarrativeTrace coordinate pinned to 0.1.2",
            Assert.Single(Problems()));
    }

    [Fact]
    public void Check_passes_a_coordinate_at_the_build_version()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.5\" />\n");

        Assert.Empty(Problems());
    }

    [Fact]
    public void Check_fails_on_a_dotnet_add_coordinate_at_the_wrong_version()
    {
        Guide("dotnet add package NarrativeTrace.Proxy --version 0.1.2\n");

        Assert.Single(Problems());
    }

    [Fact]
    public void Check_passes_a_third_party_package_reference()
    {
        Guide("<PackageReference Include=\"Microsoft.Extensions.Logging\" Version=\"9.0.0\" />\n");

        Assert.Empty(Problems());
    }

    [Fact]
    public void Check_treats_a_near_miss_package_id_as_third_party()
    {
        Guide("<PackageReference Include=\"NarrativeTraceX.Core\" Version=\"9.0.0\" />\n");

        Assert.Empty(Problems());
    }

    // The narrowed rule, both halves on one page: somebody else's version in prose — a
    // compatibility-table row, an advisory's before/after pair — is a fact about their release and
    // passes, while OUR coordinate at the wrong version is still the one thing reported.
    [Fact]
    public void Check_passes_third_party_versions_in_prose_and_still_fails_our_own_coordinate()
    {
        Guide("| .NET | 10.0.0 | the target framework |\n"
            + "The logging advisory moved 8.0.1 -> 8.0.12.\n"
            + "<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.9\" />\n");

        Assert.StartsWith("documentation/guide.md:3: NarrativeTrace coordinate pinned to 0.1.9",
            Assert.Single(Problems()));
    }

    // ---------------------------------------------------------------------------------------
    // Check — the markers the ruling removed
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Check_fails_on_a_since_marker()
    {
        Guide("Output is on by default *(since 0.1.3)*.\n");

        Assert.Contains("*(since …)* marker", Assert.Single(Problems()));
    }

    [Fact]
    public void Check_fails_on_a_since_marker_wrapped_after_since()
    {
        Guide("Output is on by default *(since\n0.1.3, unreleased)*.\n");

        Assert.Contains(Problems(), p => p.StartsWith("documentation/guide.md:1:")
            && p.Contains("*(since …)* marker"));
    }

    [Fact]
    public void Check_fails_on_a_since_marker_wrapped_after_the_version_comma()
    {
        Guide("Output is on by default *(since 0.1.3,\nunreleased)*.\n");

        Assert.Contains(Problems(), p => p.StartsWith("documentation/guide.md:1:")
            && p.Contains("*(since …)* marker"));
    }

    [Fact]
    public void Check_fails_on_a_since_marker_in_a_heading()
    {
        // The scan ContractLint used to run for heading markers (their GitHub anchor slug moved
        // when a release settled) is now this lint's job — a heading is just another line to it.
        Guide("## Traceparent seeding *(since 0.1.3)*\n\nSome text.\n");

        Assert.Contains(Problems(), p => p.StartsWith("documentation/guide.md:1:")
            && p.Contains("*(since …)* marker"));
    }

    [Fact]
    public void Check_fails_on_a_since_marker_inside_a_fenced_block()
    {
        Guide("```text\nguide.md:7: *(since 0.1.3)* is version talk\n```\n");

        Assert.Contains(Problems(), p => p.Contains("*(since …)* marker"));
    }

    [Fact]
    public void Check_fails_on_the_published_version_banner()
    {
        Guide("<!-- published-version -->\n");

        Assert.Contains("published-version banner", Assert.Single(Problems()));
    }

    [Fact]
    public void Check_fails_on_the_cache_age_comment()
    {
        Guide("*(Docs and published both at 0.1.4.)* <!-- published-version checked 2026-01-01T00:00:00Z (cached, 40m old) -->\n");

        Assert.Contains(Problems(), p => p.Contains("published-version banner"));
    }

    // ---------------------------------------------------------------------------------------
    // Scope
    // ---------------------------------------------------------------------------------------

    // The fixture carries a coordinate the rule WOULD flag in a governed page, so a green result
    // proves the file is out of scope rather than that it happened to be clean.
    [Fact]
    public void Check_ignores_a_contract_yaml()
    {
        Write("documentation/contract.yaml",
            "entries:\n  - claim: \"dotnet add package NarrativeTrace.Core --version 0.1.0 resolves\"\n");

        Assert.Empty(Problems());
    }

    [Fact]
    public void Check_reads_an_empty_documentation_directory()
    {
        Directory.CreateDirectory(Path.Combine(_repo, "documentation"));

        Assert.Empty(Problems());
    }

    [Fact]
    public void Check_reads_the_root_readme_and_its_mirrors()
    {
        Write("README.md", "<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.2\" />\n");
        Write("LEAME.md",
            "<!-- source: README.md blob 000000000000 | translated: 2026-01-01 -->\n"
            + "<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.1\" />\n");

        Assert.Equal(new[] { "LEAME.md", "README.md" },
            Problems().Select(p => p[..p.IndexOf(':')]).Distinct().OrderBy(f => f).ToArray());
    }

    [Fact]
    public void Check_skips_an_internal_doc_that_publishignore_strips()
    {
        // Fictional stripped paths — never the repo's real private-doc names, so this shipped test
        // file cites no stripped artifact (the publish reference gate forbids that).
        Write(".publishignore", "documentation/scratch-*.md\n");
        Write("documentation/scratch-roadmap.md",
            "<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.0\" />\n");
        Write("documentation/scratch-analysis.md", "dotnet add package NarrativeTrace.Proxy --version 0.1.1\n");
        Write("documentation/guide.md", "no coordinate here\n");

        Assert.Empty(Problems());
    }

    [Fact]
    public void Check_ignores_a_file_without_a_trailing_newline()
    {
        Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"0.1.5\" />");

        Assert.Empty(Problems());
    }

    // ---------------------------------------------------------------------------------------
    // The real repository is clean — the wired gate must be green in the tree it ships from.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Real_repository_public_docs_carry_no_version_talk()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var props = Path.Combine(repoRoot, "Directory.Build.props");
        if (!File.Exists(props))
            return; // not running from the repo tree (e.g. a packaged test run)
        var version = System.Text.RegularExpressions.Regex
            .Match(File.ReadAllText(props), "<VersionPrefix>([^<]+)</VersionPrefix>").Groups[1].Value;

        Assert.Empty(VersionLiteralSupport.Check(repoRoot, version));
    }

    // ---------------------------------------------------------------------------------------
    // Property: sync then check is clean, for any version, and sync never moves twice — with
    // somebody else's version sitting in the same page's prose, untouched by either half.
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Sync_then_check_is_clean_and_idempotent_for_any_version()
    {
        var random = new Random(20260925);
        for (var iteration = 0; iteration < 200; iteration++)
        {
            var version = $"{100 + random.Next(20)}.{100 + random.Next(20)}.{100 + random.Next(20)}";
            Guide("<PackageReference Include=\"NarrativeTrace.Core\" Version=\"9.9.9\" />\n"
                + "dotnet add package NarrativeTrace.Proxy --version 9.9.9\n"
                + "The example service reports ServiceVersion 2.0.0.\n");

            VersionLiteralSupport.Sync(_repo, version);
            Assert.Empty(VersionLiteralSupport.Check(_repo, version));
            Assert.Empty(VersionLiteralSupport.Sync(_repo, version));
        }
    }
}
