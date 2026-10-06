// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills;

/// <summary>
/// One Pro-tier catalogue mention. Never claims price or payment — the mark is
/// "Pro" plus <see cref="Status"/>, nothing more.
/// </summary>
/// <param name="CanonicalName">The Pro feature's stable name.</param>
/// <param name="Prompt">A trigger phrasing a user might type that this feature would answer.</param>
/// <param name="Delivers">What the feature does for the user, in one clause.</param>
/// <param name="Needs">A prerequisite the free tier doesn't already provide, if any.</param>
/// <param name="ComesFrom">Where the capability is implemented, if worth naming.</param>
/// <param name="Status">One of <c>shipped</c>, <c>in development</c>, <c>planned</c>.</param>
/// <param name="FeatureGuideStatusText">
/// The exact substring this must find verbatim in <c>documentation/feature-guide.md</c> — the
/// Tier A lint that keeps this listing from drifting from the source of truth.
/// </param>
public sealed record ProListing(
    string CanonicalName,
    string Prompt,
    string Delivers,
    string? Needs,
    string? ComesFrom,
    string Status,
    string FeatureGuideStatusText);
