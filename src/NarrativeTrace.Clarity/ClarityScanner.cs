// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Core;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Analyzes compiled types without running them: builds synthetic trace nodes
/// from public methods via reflection, then delegates to
/// <see cref="ClarityAnalyzer"/> for scoring. Use it for CI gates and tooling
/// that needs naming feedback from an assembly alone.
/// </summary>
/// <remarks>
/// Property accessors, operators, <see cref="object"/> methods, and anything
/// the compiler synthesized are skipped; only types that contribute at least
/// one public, hand-written method appear in the results. A record whose
/// members are all generated therefore yields no result, exactly as a class
/// with only auto-properties does.
/// </remarks>
public static class ClarityScanner
{
    private const string CompilerGeneratedAttributeName =
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    /// <summary>Scans the given types, returning one result per type keyed by its name.</summary>
    /// <param name="types">Types to score.</param>
    /// <param name="vocabulary">
    /// The project's committed glossary vocabulary, so a scan and a test run of
    /// the same repository speak the same language; null uses the built-in
    /// dictionaries alone.
    /// </param>
    public static IReadOnlyDictionary<string, ClarityResult> Scan(
        IEnumerable<Type> types, DomainVocabulary? vocabulary = null)
    {
        var results = new Dictionary<string, ClarityResult>();
        foreach (var type in types)
        {
            var propertyNames = PropertyNames(type);
            var nodes = BuildNodes(type);
            nodes.AddRange(BuildPropertyNodes(type, propertyNames));
            if (nodes.Count > 0)
            {
                results[type.Name] = ClarityAnalyzer.Analyze(
                    new TraceTree(nodes), propertyNames, vocabulary);
            }
        }

        return results;
    }

    /// <summary>
    /// Declared public instance property names, which carry the author's
    /// domain vocabulary and are scored as nouns.
    /// </summary>
    /// <remarks>
    /// Indexers are excluded: their name is always the compiler's
    /// <c>Item</c>, which the author never chose. Compiler-generated
    /// properties (a record's <c>EqualityContract</c>) are excluded for the
    /// same reason.
    /// </remarks>
    private static HashSet<string> PropertyNames(Type type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in type.GetProperties(
            BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.DeclaredOnly))
        {
            if (property.GetIndexParameters().Length == 0
                && !IsCompilerGenerated(property))
            {
                names.Add(property.Name);
            }
        }

        return names;
    }

    private static IEnumerable<TraceNode> BuildPropertyNodes(
        Type type, HashSet<string> propertyNames)
    {
        foreach (var name in propertyNames)
        {
            yield return new TraceNode(
                new MethodSignature(type.Name, name, []),
                new Returned(""), [], 0);
        }
    }

    private static List<TraceNode> BuildNodes(Type type)
    {
        var nodes = new List<TraceNode>();
        foreach (var method in type.GetMethods(
            BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (ShouldSkip(method))
            {
                continue;
            }

            var signature = new MethodSignature(
                type.Name, method.Name, BuildParameters(method));
            nodes.Add(new TraceNode(signature, new Returned(""), [], 0));
        }

        return nodes;
    }

    private static bool ShouldSkip(MethodInfo method)
    {
        return method.IsSpecialName
            || IsObjectMethod(method)
            || IsCompilerGenerated(method);
    }

    // Nobody chose these names, so scoring them tells the author nothing they
    // can act on. Records are the case that matters: the compiler synthesizes
    // Deconstruct, Equals(T), and <Clone>$, all of which survive the checks
    // above, so every record was being graded on members it never declared.
    // Read from attribute metadata rather than a runtime type check so this
    // works under the reflection-only MetadataLoadContext the CLI uses. Async
    // and iterator methods are unaffected — they carry AsyncStateMachine /
    // IteratorStateMachine, not CompilerGenerated.
    private static bool IsCompilerGenerated(MemberInfo member)
    {
        foreach (var attribute in member.GetCustomAttributesData())
        {
            if (attribute.AttributeType.FullName == CompilerGeneratedAttributeName)
            {
                return true;
            }
        }

        return false;
    }

    // Matches the exact signatures declared on System.Object so overrides of
    // ToString/Equals/GetHashCode are ignored, without skipping look-alikes such
    // as IEquatable<T>.Equals(T). Uses only name + parameter metadata, so it
    // works under a reflection-only MetadataLoadContext (GetBaseDefinition does
    // not).
    private static bool IsObjectMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        return method.Name switch
        {
            "ToString" or "GetHashCode" or "GetType" => parameters.Length == 0,
            "Equals" => parameters.Length == 1
                && parameters[0].ParameterType.FullName == "System.Object",
            _ => false,
        };
    }

    private static ParameterCapture[] BuildParameters(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var captures = new ParameterCapture[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            captures[i] = new ParameterCapture(parameters[i].Name ?? "", "", false);
        }

        return captures;
    }
}
