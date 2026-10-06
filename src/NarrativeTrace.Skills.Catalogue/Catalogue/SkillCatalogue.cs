// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>The assembled catalogue: every shipped skill and Pro-tier listing.</summary>
public static class SkillCatalogue
{
    /// <summary>Every skill this repository ships, in a stable order.</summary>
    public static readonly IReadOnlyList<Skill> Skills =
    [
        NarrativeTraceDoctorSkill.Definition,
        AddNarrativeTracingSkill.Definition,
    ];

    /// <summary>Every Pro-tier listing the catalogue advertises.</summary>
    public static readonly IReadOnlyList<ProListing> Pro = ProListings.All;
}
