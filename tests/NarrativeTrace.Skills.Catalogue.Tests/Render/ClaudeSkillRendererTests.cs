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
    public void Frontmatter_uses_the_canonical_name_as_name()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.StartsWith("---\nname: narrativetrace-example\n", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Frontmatter_carries_description_when_to_use_and_allowed_tools()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.Contains("description: \"Does a thing.\"", rendered, StringComparison.Ordinal);
        Assert.Contains("when_to_use: \"When a thing needs doing.\"", rendered, StringComparison.Ordinal);
        Assert.Contains("allowed-tools: Bash(dotnet *), Bash(git *)", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void A_skill_declaring_no_allowed_tools_renders_no_allowed_tools_line()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple with { AllowedTools = [] }, TestPaths.RepoRoot());

        Assert.DoesNotContain("allowed-tools", rendered, StringComparison.Ordinal);
        Assert.Contains("when_to_use:", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void A_step_with_no_commands_renders_a_heading_and_no_empty_code_block()
    {
        var skill = Simple with { Steps = [new SkillStep("Ask once", new CommandStep([]), Verify: "asked")] };

        var rendered = ClaudeSkillRenderer.Render(skill, TestPaths.RepoRoot());

        Assert.Contains("## 1. Ask once\n\n**verify:** asked", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("```bash", rendered, StringComparison.Ordinal);
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
    public void Condition_renders_as_a_when_line_between_the_heading_and_the_body()
    {
        var conditional = Simple with
        {
            Steps = [Simple.Steps[0] with { Condition = "only when the project has no entry point" }],
        };

        var rendered = ClaudeSkillRenderer.Render(conditional, TestPaths.RepoRoot());

        Assert.Contains(
            "## 1. Run it\n\n**when:** only when the project has no entry point\n\n```bash",
            rendered,
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_condition_is_rejected(string blank)
    {
        Assert.Throws<ArgumentException>(() => new SkillStep("t", new CommandStep([]), Condition: blank));
        Assert.Throws<ArgumentException>(() => Simple.Steps[0] with { Condition = blank });
    }

    [Fact]
    public void An_unconditional_step_renders_no_when_line()
    {
        var rendered = ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot());

        Assert.DoesNotContain("**when:**", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_renders_the_when_line_the_same_way()
    {
        var conditional = Simple with { Steps = [Simple.Steps[0] with { Condition = "if x" }] };

        Assert.Equal(
            ClaudeSkillRenderer.Render(conditional, TestPaths.RepoRoot()),
            CodexSkillRenderer.Render(conditional, TestPaths.RepoRoot()));
        Assert.Contains("**when:** if x", CodexSkillRenderer.Render(conditional, TestPaths.RepoRoot()), StringComparison.Ordinal);
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

    [Fact]
    public void Reference_sections_render_after_the_steps_and_before_the_rules()
    {
        var skill = Simple with
        {
            Sections =
            [
                new SkillSection("Which flavour answers which question", "| a | b |\n|---|---|\n| 1 | 2 |\n"),
                new SkillSection("Shapes", "- a call made twice"),
            ],
        };

        var rendered = ClaudeSkillRenderer.Render(skill, TestPaths.RepoRoot());

        var steps = rendered.IndexOf("## 1. Run it", StringComparison.Ordinal);
        var table = rendered.IndexOf("## Which flavour answers which question\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n", StringComparison.Ordinal);
        var shapes = rendered.IndexOf("## Shapes\n\n- a call made twice\n\n", StringComparison.Ordinal);
        var always = rendered.IndexOf("## Always", StringComparison.Ordinal);
        Assert.True(steps >= 0 && steps < table && table < shapes && shapes < always, rendered);
    }

    [Fact]
    public void A_skill_without_sections_renders_exactly_as_before()
    {
        Assert.Empty(Simple.Sections);
        Assert.Contains("**failure:** nothing prints", ClaudeSkillRenderer.Render(Simple, TestPaths.RepoRoot()), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("two\nlines")]
    [InlineData("## a heading")]
    [InlineData("carriage\rreturn")]
    public void A_section_heading_is_one_line_of_plain_text(string heading)
    {
        Assert.Throws<ArgumentException>(() => new SkillSection(heading, "body"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n ")]
    public void A_section_body_is_never_blank(string body)
    {
        Assert.Throws<ArgumentException>(() => new SkillSection("Heading", body));
    }
}
