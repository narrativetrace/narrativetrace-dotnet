// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

public sealed class SkillsInstalledCheckTests
{
    private const string Doctor = "narrativetrace-doctor";
    private const string AddTracing = "add-narrative-tracing";
    private const string CurrentVersion = "1.2.3";
    private const string Current = "NarrativeTrace.Skills/" + CurrentVersion;
    private const string Older = "NarrativeTrace.Skills/0.0.1";
    private static readonly string[] Catalogue = [Doctor, AddTracing];

    private static DoctorSnapshot Project(params InstalledSkill[] skills) =>
        DoctorSnapshotFixtures.Empty() with
        {
            CarrierCatalogueSkills = Catalogue,
            ProjectResolvedFamilyVersion = CurrentVersion,
            InstalledSkills = skills,
        };

    private static InstalledSkill Installed(string name, string coordinate, SkillFlavour flavour = SkillFlavour.Agents) =>
        new(flavour, name, SkillPresence.Ours, coordinate, "page");

    private static InstalledSkill Theirs(string name, SkillFlavour flavour = SkillFlavour.Agents) =>
        new(flavour, name, SkillPresence.Foreign, "", "");

    private static InstalledSkill NotADirectory(string name) =>
        new(SkillFlavour.Agents, name, SkillPresence.NotADirectory, "", "");

