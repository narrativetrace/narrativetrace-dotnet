// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;
using Action = NarrativeTrace.Tooling.Init.Action;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Every refusal and advisory the install plan prints is read by a person or an agent deciding what to
/// do next, so each one is pinned to its whole text, and the one place that must refuse rather than
/// append is pinned to refusing.
/// </summary>
public sealed class InitPlannerMessageTest : IDisposable
{
    private const string Target = "../../.agents/skills/narrativetrace-doctor";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-initmsg-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public InitPlannerMessageTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "1.2.3");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static ProjectState.Builder Project() => ProjectState.NewBuilder();

    private InitPlan Plan(ProjectState state, bool writeExisting = false) =>
        InitPlanner.Plan(state, _carrier, InitOptions.Defaults().WithWriteExisting(writeExisting));

    private static Refuse RefusalOn(InitPlan plan, string path) =>
        Assert.IsType<Refuse>(plan.Actions.First(action => action.Path == path));

    [Fact]
    public void Planning_without_a_state_a_carrier_or_options_says_what_it_needs()
    {
        var error = Assert.Throws<ArgumentException>(() => InitPlanner.Plan(null!, _carrier, InitOptions.Defaults()));

        Assert.Equal("planning needs a project state, a carrier and options", error.Message);
    }

    [Fact]
    public void A_project_on_another_version_gets_one_note_naming_both_and_the_command()
    {
        var plan = Plan(Project().ResolvedFamilyVersion("9.9.9").Build());

        Assert.Equal(
            [
                "note: this CLI carries NarrativeTrace.Skills/1.2.3, but the project resolves NarrativeTrace 9.9.9"
                + " — run `dotnet tool run dotnet-narrativetrace init` from a CLI at 9.9.9 to install matching skills.",
            ],
            plan.Warnings);
    }

    [Fact]
    public void A_project_on_the_same_version_gets_no_note()
    {
        Assert.Empty(Plan(Project().ResolvedFamilyVersion("1.2.3").Build()).Warnings);
    }

    [Fact]
    public void A_linked_install_root_is_refused_with_the_whole_instruction()
    {
        var plan = Plan(Project().LinkedInstallRoot(SkillFlavour.Agents, "elsewhere").Build());

        Assert.Equal(
            ".agents/skills is a symbolic link to elsewhere — every skill of this flavour would be written"
            + " through it; remove the link, or run the install where it points",
            RefusalOn(plan, ".agents/skills").Reason);
    }

    [Fact]
    public void A_linked_skill_that_reaches_no_page_is_refused_with_the_whole_instruction()
    {
        var linked = new InstalledSkill(SkillFlavour.Claude, Carriers.Doctor, SkillPresence.LinkedDirectory, "", "", Target);

        var plan = Plan(Project().AddInstalledSkill(linked).Build());

        Assert.Equal(
            ".claude/skills/narrativetrace-doctor is a symbolic link to " + Target
            + ", and there is no page of narrativetrace's at the other end — remove the link and run the install again",
            RefusalOn(plan, ".claude/skills/narrativetrace-doctor").Reason);
    }

    [Fact]
    public void A_foreign_skill_directory_is_refused_without_force_with_the_whole_instruction()
    {
        var foreign = new InstalledSkill(SkillFlavour.Agents, Carriers.Doctor, SkillPresence.Foreign, "", "theirs\n");

        var plan = Plan(Project().AddInstalledSkill(foreign).Build());

        Assert.Equal(
            ".agents/skills/narrativetrace-doctor was not installed by narrativetrace — re-run with --force to"
            + " overwrite this skill, or move the directory aside",
            RefusalOn(plan, ".agents/skills/narrativetrace-doctor").Reason);
    }

    [Fact]
    public void An_existing_agents_file_without_a_section_is_refused_with_the_flag_to_use()
    {
        var plan = Plan(Project().AgentsMd("# Agents\n").Build());

        Assert.Equal(
            "AGENTS.md exists and carries no NarrativeTrace section — re-run with --write-existing to append one",
            RefusalOn(plan, "AGENTS.md").Reason);
    }

    [Fact]
    public void An_agents_file_with_a_broken_marker_is_refused_with_the_scan_problems_after_its_path()
    {
        var plan = Plan(Project().AgentsMd("<!-- narrativetrace:start -->\nbody\n").Build(), writeExisting: true);

        var reason = RefusalOn(plan, "AGENTS.md").Reason;

        Assert.StartsWith("AGENTS.md: ", reason, StringComparison.Ordinal);
        Assert.Contains("line 1", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void An_agents_file_with_two_sections_is_refused_naming_the_lines_and_the_rule()
    {
        var twice =
            "<!-- narrativetrace:start -->\na\n<!-- narrativetrace:end -->\n"
            + "<!-- narrativetrace:start -->\nb\n<!-- narrativetrace:end -->\n";

        var plan = Plan(Project().AgentsMd(twice).Build(), writeExisting: true);

        Assert.Equal(
            "AGENTS.md carries two or more NarrativeTrace sections ([line 1, line 4]) — leave exactly one",
            RefusalOn(plan, "AGENTS.md").Reason);
    }

    [Fact]
    public void A_claude_file_that_does_not_import_agents_is_refused_with_the_flag_to_use()
    {
        var plan = Plan(Project().ClaudeMd("# Project\n").Build());

        Assert.Equal(
            "CLAUDE.md exists and does not import AGENTS.md — re-run with --write-existing to add the one-line import",
            RefusalOn(plan, "CLAUDE.md").Reason);
    }

    [Fact]
    public void An_unfinished_fence_is_refused_with_the_whole_explanation()
    {
        var plan = Plan(Project().AgentsMd("# Agents\n\n```\nnot closed\n").Build(), writeExisting: true);

        Assert.Equal(
            "AGENTS.md ends inside an unfinished fenced code block — close the fence, and anything appended"
            + " after it will be read as text rather than as code",
            RefusalOn(plan, "AGENTS.md").Reason);
    }

    [Fact]
    public void A_vendor_rule_file_with_no_section_is_refused_never_appended_to_even_when_writing_existing_is_allowed()
    {
        var plan = Plan(Project().MarkedRuleFile(".cursorrules", "# my rules\n").Build(), writeExisting: true);

        Assert.IsType<Refuse>(plan.Actions.First(action => action.Path == ".cursorrules"));
        Assert.DoesNotContain(plan.Actions, action => action is AppendBlock { Path: ".cursorrules" });
    }

    [Fact]
    public void A_cursor_rule_that_is_not_ours_is_refused_with_the_whole_instruction()
    {
        var plan = Plan(Project().DetectedPlatform("cursor").PointerFile(".cursor/rules/narrativetrace.mdc", "mine\n").Build());

        Assert.Equal(
            ".cursor/rules/narrativetrace.mdc was not written by narrativetrace (it has no single NarrativeTrace"
            + " section) — move it aside and run the install again",
            RefusalOn(plan, ".cursor/rules/narrativetrace.mdc").Reason);
    }
}
