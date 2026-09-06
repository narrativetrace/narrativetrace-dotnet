// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The canonical artifact schema every exporter stamps its output with.
/// </summary>
/// <remarks>
/// <para>
/// One constant so a version bump cannot land in some artifacts and not others.
/// The version is emitted as <c>nt.schemaVersion</c> from six independent
/// places — the canonical entry, chapter export, the two logging bridges, the
/// ASP.NET Core log scope, and the OTel span tags — and a mismatch between them
/// is invisible to schema validation, because each artifact stays individually
/// valid while disagreeing about which contract it satisfies.
/// </para>
/// <para>
/// This is the <b>cross-port</b> contract: the Java edition stamps the same
/// value, and consumers key their parsing off it. Bumping it is a coordinated
/// change across ports, not a local edit — see item 2 in the repository task
/// list at the root.
/// </para>
/// </remarks>
public static class CanonicalSchema
{
    /// <summary>
    /// The schema version stamped into every canonical artifact, as
    /// <c>major.minor</c>.
    /// </summary>
    /// <remarks>
    /// Do not hard-code this value at an emission site. Reference it, so a bump
    /// reaches every artifact at once.
    /// </remarks>
    public const string Version = "1.2";
}
