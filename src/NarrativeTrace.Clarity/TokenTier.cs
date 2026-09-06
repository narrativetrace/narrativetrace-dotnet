// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Genericity tiers for a name token, mirroring the Java clarity model.
/// Ordered least → most specific.
/// </summary>
public enum TokenTier
{
    /// <summary>Carries no information at all — <c>data</c>, <c>temp</c>, <c>foo</c>, <c>x</c>.</summary>
    Meaningless,

    /// <summary>Gestures at a meaning without pinning one down — <c>info</c>, <c>value</c>, <c>item</c>.</summary>
    Vague,

    /// <summary>Generic but type-anchored, so it says something — <c>orderList</c>, <c>userMap</c>.</summary>
    TypedGeneric,

    /// <summary>Specific and domain-bearing. The goal.</summary>
    NotGeneric,
}
