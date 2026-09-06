// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// One wire-header value from <c>headers.json</c>.
/// </summary>
/// <param name="Id">Stable kebab-case identifier.</param>
/// <param name="Description">What the case is testing.</param>
/// <param name="Value">The materialized header value.</param>
/// <param name="Accepted">
/// Whether a conforming parser must accept it; every other case must be refused without throwing.
/// </param>
public sealed record HeaderCase(string Id, string Description, string Value, bool Accepted)
{
    /// <inheritdoc />
    public override string ToString() => Id;
}
