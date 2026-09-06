// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// How informative a method name's leading verb is.
/// </summary>
/// <remarks>
/// The first thing <see cref="MethodNameScorer"/> judges, because the verb is
/// what tells a reader whether the call does something they can predict. The
/// members are a classification, not a scale — do not compare them with
/// <c>&lt;</c>; the scorer maps each to its own weight.
/// </remarks>
public enum VerbCategory
{
    /// <summary>A verb from the project's own domain vocabulary — the most informative kind.</summary>
    Domain,

    /// <summary>A common, well-understood programming verb such as <c>Save</c>, <c>Send</c> or <c>Parse</c>.</summary>
    Standard,

    /// <summary>A predicate verb such as <c>Is</c>, <c>Has</c> or <c>Can</c>, implying a boolean answer.</summary>
    Boolean,

    /// <summary>A verb that says nothing specific — <c>Do</c>, <c>Handle</c>, <c>Process</c>, <c>Manage</c>.</summary>
    Generic,

    /// <summary>The leading token was not recognized as a verb at all.</summary>
    Unknown,
}
