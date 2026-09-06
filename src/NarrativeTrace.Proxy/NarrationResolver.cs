// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Proxy;

internal static class NarrationResolver
{
    private static readonly Regex PlaceholderPattern =
        new(@"\{(\w+)(?:\.(\w+))?\}", RegexOptions.Compiled);

    public static string? Resolve(
        string? template,
        object?[] args,
        ParameterInfo[] parameters)
    {
        if (template is null)
        {
            return null;
        }

        return PlaceholderPattern.Replace(
            template, m => ReplacePlaceholder(
                m, args, parameters));
    }

    private static string ReplacePlaceholder(
        Match match,
        object?[] args,
        ParameterInfo[] parameters)
    {
        var paramName = match.Groups[1].Value;
        var propName = match.Groups[2].Success
            ? match.Groups[2].Value
            : null;

        var index = FindParameterIndex(
            parameters, paramName);
        if (index < 0 || index >= args.Length)
        {
            return match.Value;
        }

        var value = args[index];
        return propName is null
            ? ResolveSimplePlaceholder(value, parameters[index], match.Value)
            : ResolvePathPlaceholder(value, propName, parameters[index], match.Value);
    }

    // A placeholder naming a value directly -- {password}, {token}, {card}
    // -- with no path segment (mirrored from the java flagship): the key
    // IS the parameter's name, so the name axis is asked here, the same
    // way ResolvePathPlaceholder asks it for a path segment. Asked only
    // once a value exists: a placeholder naming nothing stays literal
    // (still catches an authoring typo) rather than swallowing the
    // unresolved-placeholder warning.
    private static string ResolveSimplePlaceholder(
        object? value, ParameterInfo parameter, string literal)
    {
        if (value is null)
        {
            return literal;
        }

        return IsRedacted(parameter) ? RedactionPolicy.Marker : PlainValue(value);
    }

    private static string ResolvePathPlaceholder(
        object? value, string propName, ParameterInfo parameter, string literal)
    {
        return IsRedacted(parameter) || PathIsRedacted(value, propName)
            ? RedactionPolicy.Marker
            : ResolveValue(value, propName, literal);
    }

    // Naming a path never weakens the rules that apply to the value directly:
    // a property a template names is checked through the same redaction
    // decision ValueRenderer applies during reflective introspection
    // (RedactionPolicy.IsRedacted), not a second copy of it. A segment naming
    // no property is an authoring typo, not a value — it stays literal so the
    // unresolved-placeholder warning can still catch it. To narrate the
    // value, remove [NotTraced] from the property; that removal is the
    // deliberate, reviewable decision.
    private static bool PathIsRedacted(object? root, string? propertyName)
    {
        if (root is null || propertyName is null)
        {
            return false;
        }

        var prop = root.GetType().GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is null)
        {
            return false;
        }

