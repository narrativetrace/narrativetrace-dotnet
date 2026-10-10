// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Tooling.Feedback;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Feedback;

/// <summary>
/// Holds the issue form and the URL the verb builds together. GitHub silently ignores a URL
/// parameter naming a field id the form does not have, so a renamed field opens an empty form and
/// fails nowhere else.
/// </summary>
public sealed class IssueFormContractTests
{
    private static readonly string[] NarrativeAndBodyIds = ["did", "happened", "expected", "report", "reviewed"];

    private static readonly string[] NotFieldIds = ["template", "title", "labels"];

    private static string Form() => File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "IssueForm", PublicRepository.Form));

    private static List<string> FormFieldIds() =>
        Regex.Matches(Form(), @"^\s+id:\s*(\S+)\s*$", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();

    private static List<string> UrlFieldIds()
    {
        var url = IssueFormUrl.Of(FeedbackFixtures.ValidReport());
        return url[(url.IndexOf('?') + 1)..].Split('&')
            .Select(pair => pair[..pair.IndexOf('=')])
            .Except(NotFieldIds)
            .ToList();
    }

    [Fact]
    public void Every_field_the_url_prefills_exists_in_the_form()
    {
        var formIds = FormFieldIds();

        Assert.All(UrlFieldIds(), id => Assert.Contains(id, formIds));
    }

    [Fact]
    public void The_form_has_a_box_for_each_part_of_the_narrative_and_the_pasted_body()
    {
        var formIds = FormFieldIds();

        Assert.All(NarrativeAndBodyIds, id => Assert.Contains(id, formIds));
    }

    [Fact]
    public void The_category_dropdown_offers_exactly_the_verbs_categories()
    {
        var offered = Regex.Match(Form(), @"id: category.*?options:\s*\n((?:\s+- \S+\s*\n)+)", RegexOptions.Singleline)
            .Groups[1].Value
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line[2..])
            .ToList();

        Assert.Equal(
            [FeedbackCategory.Prompt.Id, FeedbackCategory.Skill.Id, FeedbackCategory.Doctor.Id, FeedbackCategory.Library.Id],
            offered);
    }

    [Fact]
    public void The_form_labels_every_report_from_agent()
    {
        Assert.Matches(@"(?m)^labels:\s*\n\s+- from-agent\s*$", Form());
    }

    [Fact]
    public void The_form_is_english_with_a_language_field_and_requires_the_attestation()
    {
        var form = Form();

        Assert.Contains("id: language", form, StringComparison.Ordinal);
        Assert.Matches(@"(?s)id: reviewed.*required: true", form);
    }

    [Fact]
    public void The_form_names_no_agent_vendor()
    {
        Assert.DoesNotMatch(@"(?i)claude|anthropic|codex|openai|gemini|copilot", Form());
    }
}
