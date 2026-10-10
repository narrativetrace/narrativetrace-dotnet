// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// One row of the shared <c>feedback.json</c>: a piece of problem-report text and the verdict the
/// value-free gate must reach on it.
/// </summary>
/// <param name="Id">Stable kebab-case identifier, quoted by a failing assertion so the case is findable.</param>
/// <param name="Description">What the row proves, not what the bytes are.</param>
/// <param name="Value">The text the gate reads.</param>
/// <param name="Expect"><c>"accepted"</c> (no rule may refuse) or <c>"rejected"</c>.</param>
/// <param name="Rule">For a rejected row, the rule id that must appear among those refusing it; else <see langword="null"/>.</param>
public sealed record FeedbackCase(string Id, string Description, string Value, string Expect, string? Rule)
{
    /// <summary>Whether the gate must refuse this row.</summary>
    public bool IsRejected => Expect == "rejected";

    /// <inheritdoc />
    public override string ToString() => Id;
}

/// <summary>
/// Reads the shared cross-runtime <c>feedback.json</c> (the master is the Java repo; this port copies
/// it verbatim). One loader, so the replay reads every row the same way.
/// </summary>
public static class FeedbackCorpus
{
    /// <summary>Every row, in file order.</summary>
    public static IReadOnlyList<FeedbackCase> Cases()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "HostileCorpus", "feedback.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"feedback corpus not found: {path}");
        }

        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        var result = new List<FeedbackCase>();
        foreach (var node in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            result.Add(new FeedbackCase(
                node.GetProperty("id").GetString()!,
                node.GetProperty("description").GetString()!,
                node.GetProperty("value").GetString()!,
                node.GetProperty("expect").GetString()!,
                node.TryGetProperty("rule", out var rule) ? rule.GetString() : null));
        }

        return result;
    }
}
