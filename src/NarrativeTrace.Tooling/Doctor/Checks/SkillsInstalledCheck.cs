// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Tooling.Doctor.Checks;

/// <summary>
/// <c>config.skills-installed</c> — the NarrativeTrace agent skills this project's carrier ships are
/// installed under <c>.agents/skills/</c>, and stamped with a release this project actually resolves.
/// </summary>
/// <remarks>
/// <para>INTENT: a project whose skills are absent gets an agent that improvises the library's
/// procedures; a project whose skills are a release behind gets an agent following commands that may
/// no longer exist. Both are silent failures with no symptom of their own, which is exactly the shape
/// of thing the doctor exists to say out loud.</para>
/// <para>Read-only, like every check. It reads what the installer's own reader found — a page is
/// "ours" only when it carries the provenance line <c>init</c> writes — so the doctor and the
/// installer can never disagree about which directory belongs to whom.</para>
/// <para><b>Deviation from the Java reference (D9, deliberate):</b> Java's first gate is "the carrier
/// could not be resolved" — offline, or no repository provides it. This port's carrier is always the
/// running CLI's own bundled resources, so that gate cannot fire the same way here. The equivalent
/// uncertainty in .NET is the project's OWN resolved NarrativeTrace family version — the same value
/// the <c>init</c> version guard (D4) already reads from restore output — because a .NET global tool
/// can be a different release than the project's library references. "Current" therefore compares an
/// installed page's stamped version against <see cref="DoctorSnapshot.ProjectResolvedFamilyVersion"/>,
/// never against the carrier's own coordinate.</para>
/// <para>Only <c>.agents/skills/</c> counts. The vendor copy is written only where a project is
/// detected as that vendor's, so a project carrying the vendor copy alone is a project the
/// open-standard install never reached.</para>
/// </remarks>
public static class SkillsInstalledCheck
{
    private const string Id = "config.skills-installed";

    // Quoted verbatim by documentation/agent-skills.md's "From a registry" section (rule 8, docs
    // as tests) — the marker pair around these three fields is that embed's source, never typed
    // into the page.
    // snippet:begin registryMessages
    private const string InitCommand = "dotnet tool run dotnet-narrativetrace init --dry-run";

    /// <summary>What a skill directory that is present but not ours is called in a message.</summary>
    private const string NotOurs = " (there, but not ours)";

