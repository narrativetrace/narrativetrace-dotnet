// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Abbreviation quality tiers, mirroring the Java clarity model.
/// </summary>
/// <remarks>
/// Only abbreviations in the dictionary are classified; an unrecognized short
/// token is judged as an ordinary token rather than being placed in a tier here.
/// </remarks>
public enum AbbreviationTier
{
    /// <summary>
    /// Understood by essentially every developer — <c>id</c>, <c>url</c>,
    /// <c>http</c>, <c>json</c>. Expanding these would hurt readability, so they
    /// are not penalized.
    /// </summary>
    Universal,

    /// <summary>Widely understood but context-dependent; mildly penalized against the spelled-out form.</summary>
    WellKnown,

    /// <summary>Has more than one plausible expansion — <c>tmp</c>, <c>val</c>, <c>proc</c>. Penalized.</summary>
    Ambiguous,
}
