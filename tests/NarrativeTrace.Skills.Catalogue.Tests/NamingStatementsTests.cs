// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// The three naming statements (§174 item 8) on the .NET AI-facing surfaces: the service is named
/// (<c>unknown_service:dotnet</c> otherwise), spans are named after the interface the proxy wraps, and
/// parameter names are read by reflection except where the metadata carries none.
/// </summary>
public sealed class NamingStatementsTests
{
    private static readonly IReadOnlyList<SkillStep> Steps = AddNarrativeTracingSkill.Definition.Steps;

    private static string Read(string relative) =>
        File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), relative));

    [Fact]
    public void The_install_skill_has_a_name_the_service_step_after_the_first_trace()
    {
        var titles = Steps.Select(s => s.Title).ToList();
        var name = titles.FindIndex(t => t.StartsWith("Name the service", StringComparison.Ordinal));
        var first = titles.FindIndex(t => t.StartsWith("First trace", StringComparison.Ordinal));

        Assert.True(name > first && first >= 0, string.Join(" | ", titles));
        Assert.Contains("NARRATIVETRACE_SERVICE_NAME", Steps[name].Title, StringComparison.Ordinal);
        Assert.Contains("config.service-identity", Steps[name].Verify, StringComparison.Ordinal);
    }

    [Fact]
    public void The_name_the_service_failure_note_names_the_symptom()
    {
        var step = Steps.Single(s => s.Title.StartsWith("Name the service", StringComparison.Ordinal));

        var note = Assert.Single(step.FailureNotes);
        Assert.Contains("unknown_service:dotnet", note.Symptom, StringComparison.Ordinal);
        Assert.Contains("ServiceIdentity", note.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_trace_step_says_how_spans_and_parameters_are_named()
    {
        var notes = Steps.Single(s => s.Title.StartsWith("First trace", StringComparison.Ordinal)).FailureNotes;

        var className = Assert.Single(notes, n => n.Symptom.Contains("named after the interface", StringComparison.Ordinal));
        Assert.Contains("ClassName", className.Fix, StringComparison.Ordinal);
        var parameter = Assert.Single(notes, n => n.Symptom.Contains("arg0", StringComparison.Ordinal));
        Assert.Contains("[Traced(", parameter.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_first_trace_in_llms_txt_names_the_service()
    {
        var llms = Read("documentation/guides/llms.txt");
        var firstTrace = llms[llms.IndexOf("## Install and first trace", StringComparison.Ordinal)..llms.IndexOf("## Runtime support", StringComparison.Ordinal)];

        Assert.Contains("new ServiceIdentity(", firstTrace, StringComparison.Ordinal);
    }

    [Fact]
    public void Llms_txt_states_the_three_naming_facts_plainly()
    {
        var llms = Read("documentation/guides/llms.txt");

        Assert.Contains("unknown_service:dotnet", llms, StringComparison.Ordinal);
        Assert.Contains("NARRATIVETRACE_SERVICE_NAME", llms, StringComparison.Ordinal);
        Assert.Contains("named after the interface", llms, StringComparison.Ordinal);
        Assert.Contains("reads parameter names by reflection", llms, StringComparison.Ordinal);
        Assert.Contains("[Traced(", llms, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("NARRATIVETRACE_SERVICE_NAME")]
    [InlineData("NARRATIVETRACE_SERVICE_VERSION")]
    [InlineData("NARRATIVETRACE_ENVIRONMENT")]
    public void The_configuration_guide_documents_each_identity_variable(string key)
    {
        var guide = Read("documentation/guides/configuration.md");

        Assert.Contains($"| `{key}` |", guide, StringComparison.Ordinal);
    }
}