    [Fact]
    public void Passes_when_every_catalogue_skill_is_installed_from_the_resolved_version()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Current), Installed(AddTracing, Current)));

        Assert.Equal("config.skills-installed", finding.Id);
        Assert.True(finding.Passed);
        Assert.Contains("2", finding.Message, StringComparison.Ordinal);
        Assert.Contains(CurrentVersion, finding.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A project whose NarrativeTrace version cannot be determined is not a defect. This is the .NET
    /// analogue of Java's "offline is not a defect": here the uncertainty is the PROJECT's version,
    /// not the carrier's, because the carrier is always the running CLI's own (D9).
    /// </summary>
    [Fact]
    public void Passes_and_says_so_when_the_projects_family_version_cannot_be_determined()
    {
        var snapshot = DoctorSnapshotFixtures.Empty() with
        {
            CarrierCatalogueSkills = Catalogue,
            ProjectResolvedFamilyVersion = null,
            InstalledSkills = [Installed(Doctor, Older)],
        };

        var finding = SkillsInstalledCheck.Run(snapshot);

        Assert.True(finding.Passed);
        Assert.Contains("could not be determined", finding.Message, StringComparison.Ordinal);
        Assert.Contains("cannot tell", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fails_naming_the_install_command_when_nothing_is_installed()
    {
        var finding = SkillsInstalledCheck.Run(Project());

        Assert.False(finding.Passed);
        Assert.Contains("not installed", finding.Message, StringComparison.Ordinal);
        Assert.Contains("dotnet tool run dotnet-narrativetrace init --dry-run", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void Where_no_skill_directory_exists_at_all_nobody_else_is_blamed()
    {
        var finding = SkillsInstalledCheck.Run(Project());

        Assert.Contains("not installed", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not ours", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fails_naming_both_versions_when_the_installed_pages_are_stale()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Older), Installed(AddTracing, Older)));

        Assert.False(finding.Passed);
        Assert.Contains($"installed from {Older}", finding.Message, StringComparison.Ordinal);
        Assert.Contains($"resolves {CurrentVersion}", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Re-run", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void Fails_naming_the_missing_skill_when_only_some_are_installed()
    {
        var finding = SkillsInstalledCheck.Run(Project(Installed(Doctor, Current)));

        Assert.False(finding.Passed);
        Assert.Contains(AddTracing, finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Doctor, finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A directory of somebody else's is reported, and never counted as one of ours.</summary>
    [Fact]
    public void A_foreign_directory_at_a_skills_path_is_reported_and_not_counted()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Current), Theirs(AddTracing)));

        Assert.False(finding.Passed);
        Assert.Contains(AddTracing, finding.Message, StringComparison.Ordinal);
        Assert.Contains("not ours", finding.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Doctor, finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A project's own skills are its own business — only the catalogue's names are this check's.</summary>
    [Fact]
    public void A_skill_directory_the_catalogue_never_named_is_ignored()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Current), Installed(AddTracing, Current), Theirs("deploy-to-staging")));

        Assert.True(finding.Passed);
        Assert.DoesNotContain("deploy-to-staging", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// <c>.agents/skills/</c> is the layout every project gets; the vendor copy is written only where a
    /// project is detected as that vendor's, so a vendor-only install is an install that did not happen.
    /// </summary>
    [Fact]
    public void The_vendor_copy_alone_is_not_an_install()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Current, SkillFlavour.Claude)));

        Assert.False(finding.Passed);
        Assert.Contains("not installed", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>Something that is not a directory at all sits where a skill belongs: missing, not ours.</summary>
    [Fact]
    public void A_file_where_a_skill_directory_belongs_counts_as_missing()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Current), NotADirectory(AddTracing)));

        Assert.False(finding.Passed);
        Assert.Contains(AddTracing, finding.Message, StringComparison.Ordinal);
        Assert.Contains("not ours", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>Stale beats missing: re-running init fixes both, and the stamp is the root cause.</summary>
    [Fact]
    public void A_stale_and_a_missing_skill_together_report_the_stamp()
    {
        var finding = SkillsInstalledCheck.Run(Project(Installed(Doctor, Older)));

        Assert.False(finding.Passed);
        Assert.Contains($"installed from {Older}", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_outcome_carries_the_same_stable_id_and_a_published_doc_url()
    {
        DoctorSnapshot[] snapshots =
        [
            DoctorSnapshotFixtures.Empty(),
            Project(),
            Project(Installed(Doctor, Older)),
        ];
        foreach (var snapshot in snapshots)
        {
            var finding = SkillsInstalledCheck.Run(snapshot);
            Assert.Equal("config.skills-installed", finding.Id);
            Assert.StartsWith("https://", finding.DocUrl, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// The two entry points into the same release can stamp a page with a different Id — a global
    /// tool re-packaged, or a hand-edited page — but the release is what "current" means (D9), never
    /// the coordinate whole. Comparing whole coordinates would report every such page stale for a
    /// difference that changes nothing about the page.
    /// </summary>
    [Fact]
    public void A_page_stamped_by_another_package_at_the_same_version_is_current()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(
                Installed(Doctor, "NarrativeTrace.Cli/" + CurrentVersion),
                Installed(AddTracing, "NarrativeTrace.Cli/" + CurrentVersion)));

        Assert.True(finding.Passed);
    }

    [Fact]
    public void A_page_stamped_at_another_version_is_stale_whatever_the_package_is()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(
                Installed(Doctor, "NarrativeTrace.Cli/0.0.1"),
                Installed(AddTracing, "NarrativeTrace.Cli/0.0.1")));

        Assert.False(finding.Passed);
        Assert.Contains("NarrativeTrace.Cli/0.0.1", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>A hand-edited stamp that is not a coordinate at all is compared whole, and so is stale.</summary>
    [Fact]
    public void A_stamp_that_names_no_version_is_compared_as_it_stands()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, "hand-edited"), Installed(AddTracing, "hand-edited")));

        Assert.False(finding.Passed);
        Assert.Contains("hand-edited", finding.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A stamp is trusted for its version segment only when it IS a two-part <c>Id/Version</c>
    /// coordinate. A stamp with a missing or an extra segment names no release anybody can place, so
    /// it is compared whole and reads as stale — the same answer a hand-edited stamp gets.
    /// </summary>
    [Theory]
    [InlineData("NarrativeTrace.Skills/1.2/3")]
    [InlineData("a/b/c/" + CurrentVersion)]
    public void A_stamp_with_the_wrong_number_of_segments_is_stale_even_when_it_contains_the_right_version(
        string malformed)
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, malformed), Installed(AddTracing, malformed)));

        Assert.False(finding.Passed);
        Assert.Contains(malformed, finding.Message, StringComparison.Ordinal);
    }

    /// <summary>Two different stale stamps are both named, sorted, so a reader sees the whole picture.</summary>
    [Fact]
    public void Every_distinct_stale_stamp_is_named_once()
    {
        var finding = SkillsInstalledCheck.Run(
            Project(Installed(Doctor, Older), Installed(AddTracing, "NarrativeTrace.Skills/0.0.2")));

        Assert.Contains(Older, finding.Message, StringComparison.Ordinal);
        Assert.Contains("NarrativeTrace.Skills/0.0.2", finding.Message, StringComparison.Ordinal);
    }
}
