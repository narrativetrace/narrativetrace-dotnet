// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders a <see cref="Skill"/> to the <c>SKILL.md</c> body Codex CLI discovers under its
/// repository-level <c>.agents/skills/&lt;canonicalName&gt;/</c> directory — Codex scans
/// <c>.agents/skills</c> from the current working directory up to the repository root, alongside
/// a user-level <c>~/.agents/skills</c>, an admin level, and its own bundled skills
/// (developers.openai.com/codex/skills, redirects to learn.chatgpt.com/docs/build-skills;
/// developers.openai.com/codex/concepts/customization, redirects to
/// learn.chatgpt.com/docs/customization/overview; both fetched 2026-09-13). A skill directory is
/// a <c>SKILL.md</c> file with YAML frontmatter that must include <c>name</c> and
/// <c>description</c> — a strict subset of what <see cref="ClaudeSkillRenderer"/> already emits
/// (it adds <c>when_to_use</c> and <c>allowed-tools</c>, neither documented nor prohibited by
/// Codex), so the identical page body renders unchanged here; only the directory it lands in
/// differs. Build output, never hand-edited — regenerated from the catalogue.
/// </summary>
public static class CodexSkillRenderer
{
    /// <summary>Renders the full <c>SKILL.md</c> content for <paramref name="skill"/>.</summary>
    public static string Render(Skill skill, string repoRoot) => ClaudeSkillRenderer.Render(skill, repoRoot);
}
