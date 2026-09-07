// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Maps simple type names to their namespaces by indexing compiled
/// assemblies.
/// </summary>
/// <remarks>
/// <para>
/// Trace nodes carry only simple class names, but context resolution needs
/// real namespace metadata (ADR-012). This index supplies the
/// <c>namespaceOf</c> function <see cref="GlossaryHarvester"/> and
/// <see cref="TraceTranslationView"/> take, built from type metadata — no
/// type is constructed and no initializer runs.
/// </para>
/// <para>
/// <b>Platform note.</b> The Java runtime scans compiled-class directories and
/// skips JAR entries, because traced application code lives in class
/// directories under a build tool while JARs hold third-party classes whose
/// vocabulary is not the repository's to govern. .NET has no such split — an
/// assembly is an assembly — so <see cref="FromLoadedAssemblies"/> draws the
/// same line where the platform draws one: assemblies loaded from the
/// application's own output directory, never the shared framework. Package
/// assemblies copied into that output are indexed, and any simple name they
/// collide with degrades to unknown under the ambiguity rule below — the same
/// answer Java's rule gives, reached by the same intent.
/// </para>
/// <para>
/// A simple name observed in more than one namespace is ambiguous and
/// resolves to <see langword="null"/> (unknown, hence <c>_unassigned</c>)
/// rather than guessing. Compiler-generated types (closure classes, iterator
/// and async state machines, <c>&lt;Module&gt;</c>) are never indexed; nested
/// types index under their inner simple name, matching the
/// <see cref="System.Type.Name"/> that trace nodes carry.
/// </para>
/// </remarks>
public sealed class ClassPackageIndex
{
    /// <summary>Sentinel for simple names seen in more than one namespace — resolved as unknown.</summary>
    private const string Ambiguous = " ambiguous";

    private const string CompilerGeneratedAttribute =
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    private readonly Dictionary<string, string> namespaceBySimpleName;

    private ClassPackageIndex(Dictionary<string, string> namespaceBySimpleName)
    {
        this.namespaceBySimpleName = namespaceBySimpleName;
    }

    /// <summary>
    /// Builds an index over the assemblies the running application loaded
    /// from its own output directory.
    /// </summary>
    /// <remarks>
    /// The discovery factory the suite hooks use: the equivalent of reading
    /// the classpath, minus everything the platform ships. Dynamic assemblies
    /// and assemblies with no on-disk location (single-file bundles, in-memory
    /// loads) carry no directory to compare and are skipped.
    /// </remarks>
    /// <returns>Index over every indexable type of the application's own assemblies.</returns>
    public static ClassPackageIndex FromLoadedAssemblies()
    {
        var baseDirectory = Normalized(AppContext.BaseDirectory);
        return FromAssemblies(AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => IsApplicationAssembly(assembly, baseDirectory))
            .ToArray());
    }

    /// <summary>Builds an index over the given assemblies.</summary>
    /// <param name="assemblies">Assemblies to index; must not be null.</param>
    /// <returns>Index over every indexable type found.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> is null.</exception>
    public static ClassPackageIndex FromAssemblies(IReadOnlyList<Assembly> assemblies)
    {
        if (assemblies is null)
        {
            throw new ArgumentNullException(nameof(assemblies));
        }

        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var assembly in assemblies)
        {
            foreach (var type in TypesOf(assembly))
            {
                Index(type, index);
            }
        }

        return new ClassPackageIndex(index);
    }

    /// <summary>
    /// Returns the namespace of the given simple type name, or null when
    /// unknown or ambiguous.
    /// </summary>
    /// <param name="simpleTypeName">
    /// Simple type name as carried by trace nodes; must not be null.
    /// </param>
    /// <returns>
    /// The namespace (<c>""</c> for the global namespace), or null when the
    /// name is not in the index or was seen in more than one namespace.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="simpleTypeName"/> is null.</exception>
    public string? NamespaceOf(string simpleTypeName)
    {
        if (simpleTypeName is null)
        {
            throw new ArgumentNullException(nameof(simpleTypeName));
        }

        return namespaceBySimpleName.TryGetValue(simpleTypeName, out var found)
            && !string.Equals(found, Ambiguous, StringComparison.Ordinal)
            ? found
            : null;
    }

    /// <summary>Returns whether the index is structurally consistent: no blank names or null namespaces.</summary>
    /// <remarks>
    /// Construction makes this always true for live instances; the method
    /// re-checks the same rule for test-time invariant verification.
    /// </remarks>
    internal bool Invariant()
    {
        return namespaceBySimpleName.All(
            entry => !string.IsNullOrEmpty(entry.Key) && entry.Value is not null);
    }

    private static void Index(Type type, Dictionary<string, string> index)
    {
        if (!IsIndexable(type))
        {
            return;
        }

        var namespaceName = type.Namespace ?? string.Empty;
        if (index.TryGetValue(type.Name, out var previous))
        {
            if (!string.Equals(previous, namespaceName, StringComparison.Ordinal))
            {
                index[type.Name] = Ambiguous;
            }

            return;
        }

        index[type.Name] = namespaceName;
    }

    /// <summary>
    /// Compiler-generated types never appear in traces — the C# analogues of
    /// Java's anonymous and local classes, plus <c>&lt;Module&gt;</c>, whose
    /// names the language reserves by making them unspeakable.
    /// </summary>
    private static bool IsIndexable(Type type)
    {
        var name = type.Name;
        return name.Length > 0
            && name.IndexOf('<') < 0
            && name.IndexOf('>') < 0
            && !IsCompilerGenerated(type);
    }

    /// <summary>
    /// Matched by full attribute name rather than by
    /// <c>IsDefined&lt;T&gt;()</c>, so an index built over types read in a
    /// metadata-only reflection context answers the same way — the idiom
    /// <see cref="GlossaryStaticScanner"/> already uses for the same reason.
    /// </summary>
    private static bool IsCompilerGenerated(Type type)
    {
        return type.GetCustomAttributesData().Any(
            data => string.Equals(
                data.AttributeType.FullName,
                CompilerGeneratedAttribute,
                StringComparison.Ordinal));
    }

    /// <summary>
    /// The assembly's types, tolerating a partly unloadable assembly the way
    /// the CLI scanner does — an assembly that cannot fully load still
    /// contributes the types that did.
    /// </summary>
    private static IEnumerable<Type> TypesOf(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            return e.Types.Where(type => type is not null)!;
        }
    }

    private static bool IsApplicationAssembly(Assembly assembly, string baseDirectory)
    {
        if (assembly.IsDynamic)
        {
            return false;
        }

        var location = assembly.Location;
        if (string.IsNullOrEmpty(location))
        {
            return false;
        }

        var directory = Path.GetDirectoryName(location);
        return directory is not null
            && string.Equals(
                Normalized(directory), baseDirectory, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trailing-separator-free full path. <c>AppContext.BaseDirectory</c>
    /// carries a trailing separator and <c>Path.GetDirectoryName</c>
    /// does not, so the two are only comparable once both are trimmed;
    /// <c>Path.TrimEndingDirectorySeparator</c> is unavailable on
    /// netstandard2.0.
    /// </summary>
    private static string Normalized(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
