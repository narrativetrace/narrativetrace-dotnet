// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// The participant lane-up of a sequence diagram: every class the trace touched, in
/// first-appearance order.
/// </summary>
/// <remarks>
/// Both sequence renderers declare their lanes before emitting a single arrow, and both need
/// exactly the same list in exactly the same order — the order is what makes the two diagrams
/// comparable, and what makes either of them stable across runs. It was a private twin
/// (<c>CollectParticipants</c>) in each renderer before this class existed.
/// </remarks>
internal static class SequenceParticipants
{
    /// <summary>
    /// Collects the participant class names reachable from <paramref name="nodes"/>, in
    /// first-appearance order.
    /// </summary>
    /// <param name="nodes">The (already-bounded) tree roots to walk.</param>
    /// <returns>An insertion-ordered list of raw class names, unsanitized — quoting is the caller's step.</returns>
    public static List<string> Collect(IReadOnlyList<TraceNode> nodes)
    {
        var seen = new HashSet<string>();
        var result = new List<string>();
        CollectInto(nodes, seen, result);
        return result;
    }

    private static void CollectInto(
        IReadOnlyList<TraceNode> nodes, HashSet<string> seen, List<string> result)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (seen.Add(nodes[i].Signature.ClassName))
            {
                result.Add(nodes[i].Signature.ClassName);
            }

            CollectInto(nodes[i].Children, seen, result);
        }
    }
}
