// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// A DDD bounded context declared in the glossary, mapped to namespace
/// prefixes.
/// </summary>
/// <remarks>
/// Contexts scope term identity — the same normalized term may exist
/// independently in two contexts with different definitions and translations.
/// Namespace prefixes are matched delimiter-aware by
/// <see cref="ContextResolver"/>; an empty prefix list is valid (the
/// <c>_unassigned</c> fallback context declares none).
/// </remarks>
public sealed record BoundedContext
{
    /// <param name="name">Context name, unique within a glossary; never blank.</param>
    /// <param name="packages">Namespace prefixes owned by this context; may be empty.</param>
    /// <param name="description">Human-written summary of the context's domain.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="packages"/> is null.</exception>
    public BoundedContext(
        string name, IReadOnlyList<string> packages, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("context name must not be blank", nameof(name));
        }

        if (packages is null)
        {
            throw new ArgumentNullException(nameof(packages));
        }

        Name = name;
        Packages = packages.ToArray();
        Description = description;
    }

    /// <summary>Context name, unique within a glossary; never blank.</summary>
    public string Name { get; }

    /// <summary>Namespace prefixes owned by this context; may be empty, never null.</summary>
    public IReadOnlyList<string> Packages { get; }

    /// <summary>Human-written summary of the context's domain, or null.</summary>
    public string? Description { get; }
}
