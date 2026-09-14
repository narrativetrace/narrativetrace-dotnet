// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;

namespace NarrativeTrace.Cli;

/// <summary>
/// The <c>skills render</c> verb: regenerates <c>.claude/skills/&lt;canonicalName&gt;/SKILL.md</c>
/// and Codex CLI's repository-level <c>.agents/skills/&lt;canonicalName&gt;/SKILL.md</c> for every
/// catalogue skill, and splices the managed section into <c>AGENTS.md</c>. Build output, never
/// hand-edited; anti-churn — a byte-identical file is left untouched.
/// </summary>
public static class SkillsRenderCommand
{
    /// <summary>Renders every skill page and the AGENTS.md section under <paramref name="repoRoot"/>.</summary>
    public static int Run(string repoRoot, TextWriter output, TextWriter error)
    {
        foreach (var skill in SkillCatalogue.Skills)
        {
            RenderSkillPage(repoRoot, ".claude", skill, ClaudeSkillRenderer.Render(skill, repoRoot));
            RenderSkillPage(repoRoot, ".agents", skill, CodexSkillRenderer.Render(skill, repoRoot));
        }

        SpliceAgentsMd(repoRoot);
        output.WriteLine($"Rendered {SkillCatalogue.Skills.Count} skill page(s) (Claude + Codex) and the AGENTS.md section.");
        return 0;
    }

    private static void RenderSkillPage(string repoRoot, string platformDir, Skill skill, string content)
    {
        var path = Path.Combine(repoRoot, platformDir, "skills", skill.CanonicalName, "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        WriteIfChanged(path, content);
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