    /// <summary>
    /// What a page with no provenance line most often IS: a registry install (D5 state 3) — <c>npx
    /// skills add</c>, or a plugin or workspace install — of this repository's own rendered pages.
    /// Naming the case matters because the obvious reading of "not ours" is "somebody else's work",
    /// which invites a <c>--force</c> nobody needs: <c>init</c> ADOPTS a page identical to this
    /// release's.
    /// </summary>
    private const string FromARegistry =
        " Pages that are there without our line usually came from a registry (npx skills add, a" +
        " plugin or workspace install). Run " + InitCommand +
        ", read the diff, then run it without the flag — a page identical to this release's is" +
        " adopted, and no --force is needed.";
    // snippet:end registryMessages

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        var finding = Decide(snapshot);
        Debug.Assert(
            finding.Passed || snapshot.ProjectResolvedFamilyVersion is not null,
            "a project whose NarrativeTrace version cannot be determined must never fail this check");
        return finding;
    }

    /// <summary>Worst first: nothing installed, then a wrong stamp, then a partial install.</summary>
    private static DoctorFinding Decide(DoctorSnapshot snapshot)
    {
        if (snapshot.ProjectResolvedFamilyVersion is null)
        {
            return DoctorFinding.Pass(
                Id,
                "This project's NarrativeTrace version could not be determined — cannot tell " +
                "whether the agent skills are current",
                DoctorDocUrls.ValidateInstallation);
        }

        var ours = Ours(snapshot);
        if (ours.Count == 0)
        {
            return NotInstalled(snapshot);
        }

        var stale = StaleCoordinates(ours, snapshot.ProjectResolvedFamilyVersion);
        if (stale.Count > 0)
        {
            return Stale(stale, snapshot.ProjectResolvedFamilyVersion);
        }

        var missing = Missing(snapshot);
        return missing.Count == 0 ? DecidePointers(ours, snapshot) : Incomplete(missing);
    }

    private static DoctorFinding DecidePointers(List<InstalledSkill> ours, DoctorSnapshot snapshot)
    {
        var problems = PointerProblems(snapshot);
        return problems.Count == 0
            ? UpToDate(ours, snapshot)
            : PointersWrong(problems);
    }

    private static DoctorFinding UpToDate(List<InstalledSkill> ours, DoctorSnapshot snapshot)
    {
        return DoctorFinding.Pass(
            Id,
            $"All {ours.Count} agent skill(s) are installed and current with this project's " +
            $"NarrativeTrace {snapshot.ProjectResolvedFamilyVersion}{PointersCurrent(snapshot)}",
            DoctorDocUrls.ValidateInstallation);
    }

    /// <summary><c>"; Cursor pointer present and current, …"</c> — empty when no platform needs a pointer.</summary>
    private static string PointersCurrent(DoctorSnapshot snapshot) =>
        snapshot.PointerReadings.Count == 0
            ? ""
            : "; " + string.Join(
                ", ", snapshot.PointerReadings.Select(reading => reading.PlatformName + " pointer present and current"));

    private static DoctorFinding PointersWrong(IReadOnlyList<string> problems)
    {
        return DoctorFinding.Fail(
            Id,
            "The agent skills are installed, but " + string.Join("; ", problems),
            $"Run {InitCommand}, read the diff, then run it without the flag. A rule file or section " +
            "that is identical to this release's is adopted as it stands; where the file exists and " +
            "has no section of ours, add --write-existing; a link must be removed by hand.",
            DoctorDocUrls.ValidateInstallation);
    }

    /// <summary>One sentence per detected platform whose pointer is missing, foreign, linked or not current.</summary>
    private static List<string> PointerProblems(DoctorSnapshot snapshot) =>
        snapshot.PointerReadings
            .Select(reading => PointerProblem(reading, snapshot.ProjectResolvedFamilyVersion!))
            .OfType<string>()
            .ToList();

    private static string? PointerProblem(PointerReading reading, string resolvedVersion)
    {
        var where = $"{reading.PlatformName} pointer {reading.Path}";
        return reading.Status switch
        {
            PointerReading.PointerStatus.Absent => where + " is missing",
            PointerReading.PointerStatus.Linked => where + " is behind a symbolic link, and nothing is written through one",
            PointerReading.PointerStatus.Foreign => where + " exists but has no NarrativeTrace section of ours",
            _ => SectionProblem(reading, where, resolvedVersion),
        };
    }

    private static string? SectionProblem(PointerReading reading, string where, string resolvedVersion)
    {
        if (reading.Coordinate.Length == 0)
        {
            return where + " carries no stamp, so no release it describes can be named — init will adopt it";
        }

        return VersionOf(reading.Coordinate) == resolvedVersion
            ? null
            : $"{where} was written for {reading.Coordinate}, project resolves {resolvedVersion}";
    }

    private static DoctorFinding NotInstalled(DoctorSnapshot snapshot)
    {
        var foreign = Present(snapshot);
        var suffix = foreign.Count == 0 ? "" : $" — {string.Join(", ", foreign)} is there, not ours";
        var fix = $"Run {InitCommand}, read the diff, then run it without the flag.";
        return DoctorFinding.Fail(
            Id,
            $"The NarrativeTrace agent skills are not installed under {SkillFlavour.Agents.InstallRoot()}/{suffix}",
            foreign.Count == 0 ? fix : fix + FromARegistry,
            DoctorDocUrls.ValidateInstallation);
    }

    private static DoctorFinding Stale(IReadOnlyList<string> stale, string resolvedVersion)
    {
        return DoctorFinding.Fail(
            Id,
            $"Agent skills installed from {string.Join(", ", stale)}, project resolves {resolvedVersion}",
            $"Re-run {InitCommand} and apply it — the installed pages describe a different release " +
            "of NarrativeTrace than this project uses.",
            DoctorDocUrls.ValidateInstallation);
    }

    private static DoctorFinding Incomplete(IReadOnlyList<string> missing)
    {
        return DoctorFinding.Fail(
            Id,
            $"The agent skills are installed, but this carrier's {string.Join(", ", missing)} is missing",
            $"Run {InitCommand} to add the missing page(s). A page identical to this release's is " +
            "adopted as it stands; --force is only for a directory somebody else really owns.",
            DoctorDocUrls.ValidateInstallation);
    }

    /// <summary>The catalogue's skills that are installed in this project and carry our provenance line.</summary>
    private static List<InstalledSkill> Ours(DoctorSnapshot snapshot)
    {
        return snapshot.CarrierCatalogueSkills
            .Select(name => Found(snapshot, name))
            .Where(skill => skill is { Presence: SkillPresence.Ours })
            .Select(skill => skill!)
            .ToList();
    }

    /// <summary>The catalogue's skills that are absent, or present as somebody else's — named for a reader.</summary>
    private static List<string> Missing(DoctorSnapshot snapshot)
    {
        return snapshot.CarrierCatalogueSkills
            .Where(name => !IsOurs(snapshot, name))
            .Select(name => Found(snapshot, name) is not null ? name + NotOurs : name)
            .ToList();
    }

    /// <summary>
    /// The catalogue's skills that have a directory in this project. Asked only from
    /// <see cref="NotInstalled"/>, where nothing of ours is installed by construction — so "it is
    /// there" already means "it is somebody else's".
    /// </summary>
    private static List<string> Present(DoctorSnapshot snapshot)
    {
        return snapshot.CarrierCatalogueSkills
            .Where(name => Found(snapshot, name) is not null)
            .ToList();
    }

    /// <summary>
    /// Every distinct stamp the installed pages carry that is not the version this project resolves.
    /// </summary>
    private static List<string> StaleCoordinates(IReadOnlyList<InstalledSkill> ours, string resolvedVersion)
    {
        return ours
            .Select(skill => skill.Coordinate)
            .Where(coordinate => VersionOf(coordinate) != resolvedVersion)
            .Distinct()
            .OrderBy(coordinate => coordinate, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// The version half of an <c>Id/Version</c> stamp — and only of one. A stamp with a segment
    /// missing or one too many names no release anybody can place, so it is compared as it stands and
    /// reads as stale, which is what a page nobody can place should read as. A stamp naming a
    /// different Id but the matching version still reads as current — "current" means the same
    /// release, not the same coordinate (D9), because a page can be reinstalled by a different
    /// carrier of the same release.
    /// </summary>
    private static string VersionOf(string coordinate)
    {
        var parts = coordinate.Split('/');
        return parts.Length == 2 ? parts[1] : coordinate;
    }

    private static InstalledSkill? Found(DoctorSnapshot snapshot, string name) =>
        snapshot.InstalledSkills.FirstOrDefault(
            skill => skill.Flavour == SkillFlavour.Agents && string.Equals(skill.Name, name, StringComparison.Ordinal));

    private static bool IsOurs(DoctorSnapshot snapshot, string name) =>
        Found(snapshot, name)?.Presence == SkillPresence.Ours;
}
