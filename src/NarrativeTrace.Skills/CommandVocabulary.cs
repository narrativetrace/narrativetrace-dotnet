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
}
