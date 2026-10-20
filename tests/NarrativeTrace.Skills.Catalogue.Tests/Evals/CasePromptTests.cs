// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Tests;
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// What the eval runner hands the agent as its first turn. A case document carries the grader's
/// sections (expected trajectory, grading); sending the whole file leaks what is checked, so only
/// the quoted task prompt may leave it. A bare prompt is sent byte for byte.
/// </summary>
public sealed class CasePromptTests
{
    private const string Path = "skills-evals/x/y/prompt.md";

    private static string Document(string afterTaskLine) =>
        "# Case: x / y\n\n**Fixture**: f\n\n**Task prompt** (given to the agent verbatim):\n" + afterTaskLine +
        "\n\n**Expected trajectory**: secret\n\n**Grading**:\n- graders/verify.sh\n";

    [Fact]
    public void A_file_whose_first_line_starts_with_case_is_a_case_document()
    {
        Assert.Equal(CasePromptKind.CaseDocument, CasePrompt.Kind("# Case: a / b\n\nrest"));
    }

    [Theory]
    [InlineData("Set up NarrativeTrace\nwith a hard wrap")]
    [InlineData("\n# Case: not on the first line")]
    [InlineData("#Case: no space")]
    [InlineData("")]
    public void Any_other_file_is_a_bare_prompt(string text)
    {
        Assert.Equal(CasePromptKind.BarePrompt, CasePrompt.Kind(text));
    }

    [Fact]
    public void A_bare_prompt_is_sent_byte_for_byte()
    {
        const string text = "Set up NarrativeTrace\r\nwith hard wraps\n\n  and a trailing newline\n";

        Assert.Equal(text, CasePrompt.TaskPrompt(Path, text));
    }

    [Fact]
    public void A_case_document_sends_only_the_quoted_task_prompt()
    {
        var sent = CasePrompt.TaskPrompt(Path, Document("\n> Check the setup."));

        Assert.Equal("Check the setup.", sent);
    }

    [Fact]
    public void Wrapped_quote_lines_are_joined_with_one_space_and_the_prefix_stripped()
    {
        var sent = CasePrompt.TaskPrompt(Path, Document("\n\n> first line\n> second line\n>third"));

        Assert.Equal("first line second line third", sent);
    }

    [Fact]
    public void An_empty_quote_line_is_a_paragraph_break()
    {
        var sent = CasePrompt.TaskPrompt(Path, Document("> one\n> wrapped\n>\n> two"));

        Assert.Equal("one wrapped\n\ntwo", sent);
    }

    [Fact]
    public void The_quote_ends_at_the_first_line_that_is_not_quoted()
    {
        var sent = CasePrompt.TaskPrompt(Path, Document("> only this\nnot quoted\n> nor this"));

        Assert.Equal("only this", sent);
    }

    [Fact]
    public void A_quote_elsewhere_in_the_document_is_not_the_task_prompt()
    {
        var text = "# Case: x / y\n\n> an early quote\n\n**Task prompt**:\n\n> the real one\n";

        Assert.Equal("the real one", CasePrompt.TaskPrompt(Path, text));
    }

    [Theory]
    [InlineData("# Case: x / y\n\n**Task prompt**: none here\n")]
    [InlineData("# Case: x / y\n\n**Task prompt**:\n\nnot a quote\n")]
    [InlineData("# Case: x / y\n\n> quote but no task line\n")]
    [InlineData("# Case: x / y\n\n**Task prompt**:\n>\n")]
    public void A_case_document_without_a_task_prompt_quote_fails_naming_the_path(string text)
    {
        var e = Assert.Throws<InvalidOperationException>(() => CasePrompt.TaskPrompt(Path, text));

        Assert.Contains(Path, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_prompt_in_the_evals_tree_resolves_without_the_graders_sections()
    {
        var root = TestPaths.RepoRoot();
        var evals = System.IO.Path.Combine(root, "skills-evals");
        var prompts = Directory.EnumerateFiles(evals, "prompt.md", SearchOption.AllDirectories)
            .Where(p => !IsUnder(evals, p, "fixtures") && !IsUnder(evals, p, "runner")).ToList();

        Assert.NotEmpty(prompts);
        foreach (var prompt in prompts)
        {
            var sent = CasePrompt.TaskPrompt(prompt, File.ReadAllText(prompt));
            Assert.False(string.IsNullOrWhiteSpace(sent), prompt);
            foreach (var leak in new[] { "Expected trajectory", "Grading", "graders/" })
            {
                Assert.DoesNotContain(leak, sent, StringComparison.Ordinal);
            }
        }
    }

    private static bool IsUnder(string evals, string path, string top) =>
        System.IO.Path.GetRelativePath(evals, path)
            .StartsWith(top + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
