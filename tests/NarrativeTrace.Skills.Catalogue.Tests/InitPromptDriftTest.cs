// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// Docs-as-tests rule 8, applied to the init prompt: a prompt we publish is a prompt we replay, so
/// the published text and the replayed text may never drift apart. The prompt lives in eight places
/// — the English README and its three language mirrors (the prompt itself stays in English on
/// purpose; only the sentence introducing it is translated), <c>documentation/guides/llms.txt</c>,
/// the two Tier B cases whose <c>prompt.md</c> IS the prompt and nothing else, and the
/// <c>add-narrative-tracing</c> skill's <c>trigger.yaml</c>, which carries the published prompt as
/// one of its mined positive phrasings (Phase 3 milestone 5, D10 — closes the eighth-copy gap Java
/// found in its own milestone 5). This test is what makes "eight copies" safe: they are byte-identical
/// or it fails.
/// </summary>
public sealed class InitPromptDriftTest
{
    private static readonly string RepoRoot = TestPaths.RepoRoot();

    private static readonly string[] PagesWithAFencedBlock =
    {
        "README.md", "LEAME.md", "LEIAME.md", "自述文件.md", "documentation/guides/llms.txt",
    };

    private static readonly string[] CasesWhosePromptIsTheWholeFile =
    {
        "skills-evals/add-narrative-tracing/init-prompt-empty-project/prompt.md",
        "skills-evals/add-narrative-tracing/init-prompt-existing-project/prompt.md",
    };

    private const string TriggerYamlPage = "skills-evals/add-narrative-tracing/trigger.yaml";

    [Fact]
    public void Every_published_copy_of_the_init_prompt_is_byte_identical()
    {
        var copies = new List<string>();
        foreach (var page in PagesWithAFencedBlock)
            copies.Add(FencedInitPrompt(Path.Combine(RepoRoot, page)));
        foreach (var casePrompt in CasesWhosePromptIsTheWholeFile)
            copies.Add(File.ReadAllText(Path.Combine(RepoRoot, casePrompt)).Trim());
        copies.Add(YamlBlockScalarInitPrompt(Path.Combine(RepoRoot, TriggerYamlPage)));

        Assert.Equal(8, copies.Count);
        Assert.All(copies, copy => Assert.Equal(copies[0], copy));
    }

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
