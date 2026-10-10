// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The three sentences that are the report: what was done, what happened, what was expected.
/// </summary>
/// <remarks>
/// INTENT: all three are mandatory, and that is the whole design of the field set. "It does not work"
/// is not a report; the difference between what happened and what was expected is what makes one
/// triageable without a conversation, and a conversation is exactly what nobody gets when the reporter
/// is an agent that has already ended its session.
/// </remarks>
/// <param name="Did">What the reporter did.</param>
/// <param name="Happened">What actually happened.</param>
/// <param name="Expected">What was expected instead.</param>
public sealed record ProblemNarrative(string Did, string Happened, string Expected)
{
    /// <summary>What the reporter did.</summary>
    public string Did { get; } = Require(Did, nameof(Did));

    /// <summary>What actually happened.</summary>
    public string Happened { get; } = Require(Happened, nameof(Happened));

    /// <summary>What was expected instead.</summary>
    public string Expected { get; } = Require(Expected, nameof(Expected));

    private static string Require(string value, string field) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(
                $"a report's \"{field}\" must say something — all three sentences are mandatory", field)
            : value;
}
