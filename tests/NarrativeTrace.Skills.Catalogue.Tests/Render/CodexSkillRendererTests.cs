// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

/// <summary>
/// <see cref="CodexSkillRenderer"/> renders the identical body <see cref="ClaudeSkillRenderer"/>
/// does — Codex CLI's documented <c>SKILL.md</c> format requires only <c>name</c>/<c>description</c>
/// frontmatter, a subset of what this repo already emits — so its own tests are a thin parity
/// check, not a duplicate of <see cref="ClaudeSkillRendererTests"/>'s full coverage.
/// </summary>
public sealed class CodexSkillRendererTests
{
    private static readonly Skill Simple = new(
        CanonicalName: "narrativetrace-example",
        SkillClass: SkillClass.Mechanical,
        Description: "Does a thing.",
        WhenToUse: "When a thing needs doing.",
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps: [new SkillStep("Run it", new CommandStep(["dotnet run"]), Verify: "it prints a trace")],
        Always: [new ReasonedRule("Always run the CLI first", "it is tested")],
        Never: [new ReasonedRule("Never skip verification", "silent drift is the failure mode")]);

    [Fact]
    public void Renders_the_canonical_name_as_frontmatter_name()
    {
        var rendered = CodexSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.StartsWith("---\nname: narrativetrace-example\n", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Renders_byte_identical_to_the_claude_page()
    {
        var codex = CodexSkillRenderer.Render(Simple, TestPaths.RepoRoot());
        var claude = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Equal(claude, codex);
    }

    [Fact]
    public void Rendering_the_real_catalogue_never_throws()
    {
        foreach (var skill in SkillCatalogue.Skills)
        {
            var rendered = CodexSkillRenderer.Render(skill, TestPaths.RepoRoot());
            Assert.NotEmpty(rendered);
        }
    }
}
