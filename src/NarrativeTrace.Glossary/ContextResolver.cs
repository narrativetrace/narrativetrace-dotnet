// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Glossary;

/// <summary>
/// Resolves a .NET namespace to the bounded context that owns it.
/// </summary>
/// <remarks>
/// Contexts declare namespace prefixes in the glossary file (single source of
/// truth; the file key is <c>packages</c>, shared across every NarrativeTrace runtime).
/// Matching is delimiter-aware — <c>Acme.Billing</c> owns
/// <c>Acme.Billing.Overdraft</c> but not <c>Acme.Billingx</c>. The longest
/// matching prefix wins, so nested contexts are possible. No match falls back
/// to <see cref="Unassigned"/>, letting harvesting work with zero
/// configuration. Same boundary semantics as the dependency-injection
/// module's <c>NamespaceMatcher</c>.
/// </remarks>
public sealed class ContextResolver
{
    /// <summary>Fallback context for namespaces no declared context owns.</summary>
    public const string Unassigned = "_unassigned";

    private readonly Glossary glossary;

    /// <param name="glossary">Glossary whose declared contexts drive resolution; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="glossary"/> is null.</exception>
    public ContextResolver(Glossary glossary)
    {
        this.glossary = glossary
            ?? throw new ArgumentNullException(nameof(glossary));
    }

    /// <summary>Resolves a namespace to a declared context name.</summary>
    /// <param name="namespaceName">Namespace of the declaring class; must not be null (may be empty).</param>
    /// <returns>
    /// The owning context's name, or <see cref="Unassigned"/> when no declared
    /// prefix matches.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="namespaceName"/> is null.</exception>
    public string Resolve(string namespaceName)
    {
        if (namespaceName is null)
        {
            throw new ArgumentNullException(nameof(namespaceName));
        }

        var best = Unassigned;
        var bestLength = -1;
        // Sorted iteration makes equal-length prefix ties deterministic (first name wins).
        foreach (var name in glossary.Contexts.Keys.OrderBy(n => n, StringComparer.Ordinal))
        {
            foreach (var prefix in glossary.Contexts[name].Packages)
            {
                if (Owns(prefix, namespaceName) && prefix.Length > bestLength)
                {
                    best = name;
                    bestLength = prefix.Length;
                }
            }
        }

        return best;
    }

    /// <summary>
    /// The namespace a call's bounded context is resolved from — the one rule both halves of the
    /// module use, so a term is always looked up in the context it was filed under.
    /// </summary>
    /// <remarks>
    /// Identity captured at the site is authoritative: it is the declaring type's real namespace,
    /// recorded where the truth was known. The simple-name index is the fallback for a capture that
    /// predates the captured field, and it is only ever a re-derivation — it answers <see
    /// langword="null"/> for a class that ships in a referenced assembly the index never scanned and
    /// for a simple name two namespaces share, and it can answer for the wrong one of two
    /// same-named classes.
    ///
    /// <para>Harvesting once resolved through the index alone while translation preferred the
    /// captured namespace. The two then disagreed exactly where the index is weakest, and the term
    /// landed in one context while the render looked in another — a glossary gap that curating the
    /// term could not clear, because the lookup was never going to the context it was curated
    /// in.</para>
    /// </remarks>
    /// <param name="capturedNamespace">Namespace recorded at the capture site, or null when it was not.</param>
    /// <param name="className">
    /// Simple class name, for the fallback re-derivation, or null when even that identity is
    /// missing — the index is never consulted for a null name, the same as no capture at all.
    /// </param>
    /// <param name="index">
    /// Maps a simple class name to its namespace (null when unknown or ambiguous); must not be null.
    /// </param>
    /// <returns>The namespace to resolve, never null (empty when nothing is known).</returns>
    public static string PackageToResolve(
        string? capturedNamespace, string? className, Func<string, string?> index)
    {
        if (capturedNamespace is not null)
        {
            return capturedNamespace;
        }

        return (className is not null ? index(className) : null) ?? "";
    }

    /// <summary>Delimiter-aware prefix test: equal, or a child namespace separated by a dot.</summary>
    private static bool Owns(string prefix, string namespaceName)
    {
        return namespaceName == prefix
            || namespaceName.StartsWith(prefix + ".", StringComparison.Ordinal);
    }
}
