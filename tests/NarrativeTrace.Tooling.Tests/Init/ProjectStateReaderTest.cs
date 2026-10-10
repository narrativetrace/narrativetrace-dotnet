// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Reads a real directory once, read-only: what the planner later decides from is decided here. Method
/// names mirror the Java <c>ProjectStateReaderTest</c>. The Gradle output-directory cases and Java's
/// invalid-UTF-8 "fails loudly" case have no .NET counterpart: this port has no build file to read a
/// trace directory from, and <see cref="System.Text.Encoding.UTF8"/> decodes replacement characters
/// rather than throwing.
/// </summary>
public sealed class ProjectStateReaderTest : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "nt-reader-" + Guid.NewGuid().ToString("N"));

    public ProjectStateReaderTest() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private void Write(string relative, string content)
    {
        var file = Path.Combine(_dir, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static string InstalledPage(string coordinate) =>
        "---\nname: narrativetrace-doctor\n---\n" + Provenance.Line(coordinate) + "\n\nbody\n";

    [Fact]
    public void ReadsAnEmptyDirectoryAsAnUntouchedProject()
    {
        var state = ProjectStateReader.Read(_dir);

        Assert.Null(state.AgentsMd);
        Assert.Null(state.ClaudeMd);
        Assert.False(state.HasClaudeDirectory);
        Assert.Empty(state.InstalledSkills);
        Assert.Empty(state.MarkedRuleFiles);
        Assert.Equal("TestResults/narrativetrace", state.OutputDirectory);
    }

    [Fact]
    public void ReadsBothContextFilesByteForByte()
    {
        Write("AGENTS.md", "# Agents\r\n\r\ntext");
        Write("CLAUDE.md", "﻿# Claude\n");

        var state = ProjectStateReader.Read(_dir);

        Assert.Equal("# Agents\r\n\r\ntext", state.AgentsMd);
        Assert.Equal("﻿# Claude\n", state.ClaudeMd);
    }

    [Fact]
    public void SeesTheVendorDirectoryEvenWhenItHoldsNoSkills()
    {
        Directory.CreateDirectory(Path.Combine(_dir, ".claude"));

        Assert.True(ProjectStateReader.Read(_dir).HasClaudeDirectory);
    }

    [Fact]
    public void ReadsAnInstalledSkillWithItsProvenanceCoordinate()
    {
        var page = InstalledPage("NarrativeTrace.Skills/0.1.0");
        Write(".agents/skills/narrativetrace-doctor/SKILL.md", page);

        var skill = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!;

        Assert.Equal(SkillPresence.Ours, skill.Presence);
        Assert.Equal("NarrativeTrace.Skills/0.1.0", skill.Coordinate);
        Assert.Equal(page, skill.Body);
        Assert.Equal(".agents/skills/narrativetrace-doctor/SKILL.md", skill.PagePath);
        Assert.Equal(".agents/skills/narrativetrace-doctor", skill.Directory);
    }

    [Fact]
    public void ReadsASkillDirectoryWithoutOurProvenanceAsForeign()
    {
        Write(".agents/skills/somebody-elses/SKILL.md", "---\nname: x\n---\nbody\n");
        Directory.CreateDirectory(Path.Combine(_dir, ".claude", "skills", "no-page-at-all"));

        var state = ProjectStateReader.Read(_dir);

        Assert.Equal(SkillPresence.Foreign,
            state.InstalledSkill(SkillFlavour.Agents, "somebody-elses")!.Presence);
        Assert.Equal(SkillPresence.Foreign,
            state.InstalledSkill(SkillFlavour.Claude, "no-page-at-all")!.Presence);
    }

    [Fact]
    public void ReadsAFileSittingWhereASkillDirectoryBelongsAsNotADirectory()
    {
        Write(".agents/skills/narrativetrace-doctor", "not a directory\n");

        var skill = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!;

        Assert.Equal(SkillPresence.NotADirectory, skill.Presence);
        Assert.Equal("", skill.Body);
    }

    [Fact]
    public void ReadsBothFlavoursIndependently()
    {
        Write(".agents/skills/narrativetrace-doctor/SKILL.md", InstalledPage("NarrativeTrace.Skills/0.1.0"));
        Write(".claude/skills/narrativetrace-doctor/SKILL.md", "---\nx\n---\nother\n");

        var state = ProjectStateReader.Read(_dir);

        Assert.Equal(2, state.InstalledSkills.Count);
        Assert.Equal(SkillPresence.Ours,
            state.InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!.Presence);
        Assert.Equal(SkillPresence.Foreign,
            state.InstalledSkill(SkillFlavour.Claude, "narrativetrace-doctor")!.Presence);
        Assert.Null(state.InstalledSkill(SkillFlavour.Claude, "absent"));
    }

    // --- links ----------------------------------------------------------------------------------

    /// <summary>
    /// The verified hazard <c>npx skills add</c> leaves behind: the agents page is real, and the
    /// vendor path is a LINK to it. Following that link would make the installer write the vendor
    /// flavour over the agents page, so the reader reports the link instead of resolving it.
    /// </summary>
    [Fact]
    public void ReadsASymlinkedSkillDirectoryAsALinkAndNotAsADirectoryOfOurs()
    {
        var page = InstalledPage("NarrativeTrace.Skills/0.1.0");
        Write(".agents/skills/narrativetrace-doctor/SKILL.md", page);
        Directory.CreateDirectory(Path.Combine(_dir, ".claude", "skills"));
        Directory.CreateSymbolicLink(
            Path.Combine(_dir, ".claude", "skills", "narrativetrace-doctor"),
            Path.Combine("..", "..", ".agents", "skills", "narrativetrace-doctor"));

        var linked = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Claude, "narrativetrace-doctor")!;

        Assert.Equal(SkillPresence.LinkedDirectory, linked.Presence);
        Assert.Equal(Path.Combine("..", "..", ".agents", "skills", "narrativetrace-doctor"), linked.Link);
        Assert.Equal(page, linked.Body);
        Assert.Equal(".claude/skills/narrativetrace-doctor", linked.LinkedAt());
    }

    [Fact]
    public void ReadsASymlinkedPageInsideARealDirectoryAsALinkToo()
    {
        Write(".agents/skills/somewhere/SKILL.md", "---\nname: x\n---\nbody\n");
        Directory.CreateDirectory(Path.Combine(_dir, ".agents", "skills", "narrativetrace-doctor"));
        File.CreateSymbolicLink(
            Path.Combine(_dir, ".agents", "skills", "narrativetrace-doctor", "SKILL.md"),
            Path.Combine("..", "somewhere", "SKILL.md"));

        var linked = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!;

        Assert.Equal(SkillPresence.LinkedPage, linked.Presence);
        Assert.Equal("---\nname: x\n---\nbody\n", linked.Body);
        Assert.Equal(".agents/skills/narrativetrace-doctor/SKILL.md", linked.LinkedAt());
    }

    /// <summary>A link that resolves to nothing reaches no page: there is nothing to adopt or to remove.</summary>
    [Fact]
    public void ReadsADanglingLinkAsALinkThatReachesNoPage()
    {
        Directory.CreateDirectory(Path.Combine(_dir, ".agents", "skills"));
        Directory.CreateSymbolicLink(
            Path.Combine(_dir, ".agents", "skills", "narrativetrace-doctor"),
            Path.Combine("..", "..", "gone"));

        var linked = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!;

        Assert.Equal(SkillPresence.LinkedDirectory, linked.Presence);
        Assert.Equal("", linked.Body);
    }

    /// <summary>
    /// A link OUT of the project reaches no page either, whatever is at the other end. Nothing outside
    /// the project is ours to stamp or to delete, and a shared skills directory is exactly the kind of
    /// thing a person links to on purpose.
    /// </summary>
    [Fact]
    public void ReadsALinkOutOfTheProjectAsReachingNoPage()
    {
        var elsewhere = Path.Combine(Path.GetTempPath(), "nt-elsewhere-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(elsewhere, "shared"));
        File.WriteAllText(Path.Combine(elsewhere, "shared", "SKILL.md"), InstalledPage("NarrativeTrace.Skills/1"));
        try
        {
            Directory.CreateDirectory(Path.Combine(_dir, ".agents", "skills"));
            Directory.CreateSymbolicLink(
                Path.Combine(_dir, ".agents", "skills", "narrativetrace-doctor"),
                Path.Combine(elsewhere, "shared"));

            var linked = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!;

            Assert.Equal(SkillPresence.LinkedDirectory, linked.Presence);
            Assert.Equal("", linked.Body);
            Assert.Equal(Path.Combine(elsewhere, "shared"), linked.Link);
        }
        finally
        {
            Directory.Delete(elsewhere, recursive: true);
        }
    }

    /// <summary>
    /// A whole install root behind a link is the same hazard one level up, and worse: every skill of
    /// that flavour would be written into somebody else's tree, including the ones not there yet. The
    /// root is reported as linked and nothing under it is listed, so the planner refuses the flavour
    /// once instead of planning writes it cannot make.
    /// </summary>
    [Fact]
    public void ReadsASymlinkedInstallRootAsLinkedAndListsNothingUnderIt()
    {
        var shared = Path.Combine(Path.GetTempPath(), "nt-shared-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(shared, "narrativetrace-doctor"));
        File.WriteAllText(Path.Combine(shared, "narrativetrace-doctor", "SKILL.md"), "---\nname: x\n---\nbody\n");
        try
        {
            Directory.CreateDirectory(Path.Combine(_dir, ".claude"));
            Directory.CreateSymbolicLink(Path.Combine(_dir, ".claude", "skills"), shared);

            var state = ProjectStateReader.Read(_dir);

            Assert.Equal(shared, state.LinkedInstallRoot(SkillFlavour.Claude));
            Assert.Empty(state.InstalledSkills);
            Assert.Null(state.LinkedInstallRoot(SkillFlavour.Agents));
        }
        finally
        {
            Directory.Delete(shared, recursive: true);
        }
    }

    /// <summary>
    /// A CHAIN of links the filesystem will happily follow, ending at something that is not a skill
    /// directory. The reader follows nothing itself: it reports a link, and the page it looks for
    /// behind that link — <c>&lt;name&gt;/SKILL.md</c> — does not exist, so the body is empty and the
    /// planner refuses with "no page at the other end". Worth pinning because the chain resolves: a
    /// reader that trusted the real path and read whatever it landed on would hand the planner a plain
    /// file's bytes under a skill's name.
    /// </summary>
    [Fact]
    public void ReadsALinkChainEndingAtSomethingThatIsNotASkillDirectoryAsReachingNoPage()
    {
        Write(".agents/skills/target.md", "not a skill directory\n");
        File.CreateSymbolicLink(Path.Combine(_dir, ".agents", "skills", "midway"), "target.md");
        Directory.CreateSymbolicLink(Path.Combine(_dir, ".agents", "skills", "narrativetrace-doctor"), "midway");

        var linked = ProjectStateReader.Read(_dir).InstalledSkill(SkillFlavour.Agents, "narrativetrace-doctor")!;

        Assert.Equal(SkillPresence.LinkedDirectory, linked.Presence);
        Assert.Equal("midway", linked.Link);
        Assert.Equal("", linked.Body);
    }

    [Fact]
    public void ReadsALegacyRuleFileOnlyWhenItCarriesOurMarkers()
    {
        var marked = "# rules\n<!-- narrativetrace:start -->\nx\n<!-- narrativetrace:end -->\n";
        Write(".cursorrules", marked);
        Write(".github/copilot-instructions.md", "# no markers here\n");

        var files = ProjectStateReader.Read(_dir).MarkedRuleFiles;
        Assert.Single(files);
        Assert.Equal(marked, files[".cursorrules"]);
    }

    [Fact]
    public void ReadsARuleFileWhoseMarkersDoNotPairUpSoThePlannerCanRefuseIt()
    {
        var broken = "# rules\n<!-- narrativetrace:start -->\nno end below it\n";
        Write(".cursorrules", broken);

        var files = ProjectStateReader.Read(_dir).MarkedRuleFiles;
        Assert.Single(files);
        Assert.Equal(broken, files[".cursorrules"]);
    }

    [Fact]
    public void BoundsHowManySkillDirectoriesItReads()
    {
        for (var i = 0; i < 5; i++)
        {
            Directory.CreateDirectory(Path.Combine(_dir, ".agents", "skills", "skill-" + i));
        }

        var state = ProjectStateReader.Read(_dir, 3);

        Assert.Equal(3, state.InstalledSkills.Count);
        Assert.Equal(["skill-0", "skill-1", "skill-2"], state.InstalledSkills.Select(skill => skill.Name));
    }

    private void WriteAssets(string relativeDir, params string[] idAndVersions)
    {
        var libraries = string.Join(", ", idAndVersions.Select(pair => "\"" + pair + "\": {\"type\": \"package\"}"));
        Write(relativeDir + "/project.assets.json", "{\"version\": 3, \"libraries\": {" + libraries + "}}");
    }

    [Fact]
    public void ResolvesTheFamilyVersionFromAlignedNarrativeTracePackages()
    {
        WriteAssets("obj", "NarrativeTrace.Core/0.1.5", "NarrativeTrace.Proxy/0.1.5", "Newtonsoft.Json/13.0.1");

        Assert.Equal("0.1.5", ProjectStateReader.Read(_dir).ResolvedFamilyVersion);
    }

    [Fact]
    public void ResolvesTheFamilyVersionFromANestedRestoreOutput()
    {
        WriteAssets("src/App/obj", "NarrativeTrace.Core/0.1.4");

        Assert.Equal("0.1.4", ProjectStateReader.Read(_dir).ResolvedFamilyVersion);
    }

    [Fact]
    public void LeavesTheFamilyVersionUnresolvedWhenNoNarrativeTracePackageIsRestored()
    {
        WriteAssets("obj", "Newtonsoft.Json/13.0.1");

        Assert.Null(ProjectStateReader.Read(_dir).ResolvedFamilyVersion);
    }

    [Fact]
    public void LeavesTheFamilyVersionUnresolvedWhenNarrativeTracePackagesDisagree()
    {
        // A drifted install is ambiguous — the doctor's alignment check reports it; the version
        // guard stays silent rather than warn against a version it cannot single out (D4).
        WriteAssets("obj", "NarrativeTrace.Core/0.1.5", "NarrativeTrace.Proxy/0.1.4");

        Assert.Null(ProjectStateReader.Read(_dir).ResolvedFamilyVersion);
    }

    [Fact]
    public void IgnoresAMalformedRestoreOutputWhenResolvingTheFamilyVersion()
    {
        Write("obj/project.assets.json", "{ not json");

        Assert.Null(ProjectStateReader.Read(_dir).ResolvedFamilyVersion);
    }

    [Fact]
    public void RefusesToReadAProjectThatIsNotADirectory()
    {
        var file = Path.Combine(_dir, "a-file");
        File.WriteAllText(file, "x");

        Assert.Contains("not a directory",
            Assert.Throws<ArgumentException>(() => ProjectStateReader.Read(file)).Message,
            StringComparison.Ordinal);
        Assert.Contains("project directory must be given",
            Assert.Throws<ArgumentException>(() => ProjectStateReader.Read(null!)).Message,
            StringComparison.Ordinal);
    }
}
