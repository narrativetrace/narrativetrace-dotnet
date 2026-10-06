// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders a <see cref="Skill"/> to the <c>SKILL.md</c> body Claude Code discovers under
/// <c>.claude/skills/&lt;canonicalName&gt;/</c>. Build output, never hand-edited — regenerated
/// from the catalogue. The identical body also renders Codex CLI's repository-level
/// <c>.agents/skills/&lt;canonicalName&gt;/SKILL.md</c> — see <see cref="CodexSkillRenderer"/>.
/// </summary>
public static class ClaudeSkillRenderer
{
    private static readonly JsonSerializerOptions FrontmatterStringOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Renders the full <c>SKILL.md</c> content for <paramref name="skill"/>.</summary>
    public static string Render(Skill skill, string repoRoot)
    {
        var builder = new StringBuilder();
        AppendFrontmatter(builder, skill);
        builder.Append("\n# ").Append(skill.CanonicalName).Append("\n\n");
        AppendSteps(builder, skill, repoRoot);
        AppendRules(builder, "Always", skill.Always);
        AppendRules(builder, "Never", skill.Never);
        return builder.ToString();
    }

    private static void AppendFrontmatter(StringBuilder builder, Skill skill)
    {
        builder.Append("---\n");
        builder.Append("name: ").Append(skill.CanonicalName).Append('\n');
        builder.Append("description: ").Append(FrontmatterString(skill.Description)).Append('\n');
        if (skill.WhenToUse is not null)
        {
            builder.Append("when_to_use: ").Append(FrontmatterString(skill.WhenToUse)).Append('\n');
        }

        builder.Append("allowed-tools: ").Append(string.Join(", ", CommandVocabulary.AllowedTools)).Append('\n');
        builder.Append("---\n");
    }

    private static string FrontmatterString(string value)
    {
        return JsonSerializer.Serialize(value, FrontmatterStringOptions);
    }

    private static void AppendSteps(StringBuilder builder, Skill skill, string repoRoot)
    {
        for (var i = 0; i < skill.Steps.Count; i++)
        {
            AppendStep(builder, i + 1, skill.Steps[i], repoRoot);
        }
    }

    private static void AppendStep(StringBuilder builder, int number, SkillStep step, string repoRoot)
    {
        builder.Append("## ").Append(number).Append(". ").Append(step.Title).Append("\n\n");
        AppendBody(builder, step.Body, repoRoot);
        if (step.Verify is not null)
        {
            builder.Append("**verify:** ").Append(step.Verify).Append("\n\n");
        }

        AppendFailures(builder, step.FailureNotes);
        if (step.Flag is not null)
        {
            builder.Append("**Flagged:** ").Append(step.Flag).Append("\n\n");
        }
    }

    private static void AppendBody(StringBuilder builder, IStepBody body, string repoRoot)
    {
        if (body is CommandStep commands)
        {
            AppendCommands(builder, commands);
        }
        else if (body is SnippetStep snippet)
        {
            AppendSnippet(builder, snippet, repoRoot);
        }
    }

    private static void AppendCommands(StringBuilder builder, CommandStep commands)
    {
        builder.Append("```bash\n");
        foreach (var command in commands.Commands)
        {
            builder.Append(command).Append('\n');
        }

        builder.Append("```\n\n");
    }

    private static void AppendSnippet(StringBuilder builder, SnippetStep snippet, string repoRoot)
    {
        // The repo-relative path in the marker, verbatim — the SAME `<!-- snippet: PATH -->`
        // convention documentation/sixty-seconds.md uses for this identical file, on purpose
        // (agent-skills review, 2026-09-13, superseding an earlier same-day attempt to shorten
        // this to a bare filename): SnippetCheck's own marker mechanism resolves and verifies this
        // exact comment, the same way it already does for llms.txt and every other doc — a
        // shortened marker here would silently drop this page out of that coverage. The comment is
        // never rendered by anything a reader sees (a Markdown viewer hides it, and it carries no
        // instruction), so it costs an adopter nothing; the bash commands beside it — the surface
        // an adopter actually reads and runs — carry no repo-internal path, and a test guards that.
        builder.Append("<!-- snippet: ").Append(snippet.Path);
        if (snippet.Region is not null)
        {
            builder.Append(" region=").Append(snippet.Region);
        }

        if (snippet.Mask is not null)
        {
            builder.Append(" mask=").Append(snippet.Mask);
        }

        builder.Append(" -->\n```").Append(snippet.Language).Append('\n');
        builder.Append(SnippetResolver.Resolve(snippet, repoRoot)).Append('\n');
        builder.Append("```\n<!-- /snippet -->\n\n");
    }

    private static void AppendFailures(StringBuilder builder, IReadOnlyList<FailureNote> failures)
    {
        foreach (var failure in failures)
        {
            builder.Append("**failure:** ").Append(failure.Symptom).Append(" — ")
                .Append(failure.Cause).Append(" — fix: ").Append(failure.Fix).Append("\n\n");
        }
    }

    private static void AppendRules(StringBuilder builder, string heading, IReadOnlyList<ReasonedRule> rules)
    {
        builder.Append("## ").Append(heading).Append('\n');
        foreach (var rule in rules)
        {
            builder.Append("- ").Append(rule.Rule).Append(" (").Append(rule.Reason).Append(")\n");
        }

        builder.Append('\n');
    }
}
