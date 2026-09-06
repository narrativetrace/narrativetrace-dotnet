// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.DependencyInjection;

/// <summary>
/// Matches a type's namespace against a set of configured base namespaces
/// using dot-boundary semantics, so a sibling namespace with a common
/// prefix (e.g. <c>MyApp2</c> vs base <c>MyApp</c>) does not falsely match.
/// </summary>
internal sealed class NamespaceMatcher
{
    private readonly IReadOnlyList<string> _baseNamespaces;

    public NamespaceMatcher(IReadOnlyList<string> baseNamespaces)
    {
        _baseNamespaces = baseNamespaces;
    }

    public bool Matches(string? ns)
    {
        if (string.IsNullOrEmpty(ns))
        {
            return false;
        }

        // S3267: LINQ allocates a closure on a per-resolved-type hot path.
#pragma warning disable S3267
        foreach (var @base in _baseNamespaces)
        {
            if (ns == @base
                || ns!.StartsWith(
                    @base + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }
#pragma warning restore S3267

        return false;
    }
}
