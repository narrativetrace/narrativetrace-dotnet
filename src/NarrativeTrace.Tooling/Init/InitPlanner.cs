// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Decides what an install would do — and nothing else.
/// </summary>
/// <remarks>
/// <para>INTENT: a pure function of <c>(ProjectState, Carrier, InitOptions)</c>. Every existing-file
/// policy lives here and only here, which is why each row of it is a unit test with no filesystem at
/// all, and why a <c>--dry-run</c> diff is exactly what a real run would write.</para>
/// <para>An action whose result equals what is already there is dropped, so planning a project that is
/// already current yields an EMPTY plan — the property that makes a re-run idempotent. A refusal never
/// stops the plan: the rest of the actions are still planned, and the run exits 1. Nothing here reads
/// or writes a file.</para>
/// </remarks>
public static class InitPlanner
{
    /// <summary>The managed home, the one file the section is ever written into.</summary>
    internal const string AgentsMd = "AGENTS.md";

    /// <summary>The vendor context file, which gets an import line and never a copy of the section.</summary>
    internal const string ClaudeMd = "CLAUDE.md";

    /// <summary>The line that points the vendor's context file at the managed home.</summary>
    internal const string ImportLine = "@AGENTS.md";

    /// <summary>The plan an install with these options would apply to this project.</summary>
    public static InitPlan Plan(ProjectState state, Carrier carrier, InitOptions options)
    {
        if (state is null || carrier is null || options is null)
        {
            throw new ArgumentException("planning needs a project state, a carrier and options");
        }

        var actions = new List<Action>();
        if (options.Scope.IncludesSkills())
        {
            PlanSkills(state, carrier, options, actions);
        }

        if (options.Scope.IncludesAgentsMd())
        {
            PlanAgentsMd(state, carrier, options, actions);
            PlanClaudeMd(state, options, actions);
            PlanRuleFiles(state, carrier, actions);
        }

        return new InitPlan(carrier.Coordinate, options.DryRun, WithoutNoOps(actions), VersionGuard(state, carrier));
    }

    /// <summary>
    /// The version guard (D4): when the project resolves a NarrativeTrace family version different from
    /// the carrier the running CLI carries, one advisory line names both and the command that runs the
    /// matching CLI. The plan still runs — this warns, never refuses; the doctor's staleness check
    /// catches the result either way. With no resolvable project version it says nothing.
    /// </summary>
    private static IReadOnlyList<string> VersionGuard(ProjectState state, Carrier carrier)
    {
        var projectVersion = state.ResolvedFamilyVersion;
        if (string.IsNullOrEmpty(projectVersion))
        {
            return [];
        }

        var carrierVersion = CarrierVersion(carrier.Coordinate);
        if (string.Equals(projectVersion, carrierVersion, StringComparison.Ordinal))
        {
            return [];
        }

        return
        [
            "note: this CLI carries " + carrier.Coordinate + ", but the project resolves NarrativeTrace "
            + projectVersion + " — run `dotnet tool run dotnet-narrativetrace init` from a CLI at "
            + projectVersion + " to install matching skills.",
        ];
    }

    private static string CarrierVersion(string coordinate)
    {
        var slash = coordinate.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? coordinate : coordinate[(slash + 1)..];
    }

    // --- skills ---------------------------------------------------------------------------------

    private static void PlanSkills(
        ProjectState state, Carrier carrier, InitOptions options, List<Action> actions)
    {
        foreach (var skill in carrier.Skills)
        {
            foreach (var flavour in Flavours(state, options))
            {
                actions.Add(SkillAction(state, carrier, options, skill, flavour));
            }
        }
    }

    /// <summary>
    /// Which flavours this project gets: the open standard always, the vendor one where the project
    /// looks like that vendor's — or wherever the caller overrode the detection.
    /// </summary>
    private static IReadOnlyList<SkillFlavour> Flavours(ProjectState state, InitOptions options)
    {
        var vendor = options.VendorClaude switch
        {
            Vendor.On => true,
            Vendor.Off => false,
            _ => state.HasClaudeDirectory || state.ClaudeMd is not null,
        };
        return vendor ? [SkillFlavour.Agents, SkillFlavour.Claude] : [SkillFlavour.Agents];
    }

    private static Action SkillAction(
        ProjectState state, Carrier carrier, InitOptions options, SkillEntry skill, SkillFlavour flavour)
    {
        var directory = flavour.InstallRoot() + "/" + skill.Name;
        var page = directory + "/" + InstalledSkill.Page;
        var content = Provenance.Stamp(carrier.Body(skill, flavour), carrier.Coordinate);
        var installed = state.InstalledSkill(flavour, skill.Name);
        if (installed is null)
        {
            return new CreateFile(page, content);
        }

        return installed.Presence switch
        {
            SkillPresence.NotADirectory => new Refuse(
                directory, directory + " is not a directory — move it aside and run the install again"),
            SkillPresence.Ours => Write(page, installed.Body, content),
            _ => Foreign(options, directory, page, installed.Body, content),
        };
    }

