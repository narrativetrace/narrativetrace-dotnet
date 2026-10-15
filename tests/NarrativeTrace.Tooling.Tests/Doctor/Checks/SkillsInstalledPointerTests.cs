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
/// <c>config.skills-installed</c> and the platform pointers (Phase 8): "pointer present and current"
/// per detected platform, and a failure naming every platform whose pointer is missing, foreign,
/// linked, stale or unstamped. Method names mirror the Java <c>SkillsInstalledCheckTest</c> pointer cases.
/// </summary>
public sealed class SkillsInstalledPointerTests
{
    private const string Doctor = "narrativetrace-doctor";
    private const string AddTracing = "add-narrative-tracing";
    private const string Current = "NarrativeTrace.Skills/1.2.3";
    private const string Older = "NarrativeTrace.Skills/0.0.1";

    private static DoctorSnapshot Installed(params PointerReading[] pointers) =>
        DoctorSnapshotFixtures.Empty() with
        {
            CarrierCatalogueSkills = [Doctor, AddTracing],
            ProjectResolvedFamilyVersion = "1.2.3",
            InstalledSkills =
            [
                new InstalledSkill(SkillFlavour.Agents, Doctor, SkillPresence.Ours, Current, "page"),
                new InstalledSkill(SkillFlavour.Agents, AddTracing, SkillPresence.Ours, Current, "page"),
            ],
            PointerReadings = pointers,
        };

    private static PointerReading Cursor(Status status, string coordinate) =>
        new("cursor", "Cursor", ".cursor/rules/narrativetrace.mdc", status, coordinate);

    private static PointerReading Replit(Status status, string coordinate) =>
        new("replit", "Replit", "replit.md", status, coordinate);

    [Fact]
    public void SaysNothingAboutPointersForAProjectOnNoPlatformThatNeedsOne()
    {
        var finding = SkillsInstalledCheck.Run(Installed());

        Assert.True(finding.Passed);
        Assert.DoesNotContain("pointer", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsAPresentAndCurrentPointerPerDetectedPlatform()
    {
        var finding = SkillsInstalledCheck.Run(Installed(Cursor(Status.Section, Current), Replit(Status.Section, Current)));

        Assert.True(finding.Passed);
        Assert.Contains("Cursor pointer present and current", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Replit pointer present and current", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void APointerStampedWithAnotherCarrierOfTheSameReleaseIsStillCurrent()
    {
        var finding = SkillsInstalledCheck.Run(Installed(Cursor(Status.Section, "NarrativeTrace.Cli/1.2.3")));

        Assert.True(finding.Passed);
    }

    [Fact]
    public void FailsWhenADetectedPlatformHasNoPointerAndNamesThePath()
    {
        var finding = SkillsInstalledCheck.Run(Installed(Cursor(Status.Absent, "")));

        Assert.False(finding.Passed);
        Assert.Contains("Cursor", finding.Message, StringComparison.Ordinal);
        Assert.Contains(".cursor/rules/narrativetrace.mdc", finding.Message, StringComparison.Ordinal);
        Assert.Contains("missing", finding.Message, StringComparison.Ordinal);
        Assert.Contains("init --dry-run", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsWhenAPointerIsFromAnOlderRelease()
    {
        var finding = SkillsInstalledCheck.Run(Installed(Replit(Status.Section, Older)));

        Assert.False(finding.Passed);
        Assert.Contains("Replit", finding.Message, StringComparison.Ordinal);
        Assert.Contains(Older, finding.Message, StringComparison.Ordinal);
        Assert.Contains("1.2.3", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsWhenAPointerCarriesNoStampBecauseNothingSaysWhichReleaseItDescribes()
    {
        var finding = SkillsInstalledCheck.Run(Installed(Cursor(Status.Section, "")));

        Assert.False(finding.Passed);
        Assert.Contains("no stamp", finding.Message, StringComparison.Ordinal);
        Assert.Contains("adopt", finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void FailsWhenAPointerFileHasNoSectionOfOursOrSitsBehindALink()
    {
        var foreign = SkillsInstalledCheck.Run(Installed(Replit(Status.Foreign, "")));
        var linked = SkillsInstalledCheck.Run(Installed(Cursor(Status.Linked, "")));

        Assert.False(foreign.Passed);
        Assert.Contains("--write-existing", foreign.Fix, StringComparison.Ordinal);
        Assert.False(linked.Passed);
        Assert.Contains("symbolic link", linked.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NamesEveryPlatformWhoseSectionIsWrongNotJustTheFirst()
    {
        var finding = SkillsInstalledCheck.Run(Installed(Cursor(Status.Absent, ""), Replit(Status.Section, Older)));

        Assert.Contains("Cursor", finding.Message, StringComparison.Ordinal);
        Assert.Contains("Replit", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ASkillsFailureStillWinsOverAPointerFailure()
    {
        var snapshot = DoctorSnapshotFixtures.Empty() with
        {
            CarrierCatalogueSkills = [Doctor],
            ProjectResolvedFamilyVersion = "1.2.3",
            PointerReadings = [Cursor(Status.Absent, "")],
        };

        Assert.Contains("not installed", SkillsInstalledCheck.Run(snapshot).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnresolvedProjectVersionPassesWithoutJudgingPointers()
    {
        var snapshot = Installed(Cursor(Status.Absent, "")) with { ProjectResolvedFamilyVersion = null };

        Assert.True(SkillsInstalledCheck.Run(snapshot).Passed);
    }
}
