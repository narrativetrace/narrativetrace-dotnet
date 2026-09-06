// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Renders a finished <see cref="TraceTree"/> as a JSON array of canonical
/// entries — the machine-readable per-test artifact.
/// </summary>
/// <remarks>
/// Two artifacts, one shape. <see cref="Canonical"/> is the input of canonical-
/// schema consumers (ports, conformance fixtures); <see cref="Structural"/> is
/// the same array with every runtime value elided (ADR-002 Level 1), for
/// handing to an AI consumer. They share this renderer deliberately: a
/// structural artifact that drifted from the canonical shape would stop being
/// the same trace, and only the projection may differ between them.
/// </remarks>
public static class CanonicalEntryArrayExporter
{
    /// <summary>Renders the tree as the canonical entry array, values included.</summary>
    /// <param name="tree">The finished trace; must not be null.</param>
    /// <returns>A JSON array of entries, in depth-first emission order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is null.</exception>
    public static string Canonical(TraceTree tree)
    {
        return Render(tree, entry => entry);
    }

    /// <summary>Renders the tree as the value-free structural entry array.</summary>
    /// <param name="tree">The finished trace; must not be null.</param>
    /// <returns>
    /// The same array <see cref="Canonical"/> produces, with every runtime-value
    /// field elided by <see cref="StructuralProjection"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is null.</exception>
    public static string Structural(TraceTree tree)
    {
        return Render(tree, StructuralProjection.Project);
    }

    private static string Render(
        TraceTree tree, Func<CanonicalEntry, CanonicalEntry> projection)
    {
        var entries = TraceTreeCanonicalMapper.FromTree(tree)
            .Select(projection)
            .Select(CanonicalEntrySerializer.ToJson);
        return "[\n" + string.Join(",\n", entries) + "\n]";
    }
}
