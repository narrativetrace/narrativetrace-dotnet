// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Catalogue;
using Xunit;

namespace NarrativeTrace.Skills.Tests;

/// <summary>
/// <c>add-narrative-tracing</c>'s first-trace step branches on the project's test runner, exactly as it
/// already branches on an entry point (B-92): tests → one existing test leaves the files; no tests → the
/// console, said plainly, and a test runner is never added.
/// </summary>
public sealed class AddNarrativeTracingFirstTraceTests
{
    private static readonly SkillStep FirstTrace = AddNarrativeTracingSkill.Definition.Steps
        .Single(s => s.Title.StartsWith("First trace", StringComparison.Ordinal));

    private static string Condition => FirstTrace.Condition ?? string.Empty;

    [Fact]
    public void A_project_with_tests_runs_one_existing_test_through_the_integration_and_keeps_its_files()
    {
        Assert.Contains("if the project already has tests", Condition, StringComparison.Ordinal);
        Assert.Contains("do not add a demo program: run one existing test through that integration with output on", Condition, StringComparison.Ordinal);
        Assert.Contains("`.md` and `.nt`", Condition, StringComparison.Ordinal);
        Assert.Contains("print the rendered file so the trace is also on the console", Condition, StringComparison.Ordinal);
        Assert.Contains("the step is done when that test's files exist", Condition, StringComparison.Ordinal);
    }

    [Fact]
    public void A_runner_without_an_integration_keeps_its_runner_and_takes_the_console_path()
    {
        Assert.Contains("a project whose runner has no integration keeps that runner and takes the console path below", Condition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_console_path_says_nothing_is_written_to_disk_and_never_adds_a_test_runner()
    {
        Assert.EndsWith(AddNarrativeTracingSkill.FirstTraceWritesNothingWithoutTests, Condition, StringComparison.Ordinal);
        Assert.Contains("nothing is written to disk by this program", Condition, StringComparison.Ordinal);
        Assert.Contains("the application's log is the record", Condition, StringComparison.Ordinal);
        Assert.Contains("this page never adds a test runner or a test to a project that has none", Condition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_step_names_no_framework_the_doctor_checks()
    {
        var text = string.Join('\n', FirstTrace.Title, Condition, FirstTrace.Verify);

        foreach (var framework in new[] { "xUnit", "NUnit", "MSTest", "TUnit", "ASP.NET", "Serilog" })
        {
            Assert.DoesNotContain(framework, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