    private static Action Foreign(
        InitOptions options, string directory, string page, string current, string content)
    {
        if (!options.Force)
        {
            return new Refuse(
                directory,
                directory + " was not installed by narrativetrace — re-run with --force to overwrite"
                + " this skill, or move the directory aside");
        }

        return Write(page, current, content);
    }

    /// <summary>A write is a create when nothing is there and a replacement when something is.</summary>
    private static Action Write(string page, string current, string content) =>
        current.Length == 0 ? new CreateFile(page, content) : new ReplaceBlock(page, current, content);

    // --- the managed section --------------------------------------------------------------------

    private static void PlanAgentsMd(
        ProjectState state, Carrier carrier, InitOptions options, List<Action> actions)
    {
        var block = AgentsMdBlock.Render(carrier, state);
        var existing = state.AgentsMd;
        if (existing is null)
        {
            actions.Add(new CreateFile(AgentsMd, MarkedBlock.CreatedNote + "\n" + block));
            return;
        }

        actions.Add(BlockAction(AgentsMd, existing, block, options.WriteExisting));
    }

    /// <summary>The same decision for any file that may carry the section: our home and the rule files.</summary>
    private static Action BlockAction(string path, string text, string block, bool mayWriteExisting)
    {
        var scan = MarkedBlock.ScanText(text);
        if (scan.Problems.Count > 0)
        {
            return new Refuse(path, path + ": " + string.Join("; ", scan.Problems));
        }

        if (scan.Regions.Count > 1)
        {
            return new Refuse(path, path + " carries " + Lines(scan) + " — leave exactly one");
        }

        var matched = MarkedBlock.WithEol(block, MarkedBlock.EolOf(text));
        return scan.Regions.Count == 1
            ? new ReplaceBlock(path, text, MarkedBlock.Replace(text, scan.Regions[0], matched))
            : AppendOrRefuse(path, text, matched, mayWriteExisting);
    }

    private static Action AppendOrRefuse(string path, string text, string matched, bool mayWriteExisting)
    {
        if (!mayWriteExisting)
        {
            return new Refuse(
                path, path + " exists and carries no NarrativeTrace section — re-run with"
                + " --write-existing to append one");
        }

        return MarkedBlock.EndsInsideFence(text)
            ? new Refuse(path, UnfinishedFence(path))
            : new AppendBlock(path, text, matched);
    }

    private static string Lines(MarkedBlock.Scan scan) =>
        "two or more NarrativeTrace sections (["
        + string.Join(", ", scan.Regions.Select(region => "line " + region.StartLine))
        + "])";

    private static void PlanClaudeMd(ProjectState state, InitOptions options, List<Action> actions)
    {
        var existing = state.ClaudeMd;
        if (existing is null || MarkedBlock.LineIsIgnoringTrailingSpace(existing, ImportLine) is not null)
        {
            return;
        }

        if (!options.WriteExisting)
        {
            actions.Add(new Refuse(
                ClaudeMd, ClaudeMd + " exists and does not import " + AgentsMd
                + " — re-run with --write-existing to add the one-line import"));
            return;
        }

        actions.Add(MarkedBlock.EndsInsideFence(existing)
            ? new Refuse(ClaudeMd, UnfinishedFence(ClaudeMd))
            : new AppendLine(ClaudeMd, existing, ImportLine));
    }

    /// <summary>
    /// A vendor rule file is never created and never appended to: an existing managed section in one
    /// is kept current, and that is all.
    /// </summary>
    private static void PlanRuleFiles(ProjectState state, Carrier carrier, List<Action> actions)
    {
        var block = AgentsMdBlock.Render(carrier, state);
        foreach (var (path, text) in state.MarkedRuleFiles)
        {
            actions.Add(BlockAction(path, text, block, false));
        }
    }

    /// <summary>
    /// A file that ends inside an unfinished fenced code block cannot be appended to: whatever is added
    /// lands inside that fence, invisible to this installer's own markers and to every Markdown reader,
    /// so the next run would add it again.
    /// </summary>
    private static string UnfinishedFence(string path) =>
        path + " ends inside an unfinished fenced code block — close the fence, and anything appended"
        + " after it will be read as text rather than as code";

    /// <summary>Drops every action that would write what is already there.</summary>
    private static List<Action> WithoutNoOps(List<Action> actions) =>
        actions.Where(ChangesSomething).ToList();

    private static bool ChangesSomething(Action action) =>
        action is not FileEdit edit || !string.Equals(edit.Before, edit.After, StringComparison.Ordinal);
}
