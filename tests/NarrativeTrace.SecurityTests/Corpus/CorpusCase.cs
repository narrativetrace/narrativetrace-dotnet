// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// One hostile scalar from <c>strings.json</c> or <c>injection.json</c>.
/// </summary>
/// <param name="Id">Stable kebab-case identifier, quoted by a failing assertion so the case is findable.</param>
/// <param name="Description">What breaks, not what the bytes are.</param>
/// <param name="Value">The materialized value, <c>repeat</c> already expanded.</param>
public sealed record CorpusCase(string Id, string Description, string Value)
{
    /// <inheritdoc />
    public override string ToString() => Id;
}
