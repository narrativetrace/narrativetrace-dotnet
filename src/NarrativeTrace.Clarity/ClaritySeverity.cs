// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>How much a clarity issue is worth fixing.</summary>
/// <remarks>
/// Derived from the offending identifier's score, not chosen by hand: at or
/// below 0.20 is <see cref="High"/>, at or below 0.50 is
/// <see cref="Medium"/>, anything above is <see cref="Low"/>. The weights
/// drive an issue's impact score, which is what ranks the report.
/// </remarks>
public enum ClaritySeverity
{
    /// <summary>Weight 1 — worth knowing, not worth blocking on.</summary>
    Low,

    /// <summary>Weight 2 — the default for an unscored issue.</summary>
    Medium,

    /// <summary>Weight 3 — the identifier communicates almost nothing.</summary>
    High,
}

/// <summary>Weights and wire labels for <see cref="ClaritySeverity"/>.</summary>
/// <remarks>
/// Labels are fixed upper-case strings shared with the other NarrativeTrace
/// runtimes' <c>clarity-results.json</c>, never derived from .NET enum names.
/// </remarks>
public static class ClaritySeverityExtensions
{
    /// <summary>Ranking weight: High 3, Medium 2, Low 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a defined severity.</exception>
    public static int Weight(this ClaritySeverity severity)
    {
        return severity switch
        {
            ClaritySeverity.High => 3,
            ClaritySeverity.Medium => 2,
            ClaritySeverity.Low => 1,
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity), severity, null),
        };
    }

    /// <summary>The upper-case label used in <c>clarity-results.json</c>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Not a defined severity.</exception>
    public static string JsonName(this ClaritySeverity severity)
    {
        return severity switch
        {
            ClaritySeverity.High => "HIGH",
            ClaritySeverity.Medium => "MEDIUM",
            ClaritySeverity.Low => "LOW",
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity), severity, null),
        };
    }

    /// <summary>Classifies a 0–1 identifier score into a severity.</summary>
    /// <remarks>Thresholds match the Java runtime: ≤0.20 High, ≤0.50 Medium.</remarks>
    public static ClaritySeverity FromScore(double score)
    {
        if (score <= 0.20)
        {
            return ClaritySeverity.High;
        }

        return score <= 0.50 ? ClaritySeverity.Medium : ClaritySeverity.Low;
    }
}
