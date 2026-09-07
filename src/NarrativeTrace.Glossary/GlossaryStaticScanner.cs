// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Core;

namespace NarrativeTrace.Glossary;

/// <summary>Builds synthetic trace trees from compiled types, without running them.</summary>
/// <remarks>
/// The static half of ADR-012 harvesting. Its trees feed
/// <see cref="GlossaryHarvester.HarvestStatic"/>, which is the only path
/// allowed to harvest <c>[Narrated]</c> / <c>[OnError]</c> text: here the
/// attribute value is the <strong>raw</strong> template, placeholders intact,
/// whereas a captured trace carries it with values interpolated.
/// <para>
/// Attributes are matched by full type name rather than by
/// <c>GetCustomAttribute&lt;T&gt;()</c> because name matching is the only form
/// that also works for types loaded into a metadata-only reflection context,
/// which is how a CLI scan reads a user assembly without executing it —
/// resolving the attribute type would mean loading the assembly that declares
/// it, which such a scan deliberately never does.
/// </para>
/// <para>
/// Compiler-generated, special-name (property accessors, operators),
/// non-public and <see cref="object"/> methods are skipped, and a type
/// contributing neither a method nor a property contributes no tree.
/// Parameter names are taken as declared; a method with several
/// <c>[OnError]</c> templates yields one node per template so that every
/// template is harvested exactly once.
/// </para>
/// <para>
/// Declared public properties contribute a node of their own (see
/// <see cref="PropertyNodes"/>): in C# the property is the vocabulary carrier
/// that a Java record's accessor <em>method</em> is, and the Java runtime
/// harvests those.
/// </para>
/// </remarks>
public static class GlossaryStaticScanner
{
    private const string NarratedAttribute =
        "NarrativeTrace.Core.Annotation.NarratedAttribute";
    private const string OnErrorAttribute =
        "NarrativeTrace.Core.Annotation.OnErrorAttribute";
    private const string CompilerGeneratedAttribute =
        "System.Runtime.CompilerServices.CompilerGeneratedAttribute";

    private const BindingFlags DeclaredPublic =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
        | BindingFlags.DeclaredOnly;

    /// <summary>Scans already-loaded types into one tree per contributing type.</summary>
    /// <param name="types">Types to scan; must not be null.</param>
    /// <returns>One tree per type that contributed at least one method.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="types"/> is null.</exception>
    public static IReadOnlyList<TraceTree> Scan(IReadOnlyList<Type> types)
    {
        if (types is null)
        {
            throw new ArgumentNullException(nameof(types));
        }

        var trees = new List<TraceTree>();
        foreach (var type in types)
        {
            var nodes = BuildNodes(type);
            if (nodes.Count > 0)
            {
                trees.Add(new TraceTree(nodes));
            }
        }

        return trees;
    }

    private static List<TraceNode> BuildNodes(Type type)
    {
        return type.GetMethods(DeclaredPublic)
            .Where(method => !ShouldSkip(method))
            .SelectMany(method => NodesFor(type, method))
            .Concat(PropertyNodes(type))
            .ToList();
    }

    /// <summary>One parameterless node per declared property, named for the property itself.</summary>
    /// <remarks>
    /// <para>
    /// <c>OrderResult.OrderId</c> is the same vocabulary Java harvests from
    /// <c>OrderResult.orderId()</c>, so it is normalized as a method name and
    /// reaches the glossary as the same phrase (<c>"order id"</c>,
    /// <c>"total charged"</c>) — a C# scanner whose data-shape nouns were absent
    /// would score identifiers, and translate traces, against a smaller
    /// dictionary than its Java counterpart.
    /// </para>
    /// <para>
    /// Reading properties rather than un-skipping their <c>get_</c>/<c>set_</c>
    /// accessors leaves <see cref="ShouldSkip"/>'s special-name rule intact for
    /// the mechanism it was written for (operators, event accessors), and
    /// observes a read/write property once instead of twice. Indexers are
    /// excluded: their member name is the language's (<c>Item</c>), not the
    /// author's. A property carries no template — <c>[Narrated]</c> and
    /// <c>[OnError]</c> are <c>AttributeTargets.Method</c> — and no parameters.
    /// </para>
    /// </remarks>
    private static IEnumerable<TraceNode> PropertyNodes(Type type)
    {
        return type.GetProperties(DeclaredPublic)
            .Where(property => property.GetIndexParameters().Length == 0)
            .Select(property => Node(type, property.Name, [], null, null));
    }

    /// <summary>
    /// One node per error template, so a method carrying several
    /// <c>[OnError]</c> templates contributes all of them; the narration rides
    /// on the first node only, keeping each template a single observation.
    /// </summary>
    private static IEnumerable<TraceNode> NodesFor(Type type, MethodInfo method)
    {
        var parameters = method.GetParameters()
            .Select(parameter => new ParameterCapture(parameter.Name ?? "", "", false))
            .ToList();
        var narration = Templates(method, NarratedAttribute).FirstOrDefault();
        var errors = Templates(method, OnErrorAttribute);
        if (errors.Count == 0)
        {
            return [Node(type, method.Name, parameters, narration, null)];
        }

        return errors.Select((error, index) =>
            Node(type, method.Name, parameters, index == 0 ? narration : null, error));
    }

    private static TraceNode Node(
        Type type,
        string memberName,
        IReadOnlyList<ParameterCapture> parameters,
        string? narration,
        string? errorContext)
    {
        return new TraceNode(
            new MethodSignature(
                type.Name, memberName, parameters, narration, errorContext),
            new Incomplete(),
            [],
            0);
    }

    /// <summary>Reads the first constructor argument of each matching attribute.</summary>
    private static List<string> Templates(MethodInfo method, string attributeFullName)
    {
        return method.GetCustomAttributesData()
            .Where(data => data.AttributeType.FullName == attributeFullName)
            .Select(data => data.ConstructorArguments.Count > 0
                ? data.ConstructorArguments[0].Value as string
                : null)
            .Where(template => !string.IsNullOrEmpty(template))
            .Select(template => template!)
            .ToList();
    }

    private static bool ShouldSkip(MethodInfo method)
    {
        return method.IsSpecialName
            || HasAttribute(method, CompilerGeneratedAttribute)
            || IsObjectMethod(method);
    }

    private static bool HasAttribute(MethodInfo method, string attributeFullName)
    {
        return method.GetCustomAttributesData()
            .Any(data => data.AttributeType.FullName == attributeFullName);
    }

    /// <summary>
    /// Matches on name <em>and</em> parameters, so an overridden
    /// <c>ToString()</c> is skipped while a typed <c>Equals(T)</c> overload —
    /// real vocabulary — is kept.
    /// </summary>
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
}
