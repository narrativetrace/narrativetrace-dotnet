// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Numerics;
using System.Reflection;

namespace NarrativeTrace.Core;

/// <summary>
/// Decides whether a runtime <see cref="Type"/> is defined by the platform
/// (the .NET base class library) rather than by application or third-party
/// code — the ORIGIN test <see cref="ValueRenderer"/> uses to decide whether
/// a collection or map may be enumerated through its own iterator/entries,
/// or must fall back to plain object introspection instead — and which exact
/// platform types are the stateless leaves whose own
/// <see cref="object.ToString"/> rendering may call.
/// </summary>
/// <remarks>
/// Java's edition of this rule asks the defining class loader (bootstrap
/// versus application). .NET has no equivalent bootstrap-loader concept, so
/// the honest analogue is the declaring assembly's identity: a platform
/// assembly is one named <c>System*</c>, <c>mscorlib</c> or
/// <c>netstandard</c> <em>and</em> strong-name signed (a non-empty public
/// key token) — the name check alone would let a user assembly spoof origin
/// simply by calling itself <c>System.MyApp</c>; requiring the signature
/// too means spoofing it would need the platform's own private signing key.
/// </remarks>
internal static class PlatformTypes
{
    /// <summary>Whether <paramref name="type"/> is declared by a trusted platform assembly.</summary>
    public static bool IsPlatformDefined(Type type)
    {
        var name = type.Assembly.GetName();
        return HasPlatformName(name.Name) && HasPlatformSignature(name);
    }

    private static bool HasPlatformName(string? assemblyName)
    {
        return assemblyName is not null
            && (assemblyName.StartsWith("System", StringComparison.Ordinal)
                || assemblyName is "mscorlib" or "netstandard");
    }

    private static bool HasPlatformSignature(AssemblyName name)
    {
        var token = name.GetPublicKeyToken();
        return token is { Length: > 0 };
    }

    /// <summary>
    /// The exact platform types whose own <see cref="object.ToString"/> is a pure read of the
    /// value's own state — the closed list <see cref="IsStatelessLeaf"/> answers from.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Derived from what the renderer already treats as a leaf: every scalar its own type switch
    /// short-circuits, plus the scalars that reached their <see cref="object.ToString"/> through
    /// the member-less fallback. Each entry stringifies a number, a moment, an identifier, a
    /// locator or a version that it holds itself.
    /// </para>
    /// <para>
    /// @edgeCase Nothing that stringifies a composite is here, however small. A
    /// <see cref="System.Text.StringBuilder"/> or an <see cref="Exception"/> prints text a caller
    /// handed it, and a document-shaped platform value (<c>JsonElement</c>, an
    /// <c>XElement</c>, a <c>JsonNode</c>) prints a whole subtree — the very content the redaction
    /// axes exist to see, which this path does not consult.
    /// </para>
    /// </remarks>
    private static readonly HashSet<Type> StatelessLeaves =
    [
        typeof(bool), typeof(char), typeof(string),
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
        typeof(int), typeof(uint), typeof(long), typeof(ulong),
        typeof(nint), typeof(nuint),
        typeof(float), typeof(double), typeof(decimal), typeof(BigInteger),
        typeof(DateTime), typeof(DateTimeOffset), typeof(TimeSpan),
        typeof(Guid), typeof(Uri), typeof(Version),

        // The scalars the older target framework has no type for at all. A leaf
        // absent from this list renders as its type name, so the gate costs
        // detail on that framework and never trust.
#if NET5_0_OR_GREATER
        typeof(Half),
#endif
#if NET6_0_OR_GREATER
        typeof(DateOnly), typeof(TimeOnly),
#endif
#if NET7_0_OR_GREATER
        typeof(Int128), typeof(UInt128),
#endif
    ];

    /// <summary>
    /// Whether <paramref name="type"/> is a stateless leaf — the one kind of value whose own
    /// <see cref="object.ToString"/> rendering is allowed to call (the narrative-summary hook is
    /// the other sanctioned one). The single decision both of
    /// <see cref="ValueRenderer"/>'s paths ask, so they cannot answer it differently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Membership is by the exact runtime type — a <see cref="Type"/> IS its assembly plus its
    /// full name — never <see cref="Type.IsAssignableFrom"/>, never the declaring assembly's name
    /// alone, and never the absence of readable members. A member-less value is not thereby
    /// stateless: <c>JsonElement</c> is an index into a document whose whole subtree its
    /// <see cref="object.ToString"/> returns, and state parked in a static side table keyed by the
    /// instance is invisible to reflection yet free to read from inside the type's own
    /// <see cref="object.ToString"/>. A subclass is never a leaf because its base is, and an
    /// application ancestor's <see cref="object.ToString"/> — reached only because the
    /// most-derived type happens to declare no state of its own — is always somebody's element
    /// walk (<c>NarratingCollectionBase</c> is the corpus fixture for exactly that door).
    /// </para>
    /// <para>
    /// An <see cref="Enum"/> qualifies wherever it was declared: the CLR forbids an enum its own
    /// <see cref="object.ToString"/>, so the text is the platform formatting the value's own
    /// underlying number against the type's member names, and no user code runs.
    /// </para>
    /// </remarks>
    public static bool IsStatelessLeaf(Type type) =>
        type.IsEnum || StatelessLeaves.Contains(type);

    /// <summary>
    /// The nearest <see cref="List{T}"/> ancestor in <paramref name="type"/>'s
    /// own base-type chain, or <see langword="null"/> when nothing in the
    /// hierarchy is a closed <see cref="List{T}"/> — the one concrete
    /// platform collection this renderer has an honest, non-overridable
    /// state read for (its own <c>_items</c>/<c>_size</c> fields), the same
    /// role the Java edition's <c>ArrayList</c> ancestor lookup plays.
    /// </summary>
    public static Type? ListAncestor(Type type)
    {
        for (var t = type.BaseType; t is not null; t = t.BaseType)
        {
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>))
            {
                return t;
            }
        }

        return null;
    }
}
