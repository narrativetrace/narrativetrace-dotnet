// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Decides what an uninstall would remove — exactly what the installer wrote, and nothing beside it.
/// </summary>
/// <remarks>
/// <para>INTENT: the other half of the promise that makes an install safe to try. A skill directory is
/// removed only when its page carries our provenance line, whatever carrier stamped it; the managed
/// section is removed from its markers out; the import line is removed only when it is still character
/// for character the line the installer added.</para>
/// <para>A file is DELETED only when the installer created it (the <c>narrativetrace:created</c> note on
/// its first line) and nothing but our own content is left in it. A file the installer merely appended
/// to is always kept, even if removing our section empties it. Pure, like <see cref="InitPlanner"/>.</para>
/// </remarks>
public static class UninstallPlanner
{
    /// <summary>What an uninstall reports as its carrier when the project carries no stamp at all.</summary>
    internal const string UnknownCarrier = "NarrativeTrace.Skills/unknown";

    /// <summary>The plan an uninstall with these options would apply to this project.</summary>
    public static InitPlan Plan(ProjectState state, InitOptions options)
    {
        if (state is null || options is null)
        {
            throw new ArgumentException("planning an uninstall needs a project state and options");
        }

        var actions = new List<Action>();
        if (options.Scope.IncludesSkills())
        {
            PlanSkills(state, actions);
        }

        if (options.Scope.IncludesAgentsMd())
        {
            PlanAgentsMd(state, actions);
            PlanClaudeMd(state, actions);
            PlanRuleFiles(state, actions);
            PlanPointers(state, actions);
        }

        return new InitPlan(InstalledCoordinate(state), options.DryRun, actions);
    }

    /// <summary>The page first, then the directory it was the only reason for.</summary>
    private static void PlanSkills(ProjectState state, List<Action> actions)
    {
        foreach (var skill in state.InstalledSkills.Where(skill => skill.Presence == SkillPresence.Ours))
        {
            actions.Add(new DeleteFile(skill.PagePath, skill.Body));
            actions.Add(new DeleteDirectory(skill.Directory));
        }
    }

    private static void PlanAgentsMd(ProjectState state, List<Action> actions)
    {
        var text = state.AgentsMd;
        if (text is not null)
        {
            RemoveSection(InitPlanner.AgentsMd, text, mayDelete: true, actions);
        }
    }

    /// <summary>A rule file is never deleted: the installer never created one.</summary>
    private static void PlanRuleFiles(ProjectState state, List<Action> actions)
    {
        foreach (var (path, text) in state.MarkedRuleFiles)
        {
            RemoveSection(path, text, mayDelete: false, actions);
        }
    }

    /// <summary>
    /// Every platform pointer the table names, whether or not the platform's marker is still there:
    /// what the installer wrote is removed even after the project stopped looking like that platform.
    /// A pointer behind a link was never written by us and is not in the snapshot.
    /// </summary>
    private static void PlanPointers(ProjectState state, List<Action> actions)
    {
        foreach (var pointer in PlatformTable.Rows.Select(row => row.Pointer).Where(pointer => pointer is not Pointer.None))
        {
            var text = state.PointerFile(pointer.Path);
            if (text is null)
            {
                continue;
            }

            if (pointer is Pointer.Rule)
            {
                RemoveRule(pointer.Path, text, actions);
            }
            else
            {
                RemoveSection(pointer.Path, text, mayDelete: true, actions);
            }
        }
    }

    /// <summary>
    /// A rule file is deleted when nothing but its frontmatter is left once our section is gone — the
    /// installer made it — and otherwise loses only the section, so a rule the user added below ours
    /// survives.
    /// </summary>
    private static void RemoveRule(string path, string text, List<Action> actions)
    {
        var scan = MarkedBlock.ScanText(text);
        if (scan.Problems.Count > 0 || scan.Regions.Count > 1)
        {
            actions.Add(new Refuse(
                path, path + " does not carry exactly one NarrativeTrace section — remove it by hand"));
        }
        else if (scan.Regions.Count == 1)
        {
            var remainder = MarkedBlock.Remove(text, scan.Regions[0]);
            var onlyFrontmatter = string.Equals(
                MarkedBlock.WithEol(MarkedBlock.WithoutByteOrderMark(remainder), "\n"),
                CursorRule.Frontmatter,
                StringComparison.Ordinal);
            actions.Add(onlyFrontmatter ? new DeleteFile(path, text) : new ReplaceBlock(path, text, remainder));
        }
    }

    private static void RemoveSection(string path, string text, bool mayDelete, List<Action> actions)
    {
        var scan = MarkedBlock.ScanText(text);
        if (scan.Problems.Count > 0 || scan.Regions.Count > 1)
        {
            actions.Add(new Refuse(
                path, path + " does not carry exactly one NarrativeTrace section — remove it by hand"));
            return;
        }

        if (scan.Regions.Count == 0)
        {
            return;
        }

        var withoutSection = MarkedBlock.Remove(text, scan.Regions[0]);
        var created = MarkedBlock.LineIs(withoutSection, MarkedBlock.CreatedNote);
        var remainder = created is not null ? MarkedBlock.Remove(withoutSection, created) : withoutSection;
        if (mayDelete && created is not null && string.IsNullOrWhiteSpace(MarkedBlock.WithoutByteOrderMark(remainder)))
        {
            actions.Add(new DeleteFile(path, text));
            return;
        }

        actions.Add(new ReplaceBlock(path, text, remainder));
    }

    private static void PlanClaudeMd(ProjectState state, List<Action> actions)
    {
        var text = state.ClaudeMd;
        if (text is null)
        {
            return;
        }

        var line = MarkedBlock.LineIs(text, InitPlanner.ImportLine);
        if (line is not null)
        {
            actions.Add(new ReplaceBlock(InitPlanner.ClaudeMd, text, MarkedBlock.Remove(text, line)));
        }
    }

    /// <summary>
    /// The coordinate this project was installed from: the stamp on the managed section, else the one on
    /// the first skill of ours, else an honest <c>unknown</c>.
    /// </summary>
    private static string InstalledCoordinate(ProjectState state)
    {
        var text = state.AgentsMd;
        if (text is not null)
        {
            var scan = MarkedBlock.ScanText(text);
            if (scan.HasExactlyOneRegion && scan.Regions[0].Coordinate.Length > 0)
            {
                return scan.Regions[0].Coordinate;
            }
        }

        return state.InstalledSkills
            .Where(skill => skill.Presence == SkillPresence.Ours)
            .Select(skill => skill.Coordinate)
            .FirstOrDefault() ?? UnknownCarrier;
    }
}
