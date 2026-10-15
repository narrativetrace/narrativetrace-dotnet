// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using NarrativeTrace.Tooling.Frameworks;
using NarrativeTrace.Tooling.Platforms;

namespace NarrativeTrace.SkillsDevTool;

/// <summary>
/// The <c>skills render</c> verb: regenerates <c>.claude/skills/&lt;canonicalName&gt;/SKILL.md</c>,
/// Codex CLI's repository-level <c>.agents/skills/&lt;canonicalName&gt;/SKILL.md</c> and the
/// <c>AGENTS.md</c> managed section for every catalogue skill, plus the carrier tree at
/// <c>src/NarrativeTrace.Skills/skills/</c> (Phase 3 D1/D2) — the ONE physical location both the
/// published <c>NarrativeTrace.Skills</c> package's content files and the
/// <c>dotnet-narrativetrace</c> tool's embedded resources point at, so the two carrier homes are
/// byte-identical by construction rather than by a second render step that could drift (mirrors
/// the Java carrier's shared <c>sourceSets</c> root). Build output, never hand-edited; anti-churn —
/// a byte-identical file is left untouched.
/// </summary>
public static class SkillsRenderCommand
{
    /// <summary>Renders every skill page, the AGENTS.md section and the carrier under <paramref name="repoRoot"/>.</summary>
    public static int Run(string repoRoot, TextWriter output, TextWriter error)
    {
        foreach (var skill in SkillCatalogue.Skills)
        {
            RenderSkillPage(repoRoot, ".claude", skill, ClaudeSkillRenderer.Render(skill, repoRoot));
            RenderSkillPage(repoRoot, ".agents", skill, CodexSkillRenderer.Render(skill, repoRoot));
            RenderCarrierPage(repoRoot, "claude", skill, ClaudeSkillRenderer.Render(skill, repoRoot));
            RenderCarrierPage(repoRoot, "agents", skill, CodexSkillRenderer.Render(skill, repoRoot));
        }

        WriteIfChanged(CarrierPath(repoRoot, "catalogue.json"), CatalogueJsonRenderer.Render(SkillCatalogue.Skills));
        SpliceAgentsMd(repoRoot);
        var marketplacePath = MarketplaceJsonPath(repoRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(marketplacePath)!);
        WriteIfChanged(marketplacePath, MarketplaceJsonRenderer.Render(SkillCatalogue.Marketplace));
        RenderCursorFiles(repoRoot);
        RenderPage(repoRoot, FrameworkTableDocs.LlmsFull, FrameworkTableDocs.RenderLlmsFull);
        RenderPage(repoRoot, FrameworkTableDocs.LlmsTxt, FrameworkTableDocs.RenderLlmsTxt);
        RenderPage(repoRoot, PlatformTableDocs.Page, PlatformTableDocs.Splice);
        output.WriteLine($"Rendered {SkillCatalogue.Skills.Count} skill page(s) (Claude + Codex), the carrier, the AGENTS.md section, the marketplace file, the Cursor plugin files and the framework and platform tables' documentation blocks.");
        return 0;
    }

    /// <summary>
    /// <c>.claude-plugin/marketplace.json</c>, at the repository root — outside <c>.claude/</c> on
    /// purpose: the vendor reads the marketplace manifest from the repository root, and the
    /// plugin it lists is rooted at <c>./.claude</c>, so a marketplace file inside the plugin
    /// would be a directory listing itself.
    /// </summary>
    private static string MarketplaceJsonPath(string repoRoot) =>
        Path.Combine(repoRoot, ".claude-plugin", "marketplace.json");

    /// <summary>The Cursor plugin manifests and the one rule, each at its fixed repository path.</summary>
    private static void RenderCursorFiles(string repoRoot)
    {
        foreach (var (relative, content) in CursorFiles.All())
        {
            var path = Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            WriteIfChanged(path, content);
        }
    }

    private static void RenderSkillPage(string repoRoot, string platformDir, Skill skill, string content)
    {
        var path = Path.Combine(repoRoot, platformDir, "skills", skill.CanonicalName, "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteIfChanged(path, content);
    }

    private static void RenderCarrierPage(string repoRoot, string flavour, Skill skill, string content)
    {
        var path = CarrierPath(repoRoot, Path.Combine(flavour, skill.CanonicalName, "SKILL.md"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteIfChanged(path, content);
    }

    private static string CarrierPath(string repoRoot, string relative) =>
        Path.Combine(repoRoot, "src", "NarrativeTrace.Skills", "skills", relative);

    /// <summary>
    /// Re-renders one documentation page's framework-table block (Phase 6 D1: the table renders into
    /// the docs from the doctor's own rows). A repository without the page has nothing to render.
    /// </summary>
    private static void RenderPage(string repoRoot, string relativePage, Func<string, string> render)
    {
        var path = Path.Combine(repoRoot, relativePage);
        if (File.Exists(path))
        {
            WriteIfChanged(path, render(File.ReadAllText(path)));
        }
    }

    private static void SpliceAgentsMd(string repoRoot)
    {
        var path = Path.Combine(repoRoot, "AGENTS.md");
        var section = AgentsMdRenderer.RenderSection(SkillCatalogue.Skills, SkillCatalogue.Pro);
        var current = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        WriteIfChanged(path, AgentsMdRenderer.Splice(current, section));
    }

    private static void WriteIfChanged(string path, string content)
    {
        if (File.Exists(path) && File.ReadAllText(path) == content)
        {
            return;
        }

        File.WriteAllText(path, content);
    }
}
