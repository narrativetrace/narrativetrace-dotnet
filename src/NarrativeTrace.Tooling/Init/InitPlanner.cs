// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Platforms;

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
            PlanPointers(state, carrier, options, actions);
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
        var writable = WritableFlavours(state, options, actions);
        foreach (var skill in carrier.Skills)
        {
            foreach (var flavour in writable)
            {
                actions.Add(SkillAction(state, carrier, options, skill, flavour));
            }
        }
    }

    /// <summary>
    /// The flavours anything may be written into. A flavour whose whole install root is a symbolic
    /// link is refused ONCE, here, rather than once per skill: one link is one decision, and a refusal
    /// per skill would also put several actions on the one path a plan allows only one of.
    /// </summary>
    private static List<SkillFlavour> WritableFlavours(
        ProjectState state, InitOptions options, List<Action> actions)
    {
        var writable = new List<SkillFlavour>();
        foreach (var flavour in Flavours(options))
        {
            var linked = state.LinkedInstallRoot(flavour);
            if (linked is null)
            {
                writable.Add(flavour);
            }
            else
            {
                actions.Add(RefuseLinkedRoot(flavour, linked));
            }
        }

        return writable;
    }

    private static Refuse RefuseLinkedRoot(SkillFlavour flavour, string target) =>
        new(
            flavour.InstallRoot(),
            flavour.InstallRoot() + " is a symbolic link to " + target
            + " — every skill of this flavour would be written through it; remove the link, or run"
            + " the install where it points");

    /// <summary>
    /// Which flavours this project gets: the open standard always, and the vendor one too unless the
    /// caller turned it off.
    /// </summary>
    /// <remarks>
    /// INTENT: Claude Code loads project skills from <c>.claude/skills</c> only — never from
    /// <c>.agents/skills</c>. Writing the vendor flavour only where a <c>.claude/</c> directory or a
    /// <c>CLAUDE.md</c> already existed left a fresh project's skills invisible to the agent the
    /// published prompt runs from, while the doctor reported them installed. Cursor and GitHub Copilot
    /// read <c>.claude/skills</c> too, so no platform pays for the directory; <c>--vendor none</c> is
    /// the opt-out.
    /// </remarks>
    private static IReadOnlyList<SkillFlavour> Flavours(InitOptions options)
    {
        var vendor = options.VendorClaude != Vendor.Off;
        return vendor ? [SkillFlavour.Agents, SkillFlavour.Claude] : [SkillFlavour.Agents];
    }

    private static Action SkillAction(
        ProjectState state, Carrier carrier, InitOptions options, SkillEntry skill, SkillFlavour flavour)
    {
        var page = flavour.InstallRoot() + "/" + skill.Name + "/" + InstalledSkill.Page;
        var rendered = carrier.Body(skill, flavour);
        var content = Provenance.Stamp(rendered, carrier.Coordinate);
        var installed = state.InstalledSkill(flavour, skill.Name);
        if (installed is null)
        {
            return new CreateFile(page, content);
        }

        return installed.Presence switch
        {
            SkillPresence.NotADirectory => new Refuse(
                installed.Directory,
                installed.Directory + " is not a directory — move it aside and run the install again"),
            SkillPresence.Ours => Write(installed.PagePath, installed.Body, content),
            SkillPresence.LinkedDirectory or SkillPresence.LinkedPage =>
                Linked(carrier, skill, installed, content),
            _ => Foreign(options, installed, rendered, content),
        };
    }

    /// <summary>
    /// A symbolic link where a skill's directory or page belongs. Writing through it would land in
    /// whatever it points at — after <c>npx skills add</c>, the OTHER flavour's page — so the link
    /// itself is replaced whenever what it reaches is a page this install owns or would adopt, and
    /// refused otherwise. No flag appears here: <c>--force</c> covers foreign CONTENT, and a link is
    /// structure.
    /// </summary>
    private static Action Linked(
        Carrier carrier, SkillEntry skill, InstalledSkill installed, string content)
    {
        var at = installed.LinkedAt();
        if (installed.Body.Length == 0)
        {
            return new Refuse(
                at,
                at + " is a symbolic link to " + installed.Link
                + ", and there is no page of narrativetrace's at the other end — remove the link and"
                + " run the install again");
        }

        if (!IsOursOrAdoptable(carrier, skill, installed.Body))
        {
            return new Refuse(
                at,
                at + " is a symbolic link to " + installed.Link
                + ", a page narrativetrace did not install — remove the link and run the install"
                + " again; --force covers content, never a link");
        }

        return new ReplaceLink(at, installed.PagePath, installed.Link, content);
    }

    /// <summary>
    /// Whether a page reached through a link is one this install would own anyway: already stamped, or
    /// identical to what this carrier renders for EITHER flavour. Either flavour, because the link a
    /// registry leaves at the vendor path points at the open-standard page.
    /// </summary>
    private static bool IsOursOrAdoptable(Carrier carrier, SkillEntry skill, string body)
    {
        if (Provenance.CoordinateIn(body) is not null)
        {
            return true;
        }

        return Adoption.IsAdoptable(body, carrier.Body(skill, SkillFlavour.Agents))
            || Adoption.IsAdoptable(body, carrier.Body(skill, SkillFlavour.Claude));
    }

    /// <summary>
    /// A directory somebody else's tool wrote. A page equal to what this carrier renders is ADOPTED —
    /// it is our own page, installed by a registry rather than by us, so stamping it takes nothing from
    /// anybody. Anything else is a refusal until <c>--force</c> says otherwise.
    /// </summary>
    private static Action Foreign(
        InitOptions options, InstalledSkill installed, string rendered, string content)
    {
        if (Adoption.IsAdoptable(installed.Body, rendered))
        {
            return new AdoptPage(installed.PagePath, installed.Body, content);
        }

        if (!options.Force)
        {
            return new Refuse(
                installed.Directory,
                installed.Directory + " was not installed by narrativetrace — re-run with --force to overwrite"
                + " this skill, or move the directory aside");
        }

        return Write(installed.PagePath, installed.Body, content);
    }

    /// <summary>A write is a create when nothing is there and a replacement when something is.</summary>
    private static Action Write(string page, string current, string content) =>
        current.Length == 0 ? new CreateFile(page, content) : new ReplaceBlock(page, current, content);

    // --- the managed section --------------------------------------------------------------------

    private static void PlanAgentsMd(
        ProjectState state, Carrier carrier, InitOptions options, List<Action> actions)
    {
        actions.Add(ManagedHome(AgentsMd, state.AgentsMd, AgentsMdBlock.Render(carrier, state), options));
    }

    /// <summary>
    /// A markdown file that is ours to create and may carry the section: made with the created note
    /// when absent, kept current when it has a section, appended to only on <c>--write-existing</c>.
    /// </summary>
    private static Action ManagedHome(string path, string? existing, string block, InitOptions options) =>
        existing is null
            ? new CreateFile(path, MarkedBlock.CreatedNote + "\n" + block)
            : BlockAction(path, existing, block, options.WriteExisting);

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

    // --- platform pointers ----------------------------------------------------------------------

    /// <summary>
    /// One pointer per DETECTED platform whose row has one. The marker decides, never a flag: a
    /// project not on Cursor gets no <c>.cursor/</c> directory it did not have.
    /// </summary>
    private static void PlanPointers(
        ProjectState state, Carrier carrier, InitOptions options, List<Action> actions)
    {
        foreach (var row in PlatformTable.Rows.Where(row => state.DetectedPlatforms.Contains(row.Id)))
        {
            if (PlanPointer(row.Pointer, state, carrier, options) is { } action)
            {
                actions.Add(action);
            }
        }
    }

    private static Action? PlanPointer(Pointer pointer, ProjectState state, Carrier carrier, InitOptions options)
    {
        if (pointer is Pointer.None)
        {
            return null;
        }

        if (state.PointerBehindLink(pointer.Path))
        {
            return RefuseLink(pointer.Path);
        }

        var existing = state.PointerFile(pointer.Path);
        return pointer is Pointer.Rule rule
            ? PlanRule(rule, existing, carrier)
            : ManagedHome(pointer.Path, existing, AgentsMdBlock.Render(carrier, state), options);
    }

    private static Refuse RefuseLink(string path) =>
        new(path, path + " is behind a symbolic link — nothing is written through one; remove the link");

    /// <summary>
    /// The rule file: made when absent, kept current when ours, ADOPTED when it is the unstamped
    /// plugin copy byte for byte, and refused when it carries no section of ours at all.
    /// </summary>
    private static Action PlanRule(Pointer.Rule rule, string? existing, Carrier carrier)
    {
        var wanted = CursorRule.Render(rule.Text, carrier.Coordinate);
        if (existing is null)
        {
            return new CreateFile(rule.Path, wanted);
        }

        if (Adoption.IsAdoptable(existing, CursorRule.Render(rule.Text, "")))
        {
            return new AdoptPage(rule.Path, existing, wanted);
        }

        var scan = MarkedBlock.ScanText(existing);
        return scan.HasExactlyOneRegion
            ? ReplaceRuleSection(rule.Path, existing, wanted[CursorRule.Frontmatter.Length..], scan.Regions[0])
            : new Refuse(
                rule.Path,
                rule.Path + " was not written by narrativetrace (it has no single NarrativeTrace section) —"
                + " move it aside and run the install again");
    }

    private static ReplaceBlock ReplaceRuleSection(
        string path, string existing, string section, MarkedBlock.Region region) =>
        new(path, existing, MarkedBlock.Replace(existing, region, MarkedBlock.WithEol(section, MarkedBlock.EolOf(existing))));

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
