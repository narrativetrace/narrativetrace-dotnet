// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Init;
using Xunit;
using Status = NarrativeTrace.Tooling.Init.PointerReading.PointerStatus;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

/// <summary>
/// <c>config.skills-installed</c> is the check that tells an agent what to run next, so every outcome's
/// message and fix are pinned whole — a mutated sentence, or a fix that loses its second half, fails here.
/// </summary>
public sealed class SkillsInstalledMessageTests
{
    private const string Doctor = "narrativetrace-doctor";
    private const string AddTracing = "add-narrative-tracing";
    private const string Current = "NarrativeTrace.Skills/1.2.3";
    private const string Init = "dotnet tool run dotnet-narrativetrace init --dry-run";

    private static DoctorSnapshot Project(params InstalledSkill[] skills) =>
        DoctorSnapshotFixtures.Empty() with
        {
            CarrierCatalogueSkills = [Doctor, AddTracing],
            ProjectResolvedFamilyVersion = "1.2.3",
            InstalledSkills = skills,
        };

    private static InstalledSkill Ours(string name, string coordinate = Current) =>
        new(SkillFlavour.Agents, name, SkillPresence.Ours, coordinate, "page");

    private static InstalledSkill Theirs(string name) =>
        new(SkillFlavour.Agents, name, SkillPresence.Foreign, "", "");

    [Fact]
    public void A_project_whose_version_is_unknown_passes_with_the_whole_explanation()
    {
        var finding = SkillsInstalledCheck.Run(Project() with { ProjectResolvedFamilyVersion = null });

        Assert.Equal(
            "This project's NarrativeTrace version could not be determined — cannot tell whether the agent skills are current",
            finding.Message);
    }

    [Fact]
    public void A_current_install_passes_with_its_count_and_version_and_no_pointer_clause()
    {
        var finding = SkillsInstalledCheck.Run(Project(Ours(Doctor), Ours(AddTracing)));

        Assert.Equal("All 2 agent skill(s) are installed and current with this project's NarrativeTrace 1.2.3", finding.Message);
    }

    [Fact]
    public void Several_current_pointers_are_listed_in_order_after_a_semicolon()
    {
        var snapshot = Project(Ours(Doctor), Ours(AddTracing)) with
        {
            PointerReadings =
            [
                new PointerReading("cursor", "Cursor", ".cursor/rules/narrativetrace.mdc", Status.Section, Current),
                new PointerReading("replit", "Replit", "replit.md", Status.Section, Current),
            ],
        };

        Assert.Equal(
            "All 2 agent skill(s) are installed and current with this project's NarrativeTrace 1.2.3"
            + "; Cursor pointer present and current, Replit pointer present and current",
            SkillsInstalledCheck.Run(snapshot).Message);
    }

    [Fact]
    public void Nothing_installed_fails_with_the_command_and_nobody_blamed()
    {
        var finding = SkillsInstalledCheck.Run(Project());

        Assert.Equal("The NarrativeTrace agent skills are not installed under .agents/skills/", finding.Message);
        Assert.Equal($"Run {Init}, read the diff, then run it without the flag.", finding.Fix);
    }

    [Fact]
    public void Somebody_elses_directory_is_named_and_the_fix_adds_the_registry_case()
    {
        var finding = SkillsInstalledCheck.Run(Project(Theirs(Doctor)));

        Assert.Equal(
            "The NarrativeTrace agent skills are not installed under .agents/skills/ — narrativetrace-doctor is there, not ours",
            finding.Message);
        Assert.StartsWith($"Run {Init}, read the diff, then run it without the flag. Pages that are there", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void A_stale_install_names_the_stamp_and_the_whole_fix()
    {
        var finding = SkillsInstalledCheck.Run(Project(Ours(Doctor, "NarrativeTrace.Skills/0.0.1"), Ours(AddTracing)));

        Assert.Equal("Agent skills installed from NarrativeTrace.Skills/0.0.1, project resolves 1.2.3", finding.Message);
        Assert.Equal(
            $"Re-run {Init} and apply it — the installed pages describe a different release of NarrativeTrace than this project uses.",
            finding.Fix);
    }

    [Fact]
    public void Several_stale_stamps_are_listed_in_ordinal_order()
    {
        var finding = SkillsInstalledCheck.Run(Project(
            Ours(Doctor, "NarrativeTrace.Skills/0.0.9"), Ours(AddTracing, "NarrativeTrace.Skills/0.0.1")));

        Assert.Equal(
            "Agent skills installed from NarrativeTrace.Skills/0.0.1, NarrativeTrace.Skills/0.0.9, project resolves 1.2.3",
            finding.Message);
    }

    [Fact]
    public void A_partial_install_names_what_is_missing_and_what_is_there_but_not_ours()
    {
        var finding = SkillsInstalledCheck.Run(Project(Ours(Doctor), Theirs(AddTracing)));

        Assert.Equal(
            "The agent skills are installed, but this carrier's add-narrative-tracing (there, but not ours) is missing",
            finding.Message);
        Assert.Equal(
            $"Run {Init} to add the missing page(s). A page identical to this release's is adopted as it stands;"
            + " --force is only for a directory somebody else really owns.",
            finding.Fix);
    }

    [Fact]
    public void A_wholly_absent_skill_is_named_without_the_not_ours_note()
    {
        var finding = SkillsInstalledCheck.Run(Project(Ours(Doctor)));

        Assert.Equal("The agent skills are installed, but this carrier's add-narrative-tracing is missing", finding.Message);
    }

    [Fact]
    public void Pointer_problems_are_joined_and_the_fix_explains_adoption_and_flags()
    {
        var snapshot = Project(Ours(Doctor), Ours(AddTracing)) with
        {
            PointerReadings =
            [
                new PointerReading("cursor", "Cursor", ".cursor/rules/narrativetrace.mdc", Status.Foreign, ""),
                new PointerReading("replit", "Replit", "replit.md", Status.Linked, ""),
                new PointerReading("antigravity", "Antigravity", "x.md", Status.Absent, ""),
            ],
        };

        var finding = SkillsInstalledCheck.Run(snapshot);

        Assert.Equal(
            "The agent skills are installed, but Cursor pointer .cursor/rules/narrativetrace.mdc exists but has no NarrativeTrace section of ours"
            + "; Replit pointer replit.md is behind a symbolic link, and nothing is written through one"
            + "; Antigravity pointer x.md is missing",
            finding.Message);
        Assert.Equal(
            $"Run {Init}, read the diff, then run it without the flag. A rule file or section "
            + "that is identical to this release's is adopted as it stands; where the file exists and "
            + "has no section of ours, add --write-existing; a link must be removed by hand.",
            finding.Fix);
    }

    [Fact]
    public void Every_outcome_points_at_the_validation_page()
    {
        Assert.All(
            new[] { Project(), Project(Ours(Doctor)), Project(Ours(Doctor), Ours(AddTracing)) },
            snapshot => Assert.Equal(DoctorDocUrls.ValidateInstallation, SkillsInstalledCheck.Run(snapshot).DocUrl));
    }
}
