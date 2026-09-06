// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using System.Text.RegularExpressions;

namespace NarrativeTrace.Core;

/// <summary>
/// Scans a captured trace for surviving <c>{placeholder}</c> tokens in
/// resolved <c>[Narrated]</c> / <c>[OnError]</c> text — the signal that a
/// template referenced a parameter or property that does not exist. Mirrors
/// the Java <c>TemplateWarningCollector</c>.
/// </summary>
public static class TemplateWarningCollector
{
    private static readonly Regex Placeholder =
        new(@"\{[^}]+\}", RegexOptions.Compiled);

    /// <summary>
    /// Returns one warning per unresolved placeholder, formatted as
    /// <c>Class.method: {token} in narration|errorContext</c>.
    /// </summary>
    public static IReadOnlyList<string> Collect(TraceTree tree)
    {
        var warnings = new List<string>();
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so the recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        CollectNodes(TreeWalk.Bound(tree.Roots), warnings);
        return warnings;
    }

    /// <summary>
    /// Renders a warning block — a header line plus one indented bullet per
    /// warning — or the empty string when there are none. Mirrors the Java
    /// <c>TemplateWarningCollector.format</c> so both test frameworks emit an
    /// identical, single-sourced message.
    /// </summary>
    public static string Format(IReadOnlyList<string> warnings)
    {
        if (warnings.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.Append("WARNING: Unresolved template placeholder(s) detected:\n");
        for (var i = 0; i < warnings.Count; i++)
        {
            sb.Append("  - ").Append(warnings[i]).Append('\n');
        }

        return sb.ToString();
    }

    private static void CollectNodes(
        IReadOnlyList<TraceNode> nodes, List<string> warnings)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            var sig = nodes[i].Signature;
            AppendWarnings(sig, sig.Narration, "narration", warnings);
            AppendWarnings(sig, sig.ErrorContext, "errorContext", warnings);
            CollectNodes(nodes[i].Children, warnings);
        }
    }

    private static void AppendWarnings(
        MethodSignature sig, string? text, string field,
        List<string> warnings)
    {
        if (text is null)
        {
            return;
        }

        foreach (Match match in Placeholder.Matches(text))
        {
            warnings.Add(
                $"{sig.ClassName}.{sig.MethodName}: {match.Value} in {field}");
        }
    }
}
