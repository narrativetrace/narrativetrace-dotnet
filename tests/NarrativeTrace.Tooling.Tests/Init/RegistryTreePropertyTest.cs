// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The properties that have to hold for a tree the installer did NOT create: the one a registry
/// leaves behind. Every try builds a real temp project shaped like an <c>npx skills add</c> install —
/// the rendered pages of one flavour, a symbolic link at the other flavour's path, and a lock file of
/// the registry's own — and then runs the installer over it.
/// </summary>
/// <remarks>
/// Four dimensions, because each one has broken something in a port at some point: which flavour's
/// path is the link, whether the link is the DIRECTORY or the page inside it, and which line ending
/// the checked-out pages carry. Mirrors the Java <c>RegistryTreePropertyTest</c> (FsCheck here,
/// jqwik there).
/// </remarks>
public class RegistryTreePropertyTest
{
    /// <summary>What <c>npx skills add</c> writes at the project root; nothing of ours may touch it.</summary>
    private const string LockFile = "{\"skills\": []}\n";

    private static readonly Carrier Carrier = Carriers.Shared();

    // --- the properties ---------------------------------------------------------------------------

    /// <summary>
    /// The whole of D5's promise: the pages are adopted, the link is replaced by a real path, and
    /// NOTHING is written through the link — each flavour's path ends up holding its own flavour's
    /// page, which is exactly what a write through the link would have destroyed.
    /// </summary>
    [Property(MaxTest = 64)]
    public void ARegistryTreeIsAdoptedWithoutWritingThroughItsLinks(
        bool linkTheVendorPath, bool linkThePage, bool crlf)
    {
        InATemporaryProject(project =>
        {
            RegistryTree(project, linkTheVendorPath, linkThePage, Eol(crlf));

            var report = Install(project);

            Assert.Equal(0, report.ExitCode);
            AssertEveryPageIsItsOwnFlavour(project);
            Assert.Equal(LockFile, File.ReadAllText(Path.Combine(project, "skills-lock.json")));
        });
    }

    /// <summary>Planning after adopting finds nothing left to do — idempotence, over a tree we did not write.</summary>
    [Property(MaxTest = 64)]
    public void PlanningAgainAfterAdoptingARegistryTreeFindsNothingToDo(
        bool linkTheVendorPath, bool linkThePage, bool crlf)
    {
        InATemporaryProject(project =>
        {
            RegistryTree(project, linkTheVendorPath, linkThePage, Eol(crlf));
            Install(project);
            var adopted = Projects.SnapshotOf(project);

            var second = Plan(project);
            Install(project);

            Assert.True(second.IsEmpty, PlanRenderer.RenderText(second));
            Assert.Equal(adopted, Projects.SnapshotOf(project));
        });
    }

    /// <summary>
    /// Uninstalling gives back the registry's own tree minus the pages, which adoption made ours. The
    /// lock file is the registry's and stays; a page identical to this release's was never anybody
    /// else's work to keep.
    /// </summary>
    [Property(MaxTest = 64)]
    public void UninstallingAfterAdoptingLeavesTheRegistrysOwnFilesAndNoLink(
        bool linkTheVendorPath, bool linkThePage, bool crlf)
    {
        InATemporaryProject(project =>
        {
            RegistryTree(project, linkTheVendorPath, linkThePage, Eol(crlf));
            Install(project);

            var report = PlanExecutor.Execute(
                UninstallPlanner.Plan(ProjectStateReader.Read(project), InitOptions.Defaults()), project);

            Assert.Equal(0, report.ExitCode);
            Assert.Equal(["skills-lock.json"], Projects.SnapshotOf(project).Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.Empty(Projects.LinksUnder(project));
        });
    }

    // --- the tree a registry leaves ---------------------------------------------------------------

    /// <summary>
    /// One flavour's pages for real, the other flavour's path a link to them, and the lock file. The
    /// real pages are this carrier's own rendering — that is what a registry installs — carrying the
    /// line ending of whoever checked them out.
    /// </summary>
    private static void RegistryTree(string project, bool linkTheVendorPath, bool linkThePage, string eol)
    {
        var real = linkTheVendorPath ? SkillFlavour.Agents : SkillFlavour.Claude;
        var linked = linkTheVendorPath ? SkillFlavour.Claude : SkillFlavour.Agents;
        Directory.CreateDirectory(Resolve(project, linked.InstallRoot()));
        foreach (var skill in Carrier.Skills)
        {
            var page = Path.Combine(Resolve(project, real.InstallRoot()), skill.Name, "SKILL.md");
            Directory.CreateDirectory(Path.GetDirectoryName(page)!);
            File.WriteAllText(page, Carrier.Body(skill, real).Replace("\n", eol, StringComparison.Ordinal));
            Link(Path.Combine(Resolve(project, linked.InstallRoot()), skill.Name), page, linkThePage);
        }

        File.WriteAllText(Path.Combine(project, "skills-lock.json"), LockFile);
    }

    /// <summary>The link a registry makes: the directory itself, or a real directory holding a linked page.</summary>
    private static void Link(string at, string page, bool linkThePage)
    {
        if (!linkThePage)
        {
            Directory.CreateSymbolicLink(at, Path.GetDirectoryName(page)!);
            return;
        }

        Directory.CreateDirectory(at);
        File.CreateSymbolicLink(Path.Combine(at, "SKILL.md"), page);
    }

    /// <summary>
    /// The assertion a write through a link would fail: every flavour's own path holds its OWN
    /// flavour's stamped page, as a real file.
    /// </summary>
    private static void AssertEveryPageIsItsOwnFlavour(string project)
    {
        foreach (var skill in Carrier.Skills)
        {
            foreach (var flavour in new[] { SkillFlavour.Agents, SkillFlavour.Claude })
            {
                var page = Path.Combine(Resolve(project, flavour.InstallRoot()), skill.Name, "SKILL.md");
                Assert.False(new FileInfo(page).LinkTarget is not null, page + " is a real file");
                Assert.Equal(
                    Provenance.Stamp(Carrier.Body(skill, flavour), Carrier.Coordinate),
                    File.ReadAllText(page));
            }
        }
    }

    private static string Eol(bool crlf) => crlf ? "\r\n" : "\n";

    // --- the project under test -------------------------------------------------------------------

    private static string Resolve(string project, string installRoot) =>
        Path.Combine(project, installRoot.Replace('/', Path.DirectorySeparatorChar));

    private static void InATemporaryProject(System.Action<string> body) =>
        Projects.InATemporaryOne("nt-registry-", body);

    private static InitPlan Plan(string project) =>
        InitPlanner.Plan(ProjectStateReader.Read(project), Carrier, InitOptions.Defaults());

    private static ExecutionReport Install(string project) => PlanExecutor.Execute(Plan(project), project);
}
