// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Cli.Feedback;
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Feedback;

/// <summary>
/// Docs-as-tests for the report-a-problem commands: every command line the skill or the published
/// "Report a problem" section tells a reader to run must be one the verb actually accepts. A renamed
/// flag fails here instead of in a stranger's terminal.
/// </summary>
public sealed class ReportAProblemDocsTests
{
    private const string VerbPrefix = "dotnet tool run dotnet-narrativetrace feedback ";

    private static string LlmsTxt() => File.ReadAllText(Path.Combine(RepoRoot.Find(), "documentation", "guides", "llms.txt"));

    private static string BashBlockOfReportAProblem()
    {
        var section = LlmsTxt().Split("\n## ").Single(s => s.StartsWith("Report a problem", StringComparison.Ordinal));
        return Regex.Match(section, "```bash\n(.*?)```", RegexOptions.Singleline).Groups[1].Value;
    }

    /// <summary>Replaces each <c>&lt;placeholder&gt;</c> with one word (the category with a real one) and splits on whitespace outside quotes.</summary>
    private static List<string> Words(string commandLine) =>
        Regex.Matches(Regex.Replace(commandLine.Replace("<category>", "doctor", StringComparison.Ordinal), "<[^>]+>", "x"), "\"[^\"]*\"|\\S+")
            .Select(match => match.Value.Trim('"'))
            .ToList();

    private static FeedbackArguments Parsed(string command)
    {
        Assert.StartsWith(VerbPrefix, command, StringComparison.Ordinal);
        return FeedbackArguments.Parse(Words(command[VerbPrefix.Length..]));
    }

    [Theory]
    [InlineData(FeedbackCommands.DraftReport)]
    [InlineData(FeedbackCommands.PrintUrl)]
    public void The_skills_commands_are_accepted_by_the_verb(string command)
    {
        var parsed = Parsed(command);

        Assert.Null(parsed.Error);
        Assert.Equal("x", parsed.Language);
        Assert.Equal("doctor", parsed.Category);
    }

    [Fact]
    public void The_published_section_runs_the_skills_own_doctor_command_and_a_command_the_verb_accepts()
    {
        var lines = BashBlockOfReportAProblem().Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(FeedbackCommands.RunDoctorReport, lines[0]);
        Assert.All(lines.Skip(1), line => Assert.Null(Parsed(line).Error));
    }

    [Fact]
    public void The_published_section_names_the_repository_and_the_directory_the_verb_writes_to()
    {
        var section = LlmsTxt();

        Assert.Contains("github.com/narrativetrace/narrativetrace-dotnet", section, StringComparison.Ordinal);
        Assert.Contains(FeedbackCommand.OutputDirectory + "/", section, StringComparison.Ordinal);
    }
}
