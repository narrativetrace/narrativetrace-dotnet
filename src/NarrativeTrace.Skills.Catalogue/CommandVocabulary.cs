// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills;

/// <summary>
/// The dotnet port's closed command vocabulary (skill-harness design principle 7): a
/// <see cref="CommandStep"/> may only invoke tooling this port guarantees present. Repo-local
/// wrappers beat global binaries; a skill may never instruct installing a global tool.
/// </summary>
public static class CommandVocabulary
{
    /// <summary>The only first tokens a command step's lines may use.</summary>
    public static readonly IReadOnlyList<string> AllowedTools = ["dotnet", "git"];

    /// <summary>The first whitespace-separated token of a command line, or empty if it has none.</summary>
    public static string FirstToken(string command)
    {
        var trimmed = command.TrimStart();
        var spaceIndex = trimmed.IndexOf(' ', StringComparison.Ordinal);
        return spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
    }

    /// <summary>Whether every command's first token is in <see cref="AllowedTools"/>.</summary>
    public static bool IsAllowed(string command)
    {
        return AllowedTools.Contains(FirstToken(command));
    }

    /// <summary>
    /// The Claude-flavour <c>allowed-tools</c> spelling of a vocabulary command: the
    /// <c>Bash(&lt;command&gt; *)</c> tool pattern, e.g. <c>git</c> to <c>Bash(git *)</c>.
    /// </summary>
    /// <remarks>
    /// Verified against Claude Code's documented syntax: a permission rule follows the format
    /// <c>Tool</c> or <c>Tool(specifier)</c>, and <c>allowed-tools</c> lists tools, not commands —
    /// so a bare <c>git</c> names a tool that does not exist and pre-approves nothing, while the
    /// tool the steps actually use (<c>Bash</c>) stays unlisted. The trailing <c>" *"</c> is
    /// load-bearing twice over: a rule's wildcard must sit after the subcommand, and a trailing
    /// <c>" *"</c> also matches the bare command, which is what lets <c>Bash(dotnet *)</c> cover a
    /// plain <c>dotnet</c>. This is the ONLY place a platform spelling of the vocabulary is
    /// written — <see cref="SkillLints.AllowedToolsViolations"/> pins the inverse: the vocabulary
    /// itself must declare bare commands, never a pre-spelled pattern.
    /// </remarks>
    public static string ClaudeToolPattern(string command)
    {
        return $"Bash({command} *)";
    }
}
