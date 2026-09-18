// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Core;

/// <summary>
/// Everything <see cref="ValueRenderer"/>'s reflective walk needs to know about
/// a type, resolved on first sight and reused for every later render of it.
/// </summary>
/// <remarks>
/// <para>
/// The walk is unconditional by contract — a type with public state is never
/// rendered through its own <see cref="object.ToString"/>, and only
/// <see cref="NarrativeSummaryAttribute"/> opts past it (see
/// <c>documentation/privacy-and-redaction.md</c>) — so its cost is paid by every
/// traced value, not only by a hostile one. Nothing the walk asks reflection is
/// instance-dependent, though: the record marker, the member table, the
/// <see cref="NarrativeSummaryAttribute"/> member and each member's
/// <see cref="NotTracedAttribute"/> status are all properties of the
/// <see cref="Type"/>. Resolving them once per type is what makes the guaranteed
/// walk cost about what the untrusted <see cref="object.ToString"/> cost, rather
/// than weakening the guarantee to buy the difference back. Mirrors the Java
/// edition's <c>ClassValue</c> caches for the same rule.
/// </para>
/// <para>
/// Keyed on <see cref="Type"/> in a
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/>,
/// so the entry count is bounded by the number of distinct types the process
/// actually traces — the same footprint, and the same strong reference to a
/// possibly-collectible type, that the <see cref="NarrativeSummaryAttribute"/>
/// lookup this replaces already carried. Immutable once built, so
/// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}.GetOrAdd(TKey,Func{TKey,TValue})"/>
/// racing two builders for the same type is harmless.
/// </para>
/// <para>
/// A type whose reflection metadata cannot be read at all (a
/// <see cref="TypeLoadException"/>, an ambiguous <c>&lt;Clone&gt;$</c>) throws
/// out of <see cref="Of"/> exactly as the uncached probes did, and the failure is
/// not cached: the throw reaches <see cref="ValueRenderer.Render"/> /
/// <see cref="ValueRenderer.RenderStructured"/>'s own guard and degrades to the
/// <c>&lt;TypeName&gt;</c> placeholder as before.
/// </para>
/// </remarks>
internal sealed class TypeShape
{
    private const BindingFlags PublicInstance =
        BindingFlags.Public | BindingFlags.Instance;

    private const BindingFlags PrivateInstance =
        BindingFlags.NonPublic | BindingFlags.Instance;

    private static readonly
        System.Collections.Concurrent.ConcurrentDictionary<Type, TypeShape>
        Cache = new();

    private TypeShape(Type type)
    {
        TypeName = type.Name;
        IsRecord = type.GetMethod("<Clone>$") is not null;
        SummaryMethod = FindSummaryMethod(type);
        Members = BuildMembers(type);
    }

    /// <summary>The simple type name the renderers prefix an object dump with.</summary>
    public string TypeName { get; }

    /// <summary>
    /// Whether the type is a record — it renders parenthesised, and does so even
    /// with no public members, which is why this is asked separately from
    /// <see cref="Members"/> being non-empty.
    /// </summary>
    public bool IsRecord { get; }

    /// <summary>
    /// The type's <see cref="NarrativeSummaryAttribute"/> member (a no-argument
    /// method, or a property's getter), or <see langword="null"/> when it has
    /// none.
    /// </summary>
    public MethodInfo? SummaryMethod { get; }

    /// <summary>
    /// The public instance properties then fields, in the order the renderers
    /// emit them.
    /// </summary>
    public MemberSlot[] Members { get; }

    /// <summary>The shape of <paramref name="type"/>, built once per type.</summary>
    public static TypeShape Of(Type type) =>
        Cache.GetOrAdd(type, static t => new TypeShape(t));

    // Properties first, then fields: the order every property-only type
    // rendered before fields were introspected at all, so adding fields cannot
    // reshuffle existing output. A property with no readable backing field —
    // an indexer, or a computed getter with a body that is neither a true
    // auto-property nor backed by the conventional `_camelCase` field — is
    // not state, so it is never a rendering candidate at all (rendering
    // reads state, never runs behaviour: the getter itself is never called).
    private static MemberSlot[] BuildMembers(Type type)
    {
        var slots = new List<MemberSlot>();
        foreach (var property in PublicPropertiesOf(type))
        {
            var backingField = BackingFieldOf(type, property);
            if (backingField is not null)
            {
                slots.Add(new MemberSlot(property, backingField));
            }
        }

        foreach (var field in PublicFieldsOf(type))
        {
            slots.Add(new MemberSlot(field));
        }

        return [.. slots];
    }

    // An indexer is a public instance property too (named "Item" by
    // convention), but it takes index arguments it has none of here — it is
    // not state, and reflecting it as a member at all is what used to make
    // MemberSlot.Read call PropertyInfo.GetValue with no arguments and throw.
    private static PropertyInfo[] PublicPropertiesOf(Type type)
    {
        return [.. type.GetProperties(PublicInstance)
            .Where(p => p.GetIndexParameters().Length == 0)];
    }

