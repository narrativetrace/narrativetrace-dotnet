// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class ClaudeSkillRendererTests
{
    private static readonly Skill Simple = new(
        CanonicalName: "narrativetrace-example",
        ClaudeSegment: "example",
        SkillClass: SkillClass.Mechanical,
        Description: "Does a thing.",
        WhenToUse: "When a thing needs doing.",
        Fixture: "examples/NarrativeTrace.Examples.SixtySeconds",
        Steps:
        [
            new SkillStep(
                "Run it", new CommandStep(["dotnet run"]), Verify: "it prints a trace",
                Failure: [new FailureNote("nothing prints", "no context was wired", "wire a context")]),
        ],
        Always: [new ReasonedRule("Always run the CLI first", "it is tested")],
        Never: [new ReasonedRule("Never skip verification", "silent drift is the failure mode")]);

    [Fact]
    public void Frontmatter_uses_the_claude_segment_as_name()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.StartsWith("---\nname: example\n", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Frontmatter_carries_description_when_to_use_and_allowed_tools()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Contains("description: \"Does a thing.\"", rendered, StringComparison.Ordinal);
        Assert.Contains("when_to_use: \"When a thing needs doing.\"", rendered, StringComparison.Ordinal);
        Assert.Contains("allowed-tools: dotnet, git", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Body_heading_uses_the_canonical_name()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Contains("\n# narrativetrace-example\n", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Command_step_renders_as_a_fenced_bash_block()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Contains("## 1. Run it", rendered, StringComparison.Ordinal);
        Assert.Contains("```bash\ndotnet run\n```", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Verify_and_failure_lines_are_rendered()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Contains("**verify:** it prints a trace", rendered, StringComparison.Ordinal);
        Assert.Contains(
            "**failure:** nothing prints — no context was wired — fix: wire a context",
            rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Always_and_never_sections_render_reasoned_rules()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Contains("## Always\n- Always run the CLI first (it is tested)", rendered, StringComparison.Ordinal);
        Assert.Contains("## Never\n- Never skip verification (silent drift is the failure mode)",
            rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Flag_renders_as_a_flagged_line()
    {
        var flagged = Simple with
        {
            Steps = [Simple.Steps[0] with { Flag = "unstudied — eval cell pending" }],
        };

        var rendered = ClaudeSkillRenderer.Render(flagged, TestPaths.RepoRoot());

        Assert.Contains("**Flagged:** unstudied — eval cell pending", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Snippet_step_embeds_the_real_file_verbatim()
    {
        var withSnippet = Simple with
        {
            Steps =
            [
                new SkillStep(
                    "First trace",
                    new SnippetStep("examples/NarrativeTrace.Examples.SixtySeconds/Program.cs", "csharp")),
            ],
        };

        var rendered = ClaudeSkillRenderer.Render(withSnippet, TestPaths.RepoRoot());

        // The full repo-relative path, verbatim — the SAME `<!-- snippet: PATH -->` convention
        // documentation/sixty-seconds.md uses for this identical file, so SnippetCheck's marker
        // mechanism can resolve and verify it exactly like every other doc (agent-skills review,
        // 2026-09-13).
        Assert.Contains(
            "<!-- snippet: examples/NarrativeTrace.Examples.SixtySeconds/Program.cs -->",
            rendered, StringComparison.Ordinal);
        Assert.Contains("using NarrativeTrace.Proxy;", rendered, StringComparison.Ordinal);
        Assert.Contains("<!-- /snippet -->", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Rendered_bash_command_blocks_never_leak_this_repositorys_own_paths_or_project_names()
    {
        // A rendered SKILL.md ships into an adopter's own project — Claude Code reads it as literal
        // instructions there. The bash command blocks are what an adopter actually runs, and must
        // never name a path or project that exists only in THIS repository (examples/,
        // src/NarrativeTrace.*, build.sh); the snippet provenance comment beside a code block is a
        // different matter (see Snippet_step_embeds_the_real_file_verbatim) — never rendered by any
        // Markdown viewer, and load-bearing for SnippetCheck's own drift coverage of this page.
        foreach (var skill in SkillCatalogue.Skills)
        {
            var rendered = ClaudeSkillRenderer.Render(skill, TestPaths.RepoRoot());
            foreach (var block in BashCommandBlocks(rendered))
            {
                Assert.DoesNotContain("examples/", block, StringComparison.Ordinal);
                Assert.DoesNotContain("src/NarrativeTrace", block, StringComparison.Ordinal);
                Assert.DoesNotContain("build.sh", block, StringComparison.Ordinal);
            }
        }
    }

    private static IEnumerable<string> BashCommandBlocks(string rendered)
    {
        const string fence = "```bash\n";
        var index = 0;
        while ((index = rendered.IndexOf(fence, index, StringComparison.Ordinal)) >= 0)
        {
            var start = index + fence.Length;
            var end = rendered.IndexOf("```", start, StringComparison.Ordinal);
            yield return rendered[start..end];
            index = end + 3;
        }
    }

    [Fact]
    public void Rendering_the_real_catalogue_never_throws()
    {
        foreach (var skill in SkillCatalogue.Skills)
        {
            var rendered = ClaudeSkillRenderer.Render(skill, TestPaths.RepoRoot());
            Assert.NotEmpty(rendered);
        }
    }
}
