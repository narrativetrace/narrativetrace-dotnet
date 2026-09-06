// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// Classification of a class-name role suffix, mirroring the Java clarity
/// model.
/// </summary>
public enum RoleSuffixCategory
{
    /// <summary>
    /// Names a recognized pattern — <c>Factory</c>, <c>Repository</c>,
    /// <c>Builder</c>, <c>Adapter</c>. Communicates a role precisely, because
    /// the reader already knows the pattern.
    /// </summary>
    DesignPattern,

    /// <summary>Describes what the type does — <c>Validator</c>, <c>Renderer</c>, <c>Parser</c>.</summary>
    Functional,

    /// <summary>
    /// A suffix that adds no information — <c>Manager</c>, <c>Helper</c>,
    /// <c>Util</c>, <c>Data</c>. Usually a sign the type has no single
    /// responsibility to name.
    /// </summary>
    Generic,

    /// <summary>No recognized role suffix. Not a penalty — plenty of good class names have none.</summary>
    Unknown,
}
