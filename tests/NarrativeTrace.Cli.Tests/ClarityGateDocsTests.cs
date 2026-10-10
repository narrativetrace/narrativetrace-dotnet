// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Docs-as-tests for the scan → gate pairing. <c>clarity-check</c> SKIPS a missing results file with
/// exit 0 (so a project with no scan yet does not break CI), which makes a page that points the gate at
/// the wrong file the worst kind of wrong: the CI step goes green and checks nothing. Every code block
/// that runs both verbs must therefore hand the gate the file the scan really writes — the name is read
/// from <see cref="ClarityScanCommand.ResultsFileName"/>, never retyped here.
/// </summary>
public sealed partial class ClarityGateDocsTests
{
    public static TheoryData<string> Pages() =>
    [
        "documentation/guides/clarity.md",
        "documentation/guides/installation.md",
        "documentation/guides/llms.txt",
        "documentation/guides/es/guia-de-claridad.md",
        "documentation/guides/es/guia-de-instalacion.md",
        "documentation/guides/pt-BR/guia-de-clareza.md",
        "documentation/guides/pt-BR/guia-de-instalacao.md",
        "documentation/guides/zh-CN/清晰度指南.md",
        "documentation/guides/zh-CN/安装指南.md",
    ];

    [Theory]
    [MemberData(nameof(Pages))]
    public void A_block_that_scans_then_checks_hands_the_gate_the_file_the_scan_writes(string page)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot.Find(), page));
        var blocks = Fences().Matches(text).Select(match => match.Groups[1].Value)
            .Concat(text.Split('\n'))
            .Select(block => block.Replace("\\\n", " ", StringComparison.Ordinal))
            .Where(block => block.Contains("clarity-scan ", StringComparison.Ordinal) && block.Contains("clarity-check ", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(blocks);
        Assert.All(blocks, block =>
        {
            var outputDir = OutputDir().Match(block) is { Success: true } dir ? dir.Groups[1].Value.TrimEnd('/') : ".";
            var expected = outputDir == "." ? ClarityScanCommand.ResultsFileName : $"{outputDir}/{ClarityScanCommand.ResultsFileName}";
            Assert.Equal(expected, Results().Match(block).Groups[1].Value);
        });
    }

    [GeneratedRegex("```(?:bash|text)\n(.*?)```", RegexOptions.Singleline)]
    private static partial Regex Fences();

    [GeneratedRegex(@"clarity-scan\b[^\n]*?--output-dir ([^\s`]+)")]
    private static partial Regex OutputDir();

    [GeneratedRegex(@"clarity-check --results ([^\s`]+)")]
    private static partial Regex Results();
}
