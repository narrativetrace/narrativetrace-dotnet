// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills;
using NarrativeTrace.Skills.Catalogue;
using NarrativeTrace.Skills.Render;
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Phase 6 D2 as amended: the install skill hands framework wiring to the doctor in ONE step and the
/// page names no framework — which frameworks exist, and how each is wired, is the installed doctor's
/// answer, never text that goes stale on a skill page. Across assemblies on purpose: the table lives in
/// the tooling package, the page in the catalogue.
/// </summary>
public sealed class FrameworkHandOffTests
{
    private static Skill InstallSkill => AddNarrativeTracingSkill.Definition;

    private static SkillStep FrameworkStep =>
        InstallSkill.Steps.Single(step => step.Title == "Wire the frameworks this project already uses");

    [Fact]
    public void The_framework_step_comes_right_after_the_install_and_runs_the_doctor()
    {
        Assert.Equal(1, InstallSkill.Steps.ToList().IndexOf(FrameworkStep));
        var command = Assert.IsType<CommandStep>(FrameworkStep.Body);
        Assert.Equal(["dotnet tool run dotnet-narrativetrace doctor --json"], command.Commands);
        Assert.Equal(SkillCommands.FrameworkFixesApplied, FrameworkStep.Verify);
    }

    [Fact]
    public void The_step_says_apply_every_framework_fix_in_order_and_leave_no_integration_alone()
    {
        Assert.Contains("config.<framework>-*", SkillCommands.FrameworkFixesApplied, StringComparison.Ordinal);
        Assert.Contains("in order", SkillCommands.FrameworkFixesApplied, StringComparison.Ordinal);
        Assert.Contains("no integration shipped is left alone", SkillCommands.FrameworkFixesApplied, StringComparison.Ordinal);
    }

    /// <summary>
    /// The aspnetcore row kept the id it shipped with, which is not <c>config.&lt;framework&gt;-*</c>
    /// shaped; the step names every wiring check id the table has that the pattern would not cover.
    /// </summary>
    [Fact]
    public void The_step_names_every_framework_check_the_config_framework_pattern_does_not_cover()
    {
        var uncovered = FrameworkTable.WiringCheckIds().Where(id => id == "config.registration-pair");

        Assert.All(uncovered, id => Assert.Contains(id, SkillCommands.FrameworkFixesApplied, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void The_rendered_page_names_no_framework_the_doctor_checks_nor_its_package(string flavour)
    {
        var root = RepoRoot.Find();
        var page = flavour == "claude"
            ? ClaudeSkillRenderer.Render(InstallSkill, root)
            : CodexSkillRenderer.Render(InstallSkill, root);

        Assert.All(FrameworkTable.RowsWithWiringChecks(), row =>
        {
            Assert.DoesNotContain(row.Name, page, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(row.Package.PackageId, page, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void Every_framework_check_the_step_hands_off_to_is_one_the_doctor_runs_and_the_install_skill_owns()
    {
        var ids = DoctorChecks.Run(Feedback.DoctorSnapshotFixture.With()).Findings.Select(f => f.Id).ToHashSet();

        Assert.All(FrameworkTable.WiringCheckIds(), id =>
        {
            Assert.Contains(id, ids);
            Assert.Equal("add-narrative-tracing", DoctorFindingSkills.ForCheck(id));
        });
    }

    private static SkillStep FirstTraceStep =>
        InstallSkill.Steps.Single(step => step.Title.StartsWith("First trace", StringComparison.Ordinal));

    [Fact]
    public void The_first_trace_step_keeps_an_existing_entry_point_and_reads_a_real_requests_trace()
    {
        var condition = FirstTraceStep.Condition;

        Assert.NotNull(condition);
        Assert.Contains("already has an application entry point", condition, StringComparison.Ordinal);
        Assert.Contains("do not add a demo", condition, StringComparison.Ordinal);
        Assert.Contains("run the application the way it already runs", condition, StringComparison.Ordinal);
        Assert.Contains("exercise one real boundary", condition, StringComparison.Ordinal);
        Assert.Contains("read that request's trace", condition, StringComparison.Ordinal);
        Assert.Contains("otherwise", condition, StringComparison.Ordinal);
    }

    [Fact]
    public void The_install_step_condition_never_tells_an_existing_application_to_adopt_the_demo_project()
    {
        var steps = InstallSkill.Steps.Where(step => step.Condition is not null).ToList();

        Assert.Contains(FirstTraceStep, steps);
        Assert.All(steps, step => Assert.DoesNotContain("Program.cs", step.Condition!, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("claude")]
    [InlineData("codex")]
    public void The_rendered_first_trace_step_carries_the_when_line_in_both_flavours(string flavour)
    {
        var root = RepoRoot.Find();
        var page = flavour == "claude"
            ? ClaudeSkillRenderer.Render(InstallSkill, root)
            : CodexSkillRenderer.Render(InstallSkill, root);

        Assert.Contains($"**when:** {FirstTraceStep.Condition}", page, StringComparison.Ordinal);
    }
}