    // The field a property's value actually lives in, read directly instead
    // of through the getter: the compiler-generated auto-property backing
    // field first (`<Name>k__BackingField`, every record/record-struct
    // component and ordinary `{ get; }` auto-property), then the small,
    // fixed set of hand-written backing-field conventions this renderer
    // knows how to find without running any code — `_camelCase` (the
    // convention this codebase's own manually implemented properties use,
    // e.g. Lazy&lt;T&gt;'s `_value`), bare `camelCase` (BCL's KeyValuePair
    // `key`/`value`) and `m_PascalCase` (BCL's Tuple `m_Item1`/`m_Item2`).
    // A property backed by none of these has no state this renderer can
    // read without running the getter's own body, so it is not a member at
    // all — null tells the caller to skip it. Internal (not private):
    // NarrationResolver (NarrativeTrace.Proxy, granted InternalsVisibleTo)
    // reuses this exact search for a template's {obj.Property} placeholder,
    // rather than keep a second copy of the same convention.
    internal static FieldInfo? BackingFieldOf(Type type, PropertyInfo property)
    {
        var name = property.Name;
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        return type.GetField($"<{name}>k__BackingField", PrivateInstance)
            ?? type.GetField("_" + camel, PrivateInstance)
            ?? type.GetField(camel, PrivateInstance)
            ?? type.GetField("m_" + name, PrivateInstance);
    }

    // Java introspects getDeclaredFields() minus static and synthetic; the .NET
    // equivalent is the public instance field table — private fields here are
    // compiler-generated property backing stores, not state a user declared.
    private static FieldInfo[] PublicFieldsOf(Type type)
    {
        return [.. type.GetFields(PublicInstance)
            .Where(f => !f.IsSpecialName && !IsCompilerGenerated(f))];
    }

    // Excludes what Java's !isSynthetic() filter excludes — an enum's value__
    // storage is a public instance field, and rendering it would turn every
    // enum into an object dump.
    private static bool IsCompilerGenerated(MemberInfo member)
    {
        return Attributes.Has(
            member,
            typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute),
            inherit: false);
    }

    private static MethodInfo? FindSummaryMethod(Type type)
    {
        return SummaryMethodOf(type) ?? SummaryPropertyGetterOf(type);
    }

    private static MethodInfo? SummaryMethodOf(Type type)
    {
        foreach (var method in type.GetMethods(PublicInstance))
        {
            if (method.GetParameters().Length == 0
                && HasNarrativeSummary(method))
            {
                return method;
            }
        }

        return null;
    }

    // A [NarrativeSummary] property carries the attribute on the property
    // itself, never on its generated getter, so the method scan above cannot
    // see it — the property table has to be walked separately.
    private static MethodInfo? SummaryPropertyGetterOf(Type type)
    {
        return PublicPropertiesOf(type)
            .FirstOrDefault(HasNarrativeSummary)
            ?.GetGetMethod();
    }

    private static bool HasNarrativeSummary(ICustomAttributeProvider member)
    {
        return Attributes.Has(
            member, typeof(NarrativeSummaryAttribute), inherit: true);
    }
}

/// <summary>
/// One member of a rendered type, with everything about it that depends on the
/// member rather than on the value or the caller's options resolved once.
/// </summary>
/// <remarks>
/// <see cref="Annotated"/> is the <see cref="NotTracedAttribute"/> answer: the
/// attribute on the member, or on the matching primary-constructor parameter of
/// a positional record. Computing it cost a <c>GetCustomAttributes</c> array
/// plus, for a property, a <c>GetConstructors</c>/<c>GetParameters</c> scan on
/// <em>every</em> render of <em>every</em> instance, and it cannot change for the
/// life of the type. Whether the member's <em>name</em> is on a redaction
/// deny-list is deliberately not cached here — that belongs to the caller's
/// <see cref="RedactionPolicy"/>, not to the type.
/// </remarks>
internal sealed class MemberSlot
{
    private readonly FieldInfo _backing;

    /// <summary>A property-derived slot: rendered under the property's name, read from its backing field.</summary>
    public MemberSlot(PropertyInfo property, FieldInfo backingField)
    {
        _backing = backingField;
        Name = property.Name;
        Annotated = HasNotTraced(property) || IsNotTracedComponent(property);
    }

    /// <summary>A plain declared-field slot: rendered and read under the field's own name.</summary>
    public MemberSlot(FieldInfo field)
    {
        _backing = field;
        Name = field.Name;
        Annotated = HasNotTraced(field);
    }

    /// <summary>The declared member name, as rendered and as the deny-list sees it.</summary>
    public string Name { get; }

    /// <summary>Whether the member carries <see cref="NotTracedAttribute"/>.</summary>
    public bool Annotated { get; }

    /// <summary>
    /// Reads the member's backing field off <paramref name="target"/> — never a
    /// property's getter, which for a property-derived slot is not invoked at
    /// all. Throws whatever a hostile field read throws; the renderers'
    /// per-member guard degrades that to the typed error marker.
    /// </summary>
    public object? Read(object target) => _backing.GetValue(target);

    private static bool HasNotTraced(ICustomAttributeProvider member)
    {
        return Attributes.Has(
            member, typeof(NotTracedAttribute), inherit: true);
    }

    // A positional record parameter puts plain [NotTraced] on the primary
    // constructor parameter, not the generated property (Java's RECORD_COMPONENT
    // target maps to this).
    private static bool IsNotTracedComponent(PropertyInfo prop)
    {
        var ctors = prop.DeclaringType?.GetConstructors() ?? [];
        foreach (var ctor in ctors)
        {
            foreach (var parameter in ctor.GetParameters())
            {
                if (parameter.Name == prop.Name && HasNotTraced(parameter))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

internal static class Attributes
{
    public static bool Has(
        ICustomAttributeProvider member, Type attributeType, bool inherit)
    {
        return member.GetCustomAttributes(attributeType, inherit).Length > 0;
    }
}
