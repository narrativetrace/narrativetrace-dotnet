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
/// against the committed copy of Context7's live schema, carry the version tags as
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
    public void Config_leaves_ownership_verification_to_the_owner()
    {
        var config = Config();

        Assert.False(config.ContainsKey("url"));
        Assert.False(config.ContainsKey("public_key"));
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
    public void Previous_versions_are_exactly_the_version_tags()
    {
        var tags = VersionTags();
        Skip.If(tags is null, "no git history with version tags (public snapshot)");

        var listed = Config()["previousVersions"]!.AsArray().Select(n => n!["tag"]!.GetValue<string>()).ToArray();

        Assert.Equal(tags, listed);
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

    private static string[]? VersionTags()
    {
        if (!Directory.Exists(Path.Combine(Root, ".git")))
            return null;
        var info = new ProcessStartInfo("git", ["tag", "--list", "v*", "--sort=-v:refname"])
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
        };
        using var git = Process.Start(info)!;
        var tags = git.StandardOutput.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        git.WaitForExit();
        return tags.Length == 0 ? null : tags;
    }
}
