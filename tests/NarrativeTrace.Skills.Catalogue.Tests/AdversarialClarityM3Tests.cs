// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// Adversarial checks on the clarity skill and its command constants: near-miss vocabulary inputs, the
/// closed command vocabulary, attribution in rendered pages, and the consistency between the gate's
/// command literals and the report paths the scan writes.
/// </summary>
public sealed class AdversarialClarityM3Tests
{
    private static Skill ClaritySkill => AddNarrativeTraceClaritySkill.Definition;

    private static string ArgumentValue(string command, string flag)
    {
        var tokens = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var index = Array.IndexOf(tokens, flag);
        Assert.True(index >= 0 && index + 1 < tokens.Length, $"'{flag}' has no value in: {command}");
        return tokens[index + 1];
    }

    [Fact]
    public void The_clarity_skill_pre_approves_exactly_the_dotnet_and_git_vocabulary_and_no_other_tool()
    {
        var page = ClaudeSkillRenderer.Render(ClaritySkill, TestPaths.RepoRoot());

        Assert.Equal(["dotnet", "git"], ClaritySkill.PreApprovedTools);
        Assert.Single(Regex.Matches(page, "^allowed-tools: ", RegexOptions.Multiline));
        Assert.Contains("allowed-tools: Bash(dotnet *), Bash(git *)\n", page, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("dotnet build", true)]
    [InlineData("git status", true)]
    [InlineData("dotnet-narrativetrace clarity-check --results x", false)]
    [InlineData("dotnetx build", false)]
    [InlineData("dotnet.exe build", false)]
    [InlineData("gitk --all", false)]
    [InlineData("git-lfs pull", false)]
    public void The_vocabulary_admits_only_the_exact_dotnet_and_git_first_tokens(string command, bool allowed)
    {
        Assert.Equal(allowed, CommandVocabulary.IsAllowed(command));
    }

    [Fact]
    public void Every_clarity_command_constant_is_inside_the_closed_vocabulary_and_none_runs_a_bare_cli()
    {
        var commands = new[]
        {
            ClarityCommands.RestoreTool,
            ClarityCommands.Build,
            ClarityCommands.Scan,
            ClarityCommands.Check,
            ClarityCommands.BuildGate,
        };

        Assert.All(commands, command => Assert.True(CommandVocabulary.IsAllowed(command), command));
        Assert.DoesNotContain(commands, command => CommandVocabulary.FirstToken(command) == "dotnet-narrativetrace");
    }

    [Fact]
    public void The_gate_reads_the_results_file_the_scan_writes_into_the_directory_it_writes_to()
    {
        var scanDirectory = ArgumentValue(ClarityCommands.Scan, "--output-dir");
        var gateInput = ArgumentValue(ClarityCommands.Check, "--results");

        Assert.Equal(ClarityCommands.ReportDirectory, scanDirectory);
        Assert.Equal(ClarityCommands.JsonReportPath, gateInput);
        Assert.Equal(scanDirectory + "/", gateInput[..(scanDirectory.Length + 1)]);
        Assert.Equal("clarity-scan-results.json", gateInput[(scanDirectory.Length + 1)..]);
    }

    [Fact]
    public void The_inspect_step_names_the_three_threshold_properties_the_msbuild_package_declares()
    {
        var inspect = ClaritySkill.Steps.Single(step => step.Title == "Inspect the existing project setup");
        var props = File.ReadAllText(Path.Combine(
            TestPaths.RepoRoot(), "src", "NarrativeTrace.MSBuild", "buildTransitive", "NarrativeTrace.MSBuild.props"));

        foreach (var property in new[] { "ClarityMinScore", "ClarityMaxHighIssues", "ClarityWarnOnly" })
        {
            Assert.Contains(property, inspect.Verify!, StringComparison.Ordinal);
            Assert.Contains($"<{property} ", props, StringComparison.Ordinal);
        }
    }
}
