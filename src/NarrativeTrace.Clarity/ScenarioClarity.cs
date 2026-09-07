// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// One scenario's clarity result, paired with the scenario it came from.
/// </summary>
/// <remarks>
/// <para>
/// The unit a suite-level report is built from: analysis produces a
/// <see cref="ClarityResult"/> with no memory of its origin, and this attaches
/// that origin back. Scenario names are not unique across a suite, so treat this
/// as a labelled pair rather than a keyed entry.
/// </para>
/// <para>
/// Not to be confused with <see cref="NarrativeTrace.Core.ScenarioResult"/>,
/// which is the success/error <i>outcome</i> of a scenario. This type carries
/// its naming scores. (It was itself called <c>ScenarioResult</c> until
/// 2026-08-28, when Core adopted the cross-runtime outcome enum of that name.)
/// </para>
/// </remarks>
/// <param name="Scenario">The scenario name, as it appeared in the trace.</param>
/// <param name="Result">The scores and issues for that scenario.</param>
public sealed record ScenarioClarity(
    string Scenario,
    ClarityResult Result);
