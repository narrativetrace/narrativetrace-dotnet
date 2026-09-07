// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// Which machine-readable entry arrays a run writes beside its trace file.
/// </summary>
/// <remarks>
/// Both are opt-in and independent: the canonical array is for schema
/// consumers (other runtimes, conformance fixtures), the structural one is the same
/// array with every runtime value elided (ADR-002 Level 1) for AI consumers.
/// <see langword="default"/> writes neither, which is what a run that never
/// asked for them gets.
/// </remarks>
/// <param name="Canonical">Write <c>&lt;test&gt;.canonical.json</c>.</param>
/// <param name="Structural">Write <c>&lt;test&gt;.structural.json</c>.</param>
public readonly record struct EntryArtifacts(bool Canonical, bool Structural)
{
    /// <summary>Whether either artifact was asked for.</summary>
    public bool Any => Canonical || Structural;
}
