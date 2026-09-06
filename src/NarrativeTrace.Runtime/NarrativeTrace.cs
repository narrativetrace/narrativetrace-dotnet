// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
namespace NarrativeTrace.Runtime;

/// <summary>
/// Library-wide constants for the NarrativeTrace runtime.
/// </summary>
/// <remarks>
/// Note the name collides with the <c>NarrativeTrace</c> root namespace. Inside
/// the <c>NarrativeTrace.Runtime</c> namespace the type wins, so a namespace
/// qualification such as <c>NarrativeTrace.Core</c> may fail to resolve there —
/// qualify from the global namespace (<c>global::NarrativeTrace.Core</c>) if it
/// does.
/// </remarks>
public static class NarrativeTrace
{
    /// <summary>
    /// The library's product version, matching the packages' <c>VersionPrefix</c>.
    /// </summary>
    /// <remarks>
    /// Pre-1.0 and hard-coded, so it reports the version the assembly was
    /// compiled at. Stamped into exported artifacts for provenance; it is not a
    /// feature-detection mechanism.
    /// </remarks>
    public static string Version => "0.1.0";
}