        var annotated = prop.GetCustomAttribute<NotTracedAttribute>() is not null
            || IsNotTracedRecordComponent(prop);
        return RedactionPolicy.Default.IsRedacted(prop.Name, annotated);
    }

    // Mirrors ValueRenderer's record-component fallback: a positional record
    // parameter puts plain [NotTraced] on the primary constructor parameter,
    // not the generated property.
    private static bool IsNotTracedRecordComponent(PropertyInfo prop)
    {
        var ctors = prop.DeclaringType?.GetConstructors() ?? [];
        foreach (var ctor in ctors)
        {
            foreach (var parameter in ctor.GetParameters())
            {
                if (parameter.Name == prop.Name
                    && parameter.GetCustomAttribute<NotTracedAttribute>() is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // propName is always non-null here: a null-propName placeholder (a bare
    // {key}) is handled entirely in ReplacePlaceholder, including its own
    // null-value literal-preservation — see the remark there.
    private static string ResolveValue(
        object? value, string propName, string literal)
    {
        return GetPropertyValue(value, propName, literal);
    }

    // Templates interpolate a SCALAR plainly (JVM String.valueOf parity): no
    // quoting, invariant formatting. A non-scalar goes through ValueRenderer,
    // unconditionally — a real defect, mirrored from the Java flagship and
    // fixed there too. An earlier, narrower fix rendered the value
    // through ValueRenderer first and used that form ONLY when it carried the
    // redaction marker, on the theory that "no marker" means "nothing is
    // hidden". That theory is unsound: ValueRenderer's own bounds — the
    // MaxObjectKeys field cap, the MaxDepth cap (also the cycle guard), or
    // its last-resort <TypeName> degrade — can all produce a marker-free
    // result BECAUSE the walk never reached the redacted member, not because
    // nothing was hidden. "No marker" meant "nothing is hidden" and "the
    // renderer did not look" alike, and falling back to ToString() in the
    // second case printed the secret in full. Known, accepted consequence:
    // an object with nothing hidden no longer keeps its own hand-written
    // ToString() byte for byte — it renders structurally instead, the same
    // as it already did as a captured argument/return value. See
    // RedactedObjectPlaceholderTests for the pinned regression (field-cap and
    // depth-cap shapes) and TemplateRedactionPropertyTests for the
    // corpus-driven cases.
    //
    // Text is not a fast path any more (mirrored from the java flagship):
    // a string is the one scalar whose CONTENT can be a
    // credential arriving under an innocent name (issued {token}), so it
    // goes to ValueRenderer.RenderNarrationText — the value-shape redaction,
    // control escape and length cap a captured String argument already got,
    // minus the quotation marks a narration must not carry. Numbers, bool,
    // char and enum keep the fast path; a non-string scalar's VALUE cannot
    // be a credential shape, only its (already name-checked) field could be.
    private static string PlainValue(object value)
    {
        if (value is string text)
        {
            return ValueRenderer.RenderNarrationText(text);
        }

        return IsScalar(value) ? ScalarText(value) : ValueRenderer.Render(value);
    }

    // No guard here, unlike the non-scalar path. Every type IsScalar matches
    // is a sealed BCL primitive — bool, char, an enum, or a numeric
    // type — none of which can throw or come back empty from rendering
    // themselves, confirmed empirically rather than assumed. "Narration must
    // never break the call it narrates" holds here because this method's
    // whole input domain is provably safe, not because a failure is caught.
    // A rogue rendering is a NON-scalar hazard, and a non-scalar never
    // reaches this method — see PlainValue's remarks above, and
    // ValueRenderer.SafeToString, which guards exactly that hazard.
    //
    // Enum is the one scalar checked BEFORE IFormattable rather than left to
    // fall into it (Enum implements IFormattable): a C#-compiled member name
    // is a language identifier and can't carry a control character, but the
    // CLR itself does not enforce that — an IL-authored assembly can define
    // one that does, exactly the .NET shape of the Java flagship's "Number
    // subclass with a hostile toString()" finding. Sanitized, not truncated,
    // matching ValueRenderer's own no-length-cap treatment of the same case
    // (RenderStructuredValue). bool stays raw: JVM String.valueOf parity,
    // see PlainValue's remarks — TemplateParser's own needsSanitizing draws
    // the identical line at Number/Enum only.
    private static string ScalarText(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            Enum e => ControlEscape.Sanitize(e.ToString()),
            IFormattable f => f.ToString(
                null, CultureInfo.InvariantCulture),
            _ => value.ToString()!,
        };
    }

    // Values that are their own best narration and cannot hide a member:
    // numbers, booleans, characters and enum constants. Skipping the
    // renderer for these keeps the common placeholder — {orderId},
    // {quantity} — as cheap as it was. String is deliberately not here any
    // more (see PlainValue's remarks) — it is the one scalar whose content
    // can itself be a credential.
    private static bool IsScalar(object value)
    {
        return value is bool or char or Enum
            or byte or sbyte or short or ushort
            or int or uint or long or ulong
            or float or double or decimal;
    }

    // Asks the name axis on the key itself: the placeholder key IS the
    // parameter's name, so this is the same input PathIsRedacted feeds
    // RedactionPolicy for a path segment. Mirrors the java flagship's
    // SimplePlaceholder.resolve asking RedactionPolicy.isRedacted — before
    // it, this only checked the [NotTraced] annotation, so a bare
    // {password} placeholder never asked the deny-list at all.
    private static bool IsRedacted(ParameterInfo parameter)
    {
        var annotated = parameter.GetCustomAttribute<NotTracedAttribute>() is not null;
        return RedactionPolicy.Default.IsRedacted(parameter.Name, annotated);
    }

    private static int FindParameterIndex(
        ParameterInfo[] parameters, string name)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (string.Equals(
                parameters[i].Name, name,
                StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    // Unresolvable property placeholders survive as their full literal
    // ({obj.prop}) so template typos stay visible in output and warning
    // scans — matching the JVM TemplateParser's preservation semantics.
    private static string GetPropertyValue(
        object? obj, string propertyName, string literal)
    {
        var prop = obj?.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance);
        return prop is null
            ? literal
            : ReadProperty(prop, obj!, literal);
    }

    // Only the ACCESSOR is guarded here: a getter that throws leaves the
    // placeholder literal visible, matching the JVM TemplateParser. Rendering
    // deliberately sits outside the try, because PlainValue never lets a
    // rogue rendering escape either: a non-scalar degrades through
    // ValueRenderer's own guard to the <TypeName> marker, and a scalar is
    // provably safe to begin with (see PlainValue's remarks) — the two
    // accessor/render failures are different and must stay distinguishable
    // in output. Folding the render back inside this try would silently
    // downgrade every rogue ToString() to the literal and break parity with
    // the JVM and TypeScript editions.
    private static string ReadProperty(
        PropertyInfo prop, object obj, string literal)
    {
        object? value;
        try
        {
            value = prop.GetValue(obj);
        }
        catch (Exception)
        {
            return literal;
        }

        return value is null ? "null" : PlainValue(value);
    }
}
