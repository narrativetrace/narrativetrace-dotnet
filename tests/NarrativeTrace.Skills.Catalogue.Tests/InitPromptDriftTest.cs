// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// Docs-as-tests rule 8, applied to the init prompt: a prompt we publish is a prompt we replay, so
/// the published text and the replayed text may never drift apart. The prompt lives in the English
/// README and its three language mirrors (the prompt itself stays in English on purpose; only the
/// sentence introducing it is translated), <c>documentation/guides/llms.txt</c>, every Tier B case
/// whose <c>prompt.md</c> IS the prompt and nothing else, and the <c>add-narrative-tracing</c>
/// skill's <c>trigger.yaml</c>, which carries the published prompt as one of its mined positive
/// phrasings (Phase 3 milestone 5, D10). This test is what makes every copy safe: they are
/// byte-identical or it fails.
/// </summary>
/// <remarks>
/// The case copies are DISCOVERED, not listed: every <c>prompt.md</c> under the skill's cases that
/// opens with the prompt's first line is a replayed copy, and a copy nobody listed is exactly what
/// a list cannot see (Java's own milestone-4 lesson, when the two registry cases arrived). The four
/// expected paths are asserted to be among the discovered set so discovery can never quietly find
/// nothing.
/// </remarks>
public sealed class InitPromptDriftTest
{
    private static readonly string RepoRoot = TestPaths.RepoRoot();

    private const string FirstLine = "Set up NarrativeTrace in this project";

    private const string CasesDir = "skills-evals/add-narrative-tracing";

    private static readonly string[] PagesWithAFencedBlock =
    {
        "README.md", "LEAME.md", "LEIAME.md", "自述文件.md", "documentation/guides/llms.txt",
    };

    private static readonly string[] ExpectedCasePrompts =
    {
        "skills-evals/add-narrative-tracing/init-prompt-empty-project/prompt.md",
        "skills-evals/add-narrative-tracing/init-prompt-existing-project/prompt.md",
        "skills-evals/add-narrative-tracing/init-prompt-aspnetcore-project/prompt.md",
        "skills-evals/add-narrative-tracing/registry-claude-marketplace/prompt.md",
        "skills-evals/add-narrative-tracing/registry-npx-skills/prompt.md",
    };

    private const string TriggerYamlPage = "skills-evals/add-narrative-tracing/trigger.yaml";

    [Fact]
    public void Every_published_copy_of_the_init_prompt_is_byte_identical()
    {
        var caseCopies = DiscoveredCasePrompts();
        var discovered = caseCopies.Select(p => Path.GetRelativePath(RepoRoot, p).Replace('\\', '/')).ToList();
        foreach (var expected in ExpectedCasePrompts)
            Assert.Contains(expected, discovered);

        var copies = new List<string>();
        foreach (var page in PagesWithAFencedBlock)
            copies.Add(FencedInitPrompt(Path.Combine(RepoRoot, page)));
        foreach (var casePrompt in caseCopies)
            copies.Add(File.ReadAllText(casePrompt).Trim());
        copies.Add(YamlBlockScalarInitPrompt(Path.Combine(RepoRoot, TriggerYamlPage)));

        Assert.All(copies, copy => Assert.Equal(copies[0], copy));
    }

    /// <summary>Every case <c>prompt.md</c> that opens with the prompt's first line — the replayed copies.</summary>
    private static List<string> DiscoveredCasePrompts() =>
        Directory.EnumerateFiles(Path.Combine(RepoRoot, CasesDir), "prompt.md", SearchOption.AllDirectories)
            .Where(p => File.ReadAllText(p).TrimStart().StartsWith(FirstLine, StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void The_replayed_prompt_is_the_one_an_agent_is_told_to_paste()
    {
        var published = FencedInitPrompt(Path.Combine(RepoRoot, "README.md"));

        Assert.StartsWith("Set up NarrativeTrace in this project and show me its first trace.", published);
        Assert.Contains("https://narrativetrace.ai/dotnet/llms.txt", published);
        Assert.Contains("never disable redaction", published);
    }

    /// <summary>The one fenced block on the page that opens with the prompt's first line, without
    /// its fences, trimmed.</summary>
    private static string FencedInitPrompt(string page)
    {
        var lines = File.ReadAllLines(page);
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!lines[i].StartsWith("```", StringComparison.Ordinal)
                || !lines[i + 1].StartsWith("Set up NarrativeTrace in this project", StringComparison.Ordinal))
                continue;
            var body = new List<string>();
            for (var j = i + 1; j < lines.Length && lines[j] != "```"; j++)
                body.Add(lines[j]);
            return string.Join("\n", body).Trim();
        }

        throw new Xunit.Sdk.XunitException($"{page} carries no fenced init-prompt block");
    }

    /// <summary>The YAML block scalar under <c>positive:</c> that opens with the prompt's first
    /// line, dedented by its own indentation, trimmed.</summary>
    private static string YamlBlockScalarInitPrompt(string page)
    {
        const string BlockScalarMarker = "  - |";

        var lines = File.ReadAllLines(page);
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i] != BlockScalarMarker)
                continue;
            var indent = lines[i + 1][..(lines[i + 1].Length - lines[i + 1].TrimStart().Length)];
            if (!lines[i + 1].TrimStart().StartsWith("Set up NarrativeTrace in this project", StringComparison.Ordinal))
                continue;
            var body = new List<string>();
            for (var j = i + 1; j < lines.Length && (lines[j].StartsWith(indent, StringComparison.Ordinal) || lines[j].Length == 0); j++)
                body.Add(lines[j].Length == 0 ? string.Empty : lines[j][indent.Length..]);
            return string.Join("\n", body).Trim();
        }

        throw new Xunit.Sdk.XunitException($"{page} carries no YAML block-scalar init-prompt under 'positive:'");
    }
}
