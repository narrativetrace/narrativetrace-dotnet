// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Shared layout helpers for rendering fire-and-forget groups. A pure launcher
/// stub — the synthetic node whose method is <c>fire-and-forget</c> with no
/// captured body — signals background work whose result was not awaited.
/// </summary>
internal static class FireAndForgetSection
{
    public static IReadOnlyList<TraceNode> Body(
        IReadOnlyList<TraceNode> nodes)
    {
        var body = new List<TraceNode>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!IsLauncherStub(nodes[i]))
            {
                body.Add(nodes[i]);
            }
        }

        return body;
    }

    private static bool IsLauncherStub(TraceNode node)
    {
        return node.Signature.MethodName == "fire-and-forget"
            && node.Children.Count == 0;
    }
}
