// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Docs-as-tests for the clarity skill's reach: every public surface that lists the shipped skills
/// lists the same set the catalogue holds (derived, never retyped here), and the clarity guide hands a
/// reader to the skill. A fifth skill added to the catalogue fails here until each surface names it.
/// </summary>
public sealed class ClaritySkillDocsTests
{
    private const string Clarity = "add-narrativetrace-clarity";

    private static string Read(params string[] relative) =>
        File.ReadAllText(Path.Combine([RepoRoot.Find(), .. relative]));

    private static IEnumerable<string> CatalogueNames => SkillCatalogue.Skills.Select(skill => skill.CanonicalName);

    [Fact]
    public void Llms_txt_agent_skills_section_names_every_catalogue_skill_and_no_other()
    {
        var section = Read("documentation", "guides", "llms.txt")
            .Split("\n## ").Single(s => s.StartsWith("Agent skills", StringComparison.Ordinal));

        var named = Regex.Matches(section, @"^- `([a-z-]+)` — ", RegexOptions.Multiline).Select(m => m.Groups[1].Value);

        Assert.Equal(CatalogueNames.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
        Assert.Equal(CatalogueNames.Count(), named.Count());
    }

    [Fact]
    public void Llms_txt_agent_skills_entry_for_clarity_opens_with_the_catalogues_own_description_sentence()
    {
        var description = SkillCatalogue.Skills.Single(s => s.CanonicalName == Clarity).Description;
        var firstSentence = description[..(description.IndexOf(". ", StringComparison.Ordinal) + 1)];

        Assert.Contains($"- `{Clarity}` — {firstSentence}", Read("documentation", "guides", "llms.txt"), StringComparison.Ordinal);
    }

    [Fact]
    public void Llms_txt_docs_links_name_the_clarity_skill_beside_the_others()
    {
        var line = Read("documentation", "guides", "llms.txt").Split('\n')
            .Single(l => l.StartsWith("- [Agent Skills]", StringComparison.Ordinal));

        Assert.All(CatalogueNames, name => Assert.Contains($"`{name}`", line, StringComparison.Ordinal));
    }

    [Fact]
    public void Agent_skills_guide_describes_the_clarity_skill_and_its_rendered_pages()
    {
        var guide = Read("documentation", "agent-skills.md");

        Assert.Contains($"## `{Clarity}`", guide, StringComparison.Ordinal);
        Assert.Contains($".claude/skills/{Clarity}/SKILL.md", guide, StringComparison.Ordinal);
        Assert.Contains($".agents/skills/{Clarity}/SKILL.md", guide, StringComparison.Ordinal);
        Assert.Contains("Seven packaged agent playbooks", guide, StringComparison.Ordinal);
        Assert.DoesNotContain("the same three skills", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void Agent_skills_guide_install_preview_lists_every_skill_page_init_plans()
    {
        var preview = Regex.Match(
            Read("documentation", "agent-skills.md"), "<!-- snippet: artifacts/agent-skills/init-preview.json -->\n```json\n(.*?)```",
            RegexOptions.Singleline).Groups[1].Value;

        Assert.All(CatalogueNames, name => Assert.Contains($".agents/skills/{name}/SKILL.md", preview, StringComparison.Ordinal));
    }

    [Fact]
    public void The_clarity_guide_hands_a_reader_to_the_skill_and_to_the_cli_verbs_it_runs()
    {
        var guide = Read("documentation", "guides", "clarity.md");

        Assert.Contains($"`{Clarity}`", guide, StringComparison.Ordinal);
        Assert.Contains("../agent-skills.md", guide, StringComparison.Ordinal);
        Assert.Contains("clarity-scan", guide, StringComparison.Ordinal);
        Assert.Contains("clarity-check", guide, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("README.md")]
    [InlineData("LEAME.md")]
    [InlineData("LEIAME.md")]
    [InlineData("自述文件.md")]
    public void Every_readme_lists_the_clarity_skill_with_the_others(string readme)
    {
        var line = Read(readme).Split('\n').Single(l => l.Contains("agent-skills.md", StringComparison.Ordinal) && l.Contains("narrativetrace-doctor", StringComparison.Ordinal));

        Assert.All(CatalogueNames, name => Assert.Contains($"`{name}`", line, StringComparison.Ordinal));
    }

    [Fact]
    public void The_complete_reference_clarity_section_points_at_the_skill()
    {
        var reference = Read("documentation", "guides", "llms-full.md");
        var section = reference.Split("\n## ").First(s => s.StartsWith("Clarity", StringComparison.Ordinal));

        Assert.Contains($"`{Clarity}`", section, StringComparison.Ordinal);
    }
}
