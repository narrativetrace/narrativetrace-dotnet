// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.FileSystemGlobbing;
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// The repository-root <c>context7.json</c> that tells Context7 what to index: it must validate
/// against the committed copy of Context7's live schema, carry the version tags plus this build's version as
/// <c>previousVersions</c>, hold only factual text, and index no code fence without a language tag.
/// </summary>
public sealed class Context7ConfigTests
{
    private static readonly string[] IndexedExtensions =
        [".md", ".mdx", ".markdown", ".rst", ".txt", ".ipynb"];

    private static string Root => RepositoryPath.Root();

    private static JsonObject Config() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "context7.json")))!.AsObject();

    private static string[] Strings(JsonObject config, string key) =>
        config[key]?.AsArray().Select(n => n!.GetValue<string>()).ToArray() ?? [];

    [Fact]
    public void Config_validates_against_the_committed_schema_copy()
    {
        var schema = JsonSchema.FromText(File.ReadAllText(
            Path.Combine(Root, "config", "context7", "context7.schema.json")));

        var result = schema.Evaluate(JsonNode.Parse(File.ReadAllText(Path.Combine(Root, "context7.json"))),
            new EvaluationOptions { OutputFormat = OutputFormat.List });

        Assert.True(result.IsValid, JsonSerializer.Serialize(result.Details.Where(d => !d.IsValid)
            .Select(d => $"{d.InstanceLocation}: {string.Join("; ", d.Errors?.Values ?? [])}")));
    }

    [Fact]
    public void Schema_copy_records_the_day_it_was_fetched()
    {
        var note = File.ReadAllText(Path.Combine(Root, "config", "context7", "FETCHED.txt"));

        Assert.Matches(@"https://context7\.com/schema/context7\.json", note);
        Assert.Matches(@"\d{4}-\d{2}-\d{2}", note);
    }

    [Fact]
    public void Config_carries_the_owner_supplied_claim()
    {
        var config = Config();

        Assert.Equal("https://context7.com/narrativetrace/narrativetrace-dotnet", config["url"]!.GetValue<string>());
        Assert.Matches(@"^pk_[A-Za-z0-9]+$", config["public_key"]!.GetValue<string>());
    }

    [Fact]
    public void Title_names_the_runtime()
    {
        Assert.Equal("NarrativeTrace for .NET", Config()["projectTitle"]!.GetValue<string>());
    }

    [Fact]
    public void Description_is_two_factual_sentences_naming_the_published_packages()
    {
        var description = Config()["description"]!.GetValue<string>();

        Assert.InRange(description.Length, 10, 500);
        Assert.Equal(2, description.Split(". ", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains("NarrativeTrace.Proxy", description);
        Assert.Contains("NarrativeTrace.Logging", description);
    }

    [Fact]
    public void Rules_are_five_to_seven_lines_each_within_the_limit()
    {
        var rules = Strings(Config(), "rules");

        Assert.InRange(rules.Length, 5, 7);
        Assert.All(rules, rule => Assert.InRange(rule.Length, 1, 255));
    }

    [Fact]
    public void Every_translated_mirror_is_excluded_from_the_index()
    {
        var excluded = Strings(Config(), "excludeFolders");

        foreach (var mirror in TranslatedMirrors())
            Assert.Contains(excluded, pattern => mirror == pattern || mirror.StartsWith(pattern + "/", StringComparison.Ordinal));
    }

    [SkippableFact]
    public void Previous_versions_are_the_version_tags_plus_this_builds_version()
    {
        var listed = Config()["previousVersions"]!.AsArray().Select(n => n!["tag"]!.GetValue<string>()).ToArray();

        var problems = Context7PreviousVersionsSupport.Check(Root, listed);

        Skip.If(problems is null, "no git history with version tags (public snapshot)");
        Assert.Empty(problems!);
    }

    [Fact]
    public void The_builds_own_version_is_listed_before_its_tag_exists()
    {
        using var repo = TaggedRepo("0.3.0", "v0.1.0", "v0.2.0");

        var problems = Context7PreviousVersionsSupport.Check(repo.Path, ["v0.3.0", "v0.2.0", "v0.1.0"]);

        Assert.Empty(problems!);
    }

    [Fact]
    public void A_file_that_omits_the_builds_own_version_is_reported()
    {
        using var repo = TaggedRepo("0.3.0", "v0.1.0", "v0.2.0");

        var problems = Context7PreviousVersionsSupport.Check(repo.Path, ["v0.2.0", "v0.1.0"]);

        Assert.Contains(problems!, p => p.Contains("v0.3.0", StringComparison.Ordinal));
    }

    [Fact]
    public void The_release_tags_own_checkout_is_clean_when_the_file_lists_it()
    {
        using var repo = TaggedRepo("0.2.0", "v0.1.0", "v0.2.0");

        var problems = Context7PreviousVersionsSupport.Check(repo.Path, ["v0.2.0", "v0.1.0"]);

        Assert.Empty(problems!);
    }

    [Fact]
    public void A_tagged_checkout_without_the_version_source_is_a_misconfigured_gate_not_a_skip()
    {
        using var repo = TaggedRepo(null, "v0.1.0");

        Assert.Throws<InvalidOperationException>(() => Context7PreviousVersionsSupport.Check(repo.Path, ["v0.1.0"]));
    }

    [Fact]
    public void A_checkout_without_tags_is_a_skip()
    {
        using var repo = TaggedRepo("0.1.0");

        Assert.Null(Context7PreviousVersionsSupport.Check(repo.Path, ["v0.1.0"]));
    }

    [Fact]
    public void A_tree_without_git_is_a_skip()
    {
        using var dir = new TempDir();

        Assert.Null(Context7PreviousVersionsSupport.Check(dir.Path, []));
    }

    [Fact]
    public void Versions_order_numerically_so_ten_follows_nine()
    {
        var problems = Context7PreviousVersionsSupport.Problems(["v0.9.0", "v0.2.0"], "0.10.0", ["v0.10.0", "v0.9.0", "v0.2.0"]);

        Assert.Empty(problems);
    }

    [Fact]
    public void A_version_between_two_tags_is_listed_in_numeric_position()
    {
        var problems = Context7PreviousVersionsSupport.Problems(["v0.3.0", "v0.1.0"], "0.2.0", ["v0.3.0", "v0.2.0", "v0.1.0"]);

        Assert.Empty(problems);
    }

    [Fact]
    public void A_stale_extra_entry_is_reported()
    {
        var problems = Context7PreviousVersionsSupport.Problems(["v0.1.0"], "0.1.0", ["v9.9.9", "v0.1.0"]);

        Assert.Single(problems);
    }

    [Fact]
    public void A_blank_build_version_is_refused()
    {
        Assert.Throws<ArgumentException>(() => Context7PreviousVersionsSupport.Problems(["v0.1.0"], " ", []));
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("c7prev").FullName;

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }

    private static TempDir TaggedRepo(string? version, params string[] tags)
    {
        var dir = new TempDir();
        if (version is not null)
            File.WriteAllText(System.IO.Path.Combine(dir.Path, "Directory.Build.props"),
                $"<Project><PropertyGroup><VersionPrefix>{version}</VersionPrefix></PropertyGroup></Project>");
        Git(dir.Path, "init", "-q");
        foreach (var tag in tags)
        {
            File.WriteAllText(System.IO.Path.Combine(dir.Path, $"marker-{tag}.txt"), tag);
            Git(dir.Path, "add", ".");
            Git(dir.Path, "-c", "user.name=t", "-c", "user.email=t@t", "-c", "commit.gpgsign=false", "commit", "-q", "-m", tag);
            Git(dir.Path, "-c", "tag.gpgsign=false", "tag", tag);
        }
        return dir;
    }

    private static void Git(string workingDirectory, params string[] args)
    {
        var info = new ProcessStartInfo("git", args) { WorkingDirectory = workingDirectory, RedirectStandardOutput = true, RedirectStandardError = true };
        using var git = Process.Start(info)!;
        git.StandardOutput.ReadToEnd();
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
    }

    [Fact]
    public void Every_indexed_code_fence_carries_a_language_tag()
    {
        var offenders = IndexedFiles()
            .SelectMany(file => FenceAuditSupport.UntaggedFenceLines(File.ReadAllText(Path.Combine(Root, file)))
                .Select(line => $"{file}:{line}"))
            .ToList();

        Assert.True(offenders.Count == 0, "untagged fences: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Index_selection_reaches_the_published_documents_and_nothing_translated()
    {
        var files = IndexedFiles().ToList();

        Assert.Contains("README.md", files);
        Assert.Contains("documentation/guides/llms.txt", files);
        Assert.Contains("documentation/privacy-and-redaction.md", files);
        Assert.DoesNotContain(files, f => f.StartsWith("documentation/es/", StringComparison.Ordinal));
        Assert.DoesNotContain("LEAME.md", files);
    }

    private static IEnumerable<string> TranslatedMirrors()
    {
        var languages = new[] { "es", "pt-BR", "zh-CN" };
        foreach (var language in languages)
        {
            foreach (var parent in new[] { "documentation", "documentation/guides" })
            {
                if (Directory.Exists(Path.Combine(Root, parent, language)))
                    yield return $"{parent}/{language}";
            }
        }
        if (Directory.Exists(Path.Combine(Root, "documentation", "i18n")))
            yield return "documentation/i18n";
    }

    /// <summary>The repo-relative files Context7 would index, minus what the snapshot strips.</summary>
    private static IEnumerable<string> IndexedFiles()
    {
        var config = Config();
        var folders = Strings(config, "folders");
        var excludedFolders = Strings(config, "excludeFolders");
        var excludedFiles = Strings(config, "excludeFiles");
        var stripped = StrippedMatcher();
        return Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(Root, path).Replace('\\', '/'))
            .Where(file => IndexedExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Where(file => !file.Contains('/') || folders.Any(f => file.StartsWith(f + "/", StringComparison.Ordinal)))
            .Where(file => !excludedFolders.Any(p => file.StartsWith(p + "/", StringComparison.Ordinal)))
            .Where(file => !excludedFiles.Contains(Path.GetFileName(file)))
            .Where(file => stripped is null || !stripped.Match(file).HasMatches)
            .Where(file => !file.StartsWith("artifacts/", StringComparison.Ordinal) && !file.Contains("/bin/") && !file.Contains("/obj/"))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static Matcher? StrippedMatcher()
    {
        var ignore = Path.Combine(Root, ".publishignore");
        if (!File.Exists(ignore))
            return null;
        var matcher = new Matcher(StringComparison.Ordinal);
        foreach (var line in File.ReadAllLines(ignore).Where(l => l.Length > 0 && l[0] != '#'))
        {
            matcher.AddInclude(line);
            matcher.AddInclude(line + "/**");
        }
        return matcher;
    }
}
