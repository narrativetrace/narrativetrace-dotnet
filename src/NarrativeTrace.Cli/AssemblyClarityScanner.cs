// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Clarity;
using NarrativeTrace.Glossary;

namespace NarrativeTrace.Cli;

/// <summary>
/// Loads an assembly in a metadata-only reflection context — so scanned code is
/// never executed — and runs the <see cref="ClarityScanner"/> over its public
/// types.
/// </summary>
public static class AssemblyClarityScanner
{
    /// <summary>Scans the public types of the assembly at the given path.</summary>
    /// <param name="assemblyPath">Path to the assembly to scan.</param>
    /// <param name="glossaryFilePath">
    /// Path to the repository's committed <c>glossary.json</c>, so the scan
    /// scores in the project's own vocabulary; null scores with the built-in
    /// dictionaries alone. Resolve it with
    /// <see cref="GlossarySettings.ResolveFile"/>.
    /// </param>
    public static IReadOnlyDictionary<string, ClarityResult> Scan(
        string assemblyPath, string? glossaryFilePath = null)
    {
        var fullPath = Path.GetFullPath(assemblyPath);
        var resolver = new PathAssemblyResolver(ResolverPaths(fullPath));
        using var context = new MetadataLoadContext(resolver);
        var assembly = context.LoadFromAssemblyPath(fullPath);
        return ClarityScanner.Scan(
            PublicTypes(assembly), GlossaryVocabulary.FromFile(glossaryFilePath));
    }

    private static HashSet<string> ResolverPaths(string assemblyFullPath)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        AddAssembliesIn(paths, Path.GetDirectoryName(typeof(object).Assembly.Location));
        AddAssembliesIn(paths, Path.GetDirectoryName(assemblyFullPath));
        paths.Add(assemblyFullPath);
        return paths;
    }

    internal static void AddAssembliesIn(HashSet<string> paths, string? directory)
    {
        if (directory is null || !Directory.Exists(directory))
        {
            return;
        }

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
        {
            paths.Add(dll);
        }
    }

    private static IEnumerable<Type> PublicTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes().Where(IsScannable);
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(type => type is not null && IsScannable(type))!;
        }
    }

    // The narrative surface is what a caller outside the assembly can name, so
    // private nested types are excluded here — they are implementation details
    // of their enclosing type. The C# analogues of Java's anonymous, local, and
    // synthetic classes (lambda closures, async/iterator state machines) fall
    // out twice over: they are nested-private, and they declare no public
    // methods for the scanner to build a node from.
    private static bool IsScannable(Type type)
    {
        return type.IsPublic || type.IsNestedPublic;
    }
}
