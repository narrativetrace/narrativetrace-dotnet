// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>skills lint</c> verb (Tier A, skill-harness design §4.1): fast, offline, deterministic
/// checks over the catalogue plus a drift check — the rendered <c>.claude/skills/**/SKILL.md</c>
/// and AGENTS.md section on disk must match a fresh render exactly.
/// </summary>
public static class SkillsLintCommand
{
    private static readonly string[] ExcludedDirs = ["bin", "obj", ".git", "node_modules"];

    /// <summary>Runs every lint against <paramref name="repoRoot"/>; returns 0 clean, 1 on any problem.</summary>
    public static int Run(string repoRoot, TextWriter output, TextWriter error)
    {
        var problems = CatalogueProblems(repoRoot).Concat(DriftProblems(repoRoot)).ToList();
        foreach (var problem in problems)
        {
            error.WriteLine($"  - {problem}");
        }

        output.WriteLine(
            problems.Count == 0
                ? $"skills lint: {SkillCatalogue.Skills.Count} skill(s), 0 problems"
                : $"skills lint: {problems.Count} problem(s)");
        return problems.Count == 0 ? 0 : 1;
    }

    private static IEnumerable<string> CatalogueProblems(string repoRoot)
    {
        var skills = SkillCatalogue.Skills;
        var markdownBasenames = RepoMarkdownBasenames(repoRoot);
        return SkillLints.DescriptionBudgetViolations(skills)
            .Select(id => $"{id}: description exceeds the {SkillLints.DescriptionBudget}-char budget")
            .Concat(SkillLints.VocabularyViolations(skills).Select(v => $"vocabulary violation: {v}"))
            .Concat(SkillLints.CitationViolations(skills, markdownBasenames).Select(v => $"citation: {v}"))
            .Concat(FeatureGuideProblems(repoRoot));
    }

    private static IEnumerable<string> FeatureGuideProblems(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "documentation", "feature-guide.md");
        if (!File.Exists(path))
        {
            return ["documentation/feature-guide.md not found — cannot check Pro listing agreement"];
        }

        return SkillLints.ListingsDisagreeingWithFeatureGuide(SkillCatalogue.Pro, File.ReadAllText(path))
            .Select(id => $"{id}: Pro listing status disagrees with documentation/feature-guide.md");
    }

    private static IEnumerable<string> DriftProblems(string repoRoot)
    {
        return SkillCatalogue.Skills.SelectMany(skill => SkillDriftProblems(repoRoot, skill))
            .Concat(AgentsMdDriftProblems(repoRoot));
    }

    private static IEnumerable<string> AgentsMdDriftProblems(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "AGENTS.md");
        var current = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        var section = AgentsMdRenderer.RenderSection(SkillCatalogue.Skills, SkillCatalogue.Pro);
        if (AgentsMdRenderer.Splice(current, section) != current)
        {
            yield return "AGENTS.md: managed skills section is stale — run " +
                "`dotnet run --project src/NarrativeTrace.Cli -- skills render`";
        }
    }

    private static List<string> SkillDriftProblems(string repoRoot, Skill skill)
    {
        string fresh;
        try
        {
            fresh = ClaudeSkillRenderer.Render(skill, repoRoot);
        }
        catch (IOException ex)
        {
            return [$"{skill.CanonicalName}: could not render for comparison — {ex.Message}"];
        }

        var path = Path.Combine(repoRoot, ".claude", "skills", skill.ClaudeSegment, "SKILL.md");
        var onDisk = File.Exists(path) ? File.ReadAllText(path) : null;
        return onDisk == fresh
            ? []
            : [$"{skill.CanonicalName}: .claude/skills/{skill.ClaudeSegment}/SKILL.md is stale — run " +
                "`dotnet run --project src/NarrativeTrace.Cli -- skills render`"];
    }

    private static HashSet<string> RepoMarkdownBasenames(string repoRoot)
    {
        var basenames = new HashSet<string>(StringComparer.Ordinal);
        Walk(repoRoot, basenames);
        return basenames;
    }

    private static void Walk(string dir, HashSet<string> basenames)
    {
        foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
        {
            basenames.Add(Path.GetFileName(file));
        }

        var included = Directory.EnumerateDirectories(dir)
            .Where(sub => !ExcludedDirs.Contains(Path.GetFileName(sub)));
        foreach (var sub in included)
        {
            Walk(sub, basenames);
        }
    }
}
