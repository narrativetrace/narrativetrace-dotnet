// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Platforms;

/// <summary>
/// The platform table (Phase 8 D1): ONE committed registry of the agent platforms NarrativeTrace is
/// used from, how a project proves it is on one, which pointer <c>init</c> writes for it, how a
/// stranger installs the skills, and which evidence lane exercises it.
/// </summary>
/// <remarks>
/// <para>INTENT: the framework table's sibling, same shape. <c>init</c>'s plan, <c>uninstall</c>, the
/// doctor's <c>config.skills-installed</c>, the Cursor plugin manifest and
/// <c>documentation/use-with-your-agent.md</c> all read these rows, and none of them is typed twice.</para>
/// <para>Every platform here already discovers the pages <c>init</c> writes under
/// <c>.agents/skills</c> (and <c>.claude/skills</c>); a row never adds a third page flavour. Row ids are
/// a cross-port contract — append, never rename. The platform facts carry their verification date
/// because a docs-as-tests check cannot reach the vendors: re-verify them at each release and move
/// the date.</para>
/// </remarks>
public static class PlatformTable
{
    /// <summary>The date every fact below was last checked against the vendors' own documentation.</summary>
    private const string Verified = "2026-10-10";

    private const string Init = "run `dotnet tool run dotnet-narrativetrace init` in the project";

    /// <summary>Every row, in table order — the order the docs list them and the planners walk them.</summary>
    public static readonly IReadOnlyList<PlatformRow> Rows =
        [Cursor(), Replit(), Antigravity(), GitHubCopilot(), ClaudeCode()];

    /// <summary>The row with this id, or <see langword="null"/> when the table has none.</summary>
    public static PlatformRow? Row(string id) =>
        Rows.FirstOrDefault(row => string.Equals(row.Id, id, StringComparison.Ordinal));

    private static PlatformRow Cursor() => new(
        "cursor",
        "Cursor",
        [".cursor"],
        new Pointer.Rule(
            ".cursor/rules/narrativetrace.mdc",
            "The NarrativeTrace skills live under `.agents/skills` — read them before changing traced code," +
            " and run the NarrativeTrace doctor to check the install."),
        "Install the plugin from the public repository's `.cursor-plugin` manifest, or " + Init,
        "the skills in `.agents/skills` (Cursor also reads `.claude/skills`), and the one rule" +
        " `.cursor/rules/narrativetrace.mdc` that points at them",
        "cursor",
        Verified);

    private static PlatformRow Replit() => new(
        "replit",
        "Replit",
        ["replit.md", ".replit"],
        new Pointer.ManagedSection("replit.md"),
        "Import the public repository from GitHub (paste its URL, preview the skills, add them), or " + Init,
        "the skills in `.agents/skills`, and a NarrativeTrace section in `replit.md`, the project memory" +
        " the Replit agent reads first",
        PlatformRow.Manual,
        Verified);

    /// <summary>
    /// Antigravity (Google's agentic IDE and its <c>agy</c> CLI). Its markers are its OWN project layout —
    /// <c>.agents/rules</c> and <c>.agents/workflows</c>, legacy <c>.agent/</c> — never
    /// <c>.agents/skills</c>, which <c>init</c> itself writes. No registry install path:
    /// <c>agy plugin install</c> reads only a plugin's top-level <c>skills/</c> directory, which this
    /// repository does not ship.
    /// </summary>
    private static PlatformRow Antigravity() => new(
        "antigravity",
        "Antigravity",
        [".agents/rules", ".agents/workflows", ".agent"],
        new Pointer.None(),
        "Nothing to install beyond the project itself: " + Init + " and commit the skills (the `agy` CLI" +
        " and the IDE read them as workspace skills)",
        "the skills in `.agents/skills`, which Antigravity reads as workspace skills, and the section in" +
        " `AGENTS.md`, which it loads as rules",
        "antigravity",
        Verified);

    private static PlatformRow GitHubCopilot() => new(
        "github-copilot",
        "GitHub Copilot",
        [".github/copilot-instructions.md"],
        new Pointer.None(),
        "Nothing to install beyond the repository itself: " + Init + " and commit the skills",
        "the skills in `.agents/skills` (Copilot also reads `.claude/skills` and `.github/skills`), and" +
        " the section in `AGENTS.md`",
        PlatformRow.Manual,
        Verified);

    private static PlatformRow ClaudeCode() => new(
        "claude-code",
        "Claude Code",
        [".claude"],
        new Pointer.None(),
        "Add the repository as a plugin marketplace and install its plugin, or " + Init,
        "the skills in `.claude/skills` and `.agents/skills`, and the section in `AGENTS.md` that" +
        " `CLAUDE.md` imports",
        "claude",
        Verified);
}
