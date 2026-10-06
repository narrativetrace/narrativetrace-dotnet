// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;

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
        output.WriteLine($"Rendered {SkillCatalogue.Skills.Count} skill page(s) (Claude + Codex), the carrier and the AGENTS.md section.");
        return 0;
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
