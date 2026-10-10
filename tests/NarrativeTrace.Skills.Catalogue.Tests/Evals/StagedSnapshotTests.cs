// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The tree a registry reads — this repository's registry surface as a public clone would show it,
/// staged out of <c>HEAD</c>. Two argv lists, never a shell string, both with absolute paths so
/// they run in any working directory.
/// </summary>
public sealed class StagedSnapshotTests
{
    private const string RepoRoot = "/repo";
    private const string Into = "/work/staged";

    private static readonly string[] ExpectedSurface = [".claude-plugin", ".claude/skills", ".agents/skills"];
    private static readonly string[] ArchivePrefix = ["git", "-C", RepoRoot, "archive", "--format=tar", "-o"];

    [Fact]
    public void The_registry_surface_is_the_marketplace_file_and_both_page_flavours()
    {
        Assert.Equal(ExpectedSurface, StagedSnapshot.RegistrySurface);
    }

    [Fact]
    public void Staging_is_a_git_archive_then_a_tar_extract()
    {
        var commands = StagedSnapshot.StagingCommands(RepoRoot, Into);

        Assert.Equal(2, commands.Count);
        Assert.Equal("git", commands[0][0]);
        Assert.Equal("tar", commands[1][0]);
    }

    [Fact]
    public void The_archive_command_points_git_at_the_repo_and_names_every_surface_entry()
    {
        var archive = StagedSnapshot.StagingCommands(RepoRoot, Into)[0];

        Assert.Equal(ArchivePrefix, archive.Take(6));
        Assert.Equal("HEAD", archive[7]);
        Assert.Equal("--", archive[8]);
        Assert.Equal(StagedSnapshot.RegistrySurface, archive.Skip(9).ToList());
    }

    [Fact]
    public void The_tarball_is_a_sibling_of_the_staged_tree_so_the_tree_holds_only_what_was_archived()
    {
        var commands = StagedSnapshot.StagingCommands(RepoRoot, Into);
        var archive = commands[0][6];

        Assert.Equal(Into + ".tar", archive);
        Assert.Equal(new[] { "tar", "-xf", archive, "-C", Into }, commands[1]);
    }
}
