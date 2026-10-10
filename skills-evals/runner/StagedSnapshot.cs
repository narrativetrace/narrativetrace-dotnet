// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SkillsEvals;

/// <summary>
/// The tree a registry reads — this repository's registry surface as a clone of the public
/// repository would show it, staged out of <c>HEAD</c> into a scratch directory.
///
/// <para>Out of <c>HEAD</c> and never out of the working tree, for the same reason the publish
/// script's own staging and the build's vendor validation do it that way: a registry serves what
/// was committed, so a trial run against uncommitted edits would grade a tree no reader can get.</para>
///
/// <para>Two argv lists, never a shell string: <c>git archive</c> writes the tar, <c>tar</c> unpacks
/// it, and neither argument is ever reinterpreted. The caller creates the target directory and runs
/// both through the harness's own process seam.</para>
/// </summary>
public static class StagedSnapshot
{
    /// <summary>
    /// What a registry actually reads: the plugin marketplace file and both rendered page flavours.
    /// Every documented registry tool scans some subset of these three and nothing else of the
    /// repository, so staging exactly them keeps a trial's tree honest in both directions — nothing
    /// a reader cannot see, and nothing a reader can see left out.
    /// </summary>
    public static readonly IReadOnlyList<string> RegistrySurface =
        [".claude-plugin", ".claude/skills", ".agents/skills"];

    private const string ArchiveSuffix = ".tar";

    /// <summary>
    /// The commands that fill <paramref name="into"/> with <see cref="RegistrySurface"/> as of
    /// <c>HEAD</c> of the repository at <paramref name="repoRoot"/>. Both run with any working
    /// directory — every path is absolute, and <c>git</c> is pointed at the repository with
    /// <c>-C</c> rather than inheriting it.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> StagingCommands(string repoRoot, string into)
    {
        var archive = ArchiveBeside(into);
        var archiveCommand = new List<string>
        {
            "git", "-C", repoRoot, "archive", "--format=tar", "-o", archive, "HEAD", "--",
        };
        archiveCommand.AddRange(RegistrySurface);
        return [archiveCommand, ["tar", "-xf", archive, "-C", into]];
    }

    /// <summary>
    /// The tarball's own path: a sibling of the staged tree, so the tree holds exactly what
    /// <c>git archive</c> put there. A registry tool scans every file under the root it is given,
    /// and the intermediate artefact of staging is not something a clone of the public repository
    /// shows.
    /// </summary>
    private static string ArchiveBeside(string into) => into + ArchiveSuffix;
}
