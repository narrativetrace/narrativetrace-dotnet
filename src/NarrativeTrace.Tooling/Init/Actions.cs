// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// The guards every <see cref="Action"/> shares.
/// </summary>
/// <remarks>
/// INTENT: a plan is data, and data can come from anywhere — so the rule that an action never names
/// a path outside the project is enforced where the action is built, not where it is applied. Paths
/// are normalized to a forward-slash, project-relative form so the executor and the renderer both
/// read one shape.
/// </remarks>
internal static class Actions
{
    /// <summary>
    /// The same path, normalized to a forward-slash project-relative string.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// When the path is absent, absolute, empty, or climbs out of the project with <c>..</c>.
    /// </exception>
    internal static string RequireProjectRelative(string path)
    {
        if (path is null)
        {
            throw new ArgumentException("an action needs a path");
        }

        var segments = Normalize(path);
        if (System.IO.Path.IsPathRooted(path) || segments.Count == 0 || segments[0] == "..")
        {
            throw new ArgumentException(
                "an action's path must be project-relative and stay inside it, got \"" + path + "\"");
        }

        return string.Join("/", segments);
    }

    /// <summary>Text an action carries is never null; absence is the empty string.</summary>
    internal static void RequireText(string text)
    {
        if (text is null)
        {
            throw new ArgumentException("an action's text is \"\" when absent, never null");
        }
    }

    /// <summary>
    /// Collapses <c>.</c> and <c>..</c> segments the way a relative path normalizes. Splits on
    /// <c>/</c> only — action and carrier paths are always forward-slash separated, and a backslash is
    /// a legal character in a file name (never a separator here), so it survives into the name.
    /// </summary>
    private static List<string> Normalize(string path)
    {
        var stack = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
            {
                continue;
            }

            if (segment == ".." && stack.Count > 0 && stack[^1] != "..")
            {
                stack.RemoveAt(stack.Count - 1);
            }
            else
            {
                stack.Add(segment);
            }
        }

        return stack;
    }
}
