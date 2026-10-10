// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// One refusal: the report field that carried the offending text, and the rule that refused it.
/// </summary>
/// <remarks>
/// INTENT: a refusal has to be actionable. "This report cannot be filed" sends a person looking
/// through every field; "<c>happened</c> breaks <c>vf.rendered-call</c>" sends them to one line.
/// </remarks>
/// <param name="Field">The report field's own name, as the draft prints it.</param>
/// <param name="Rule">The rule that refused it.</param>
public sealed record ValueFreeViolation(string Field, ValueFreeRule Rule)
{
    /// <summary>The report field's own name, as the draft prints it.</summary>
    public string Field { get; } =
        string.IsNullOrWhiteSpace(Field)
            ? throw new ArgumentException("a violation names the field it was found in", nameof(Field))
            : Field;

    /// <summary>The rule that refused it.</summary>
    public ValueFreeRule Rule { get; } =
        Rule ?? throw new ArgumentNullException(nameof(Rule), "a violation names the rule that refused it");

    /// <summary>The one line a refusal prints: the field, the rule id, and what to do about it.</summary>
    public string Describe() => $"{Field}: {Rule.Id} — {Rule.Reason}";
}
