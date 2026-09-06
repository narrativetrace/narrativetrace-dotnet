// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>Curation lifecycle of a glossary term.</summary>
/// <remarks>
/// Distinguishes machine-harvested entries awaiting review
/// (<see cref="Harvested"/>) from human-reviewed vocabulary
/// (<see cref="Curated"/>) and entries no longer observed in code
/// (<see cref="Stale"/>). Harvesting may create Harvested entries but never
/// changes Curated ones; Stale is set only by an explicit human-invoked
/// operation, never automatically.
/// </remarks>
public enum TermStatus
{
    /// <summary>Machine-harvested from code and awaiting human review. The status a new entry gets.</summary>
    Harvested,

    /// <summary>Human-reviewed and approved. Harvesting never modifies an entry in this state.</summary>
    Curated,

    /// <summary>
    /// No longer observed in the code. Set only by an explicit human-invoked
    /// operation — harvesting never marks a term stale on its own, so absence
    /// from one run does not demote a term.
    /// </summary>
    Stale,
}
