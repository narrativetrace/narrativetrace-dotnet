// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Core;

/// <summary>
/// Renders an arbitrary captured value to a bounded, redaction-aware string.
/// </summary>
/// <remarks>
/// The safety boundary between application data and trace output: every
/// traversal is bounded by <see cref="RenderOptions"/>, so a huge collection,
/// a deep graph, or a reference cycle produces bounded output instead of
/// hanging or exhausting memory. Cycles are detected by reference identity, not
/// only by depth. Stateless and thread-safe.
/// </remarks>
public static class ValueRenderer
{
    /// <summary>Renders a value to its flat string form.</summary>
    /// <param name="value">
    /// The value to render; may be <see langword="null"/>, which renders as the
    /// literal <c>"null"</c> — indistinguishable from a string whose content is
    /// <c>"null"</c>.
    /// </param>
    /// <param name="options">
    /// Truncation and redaction limits, or <see langword="null"/> for the
    /// defaults. Note the default <see cref="RenderOptions"/> applies the secure
    /// <see cref="RedactionPolicy.Default"/>, so passing <see langword="null"/>
    /// redacts rather than disabling redaction.
    /// </param>
    /// <returns>
    /// The rendered value, bounded by <paramref name="options"/>. Never
    /// <see langword="null"/>. Lossy by design — truncated output is not
    /// round-trippable back to the original value.
    /// </returns>
    /// <remarks>
    /// Total: nothing a captured value does (a throwing <c>ToString()</c>,
    /// a throwing enumerator/dictionary, a throwing member accessor) can
    /// escape this method. Per-element failures degrade to an
    /// <c>"&lt;error&gt;"</c>-shaped placeholder for that element only; a
    /// value that fails before any partial output exists (for example a
    /// <c>ToString()</c> override that throws) degrades to
    /// <c>"&lt;TypeName&gt;"</c>. Render defensively at capture sites
    /// regardless — this boundary is a safety net, not a license to skip
    /// guarding user-controlled values elsewhere.
    /// </remarks>
    public static string Render(
        object? value, RenderOptions? options = null)
    {
        if (value is null)
        {
            return "null";
        }

        var opts = options ?? new RenderOptions();
        var seen = new HashSet<object>(new ReferenceComparer());
        try
        {
            return RenderValue(value, opts, seen, 0);
        }
        catch
        {
            return $"<{value.GetType().Name}>";
        }
    }

    /// <summary>
    /// Renders text that is substituted into narration rather than shown as
    /// a value: the same decision <see cref="Render"/> makes for a
    /// <see langword="string"/>, without the quotation marks.
    /// </summary>
    /// <param name="text">Any text destined for narration; never <see langword="null"/>.</param>
    /// <param name="options">Truncation and redaction limits, or <see langword="null"/> for the defaults.</param>
    /// <remarks>
    /// A narration placeholder writes its value into a sentence, where a
    /// quoted, escaped value would read as a rendering artifact — but the
    /// sentence is an output like any other, so the value in it must obey
    /// the same rules. Both axes apply here: value-shape redaction (a JWT
    /// is a JWT wherever it is printed), the control-character escape, and
    /// the string cap. Only the quotes are dropped.
    /// <para>
    /// The one caller is <c>NarrationResolver</c>, which used to answer a
    /// bare scalar placeholder with the value's own <see cref="object.ToString()"/>
    /// — no redaction, no escaping, no cap — so <c>[Narrated("issued {token}")]</c>
    /// printed a bearer token that the identical value answered
    /// <see cref="RedactionPolicy.Marker"/> for as a captured parameter.
    /// Keep this method and <see cref="RenderString"/> reading the same two
    /// lines: a secret must not depend on whether the value was narrated or
    /// captured.
    /// </para>
    /// </remarks>
    public static string RenderNarrationText(string text, RenderOptions? options = null)
    {
        var opts = options ?? new RenderOptions();
        return opts.RedactionPolicy.ShouldRedactValue(text)
            ? RedactionPolicy.Marker
            : Truncate(ControlEscape.Sanitize(text), opts);
    }

    /// <summary>
    /// Renders a value into the structured <see cref="RenderedValue"/>
    /// hierarchy, preserving type information (long/double/bool/instant)
    /// for typed exporters. Companion to the flat string
    /// <see cref="Render"/> path.
    /// </summary>
    public static RenderedValue RenderStructured(
        object? value, RenderOptions? options = null)
    {
        if (value is null)
        {
            return new RenderedValue.NullVal();
        }

        var opts = options ?? new RenderOptions();
        var seen = new HashSet<object>(new ReferenceComparer());
        try
        {
            return RenderStructuredValue(value, opts, seen, 0);
        }
        catch
        {
            return new RenderedValue.StringVal($"<{value.GetType().Name}>");
        }
    }

    private static RenderedValue RenderStructuredValue(
        object value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        return value switch
        {
            string s => RenderStructuredString(s, opts),
            bool b => new RenderedValue.BooleanVal(b),
            DateTime dt => new RenderedValue.InstantVal(
                new DateTimeOffset(dt).ToUnixTimeMilliseconds()),
            DateTimeOffset dto => new RenderedValue.InstantVal(
                dto.ToUnixTimeMilliseconds()),
            int or long or short or byte
                or sbyte or ushort or uint
                or float or double or decimal =>
                RenderStructuredNumber(value),
            char c =>
                new RenderedValue.StringVal(c.ToString()),
            // Enum.ToString() is not sealed the way a struct's is: a
            // C#-compiled member name can't carry a control character, but
            // nothing at the CLR level stops an IL-authored assembly from
            // defining one that does. Sanitize, no length cap — matching
            // StringVal's own no-cap contract, not RenderString's.
            Enum e =>
                new RenderedValue.StringVal(ControlEscape.Sanitize(e.ToString()!)),
            System.Threading.Tasks.Task task =>
                RenderStructuredTask(task, opts, seen, depth),
            _ => RenderStructuredComplex(value, opts, seen, depth),
        };
    }

    private static RenderedValue RenderStructuredString(string s, RenderOptions opts)
    {
        return opts.RedactionPolicy.ShouldRedactValue(s)
            ? new RenderedValue.StringVal(RedactionPolicy.Marker)
            : new RenderedValue.StringVal(s);
    }

    private static RenderedValue RenderStructuredNumber(object value)
    {
        return value is float or double or decimal
            ? new RenderedValue.DoubleVal(Convert.ToDouble(
                value, CultureInfo.InvariantCulture))
            : new RenderedValue.LongVal(Convert.ToInt64(
                value, CultureInfo.InvariantCulture));
    }

    private static RenderedValue RenderStructuredTask(
        System.Threading.Tasks.Task task,
        RenderOptions opts, HashSet<object> seen, int depth)
    {
        switch (task.Status)
        {
            case System.Threading.Tasks.TaskStatus.RanToCompletion:
                return TryTaskResult(task, out var result)
                    ? RenderStructuredItem(result, opts, seen, depth)
                    : new RenderedValue.StringVal("<error>");
            case System.Threading.Tasks.TaskStatus.Canceled:
                return new RenderedValue.StringVal("<cancelled>");
            case System.Threading.Tasks.TaskStatus.Faulted:
                return new RenderedValue.StringVal("<faulted>");
            default:
                return new RenderedValue.StringVal("<pending>");
        }
    }

    private static RenderedValue RenderStructuredComplex(
        object value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (depth >= opts.MaxDepth)
        {
            return new RenderedValue.StringVal("<...>");
        }

        return value switch
        {
            System.Collections.IDictionary dict =>
                RenderStructuredDictionary(dict, opts, seen, depth),
            System.Collections.IEnumerable seq =>
                RenderStructuredList(seq, opts, seen, depth),
            _ => RenderStructuredShaped(value, opts, seen, depth),
        };
    }

    // The same ordered decision RenderShaped makes, resolved from one cached
    // TypeShape instead of three reflective probes per render: a
    // [NarrativeSummary] member wins, then a record renders as an object even
    // with no public members, then any type that has public members, and only
    // a type with none of those reaches its own ToString().
    private static RenderedValue RenderStructuredShaped(
        object value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        var shape = TypeShape.Of(value.GetType());
        if (shape.SummaryMethod is { } summary)
        {
            return new RenderedValue.StringVal(
                InvokeSummary(summary, value));
        }

        return shape.IsRecord || shape.Members.Length > 0
            ? RenderStructuredObject(value, shape, opts, seen, depth)
            : new RenderedValue.StringVal(SafeToString(value, opts));
    }

    // Safe enumeration primitives shared by both the flat and structured
    // renderers: a hostile MoveNext must never abort the whole render, and a
    // hostile Current degrades only that one element while iteration
    // continues (the no-poison contract). GetEnumerator() itself is
    // deliberately NOT guarded here — nothing has been collected yet at that
    // point, so there is no partial output worth preserving; letting it
    // propagate lets the nearest real boundary (a parent collection's
    // per-item guard, a parent object's per-member guard, or the top-level
    // Render/RenderStructured catch) degrade at the right granularity
    // instead of this helper inventing a misleading "empty" result.
    private static bool TryMoveNext(
        System.Collections.IEnumerator enumerator, out bool moved)
    {
        try
        {
            moved = enumerator.MoveNext();
            return true;
        }
        catch
        {
            moved = false;
            return false;
        }
    }

    private static (List<TOut> Items, int Total) SafeCollectFrom<TOut>(
        System.Collections.IEnumerator enumerator, int max,
        Func<object?, TOut> render, TOut errorItem)
    {
        try
        {
            var items = new List<TOut>();
            var total = SafeCollectLoop(enumerator, max, render, errorItem, items);
            return (items, total);
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    private static (List<TOut> Items, int Total) SafeCollect<TOut>(
        System.Collections.IEnumerable seq, int max,
        Func<object?, TOut> render, TOut errorItem)
    {
        return SafeCollectFrom(seq.GetEnumerator(), max, render, errorItem);
    }

    private static int SafeCollectLoop<TOut>(
        System.Collections.IEnumerator enumerator, int max,
        Func<object?, TOut> render, TOut errorItem, List<TOut> items)
    {
        var total = 0;
        while (TryMoveNext(enumerator, out var moved))
        {
            if (!moved)
            {
                return total;
            }

            total++;
            if (items.Count < max)
            {
                items.Add(SafeRenderItem(enumerator, render, errorItem));
            }
        }

        // MoveNext itself threw: iteration can no longer continue safely,
        // but the render still shows what was collected before the failure.
        if (items.Count < max)
        {
            items.Add(errorItem);
        }

        return total + 1;
    }

    private static TOut SafeRenderItem<TOut>(
        System.Collections.IEnumerator enumerator,
        Func<object?, TOut> render, TOut errorItem)
    {
        try
        {
            return render(enumerator.Current);
        }
        catch
        {
            return errorItem;
        }
    }

    private static RenderedValue RenderStructuredList(
        System.Collections.IEnumerable seq, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (!seen.Add(seq))
        {
            return new RenderedValue.StringVal(CircularRef(seq));
        }

        try
        {
            var (items, _) = SafeCollect(
                seq, opts.MaxArrayItems,
                item => RenderStructuredItem(item, opts, seen, depth + 1),
                (RenderedValue)new RenderedValue.StringVal("<error>"));
            return new RenderedValue.ListVal(items);
        }
        finally
        {
            seen.Remove(seq);
        }
    }

    private static RenderedValue RenderStructuredObject(
        object value, TypeShape shape, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (!seen.Add(value))
        {
            return new RenderedValue.StringVal(CircularRef(value));
        }

        try
        {
            var fields = new Dictionary<string, RenderedValue>();
            var members = shape.Members;
            var limit = Math.Min(members.Length, opts.MaxObjectKeys);
            for (var i = 0; i < limit; i++)
            {
                fields[members[i].Name] = RenderStructuredMember(
                    members[i], value, opts, seen, depth);
            }

            return new RenderedValue.ObjectVal(shape.TypeName, fields);
        }
        finally
        {
            seen.Remove(value);
        }
    }

    private static RenderedValue RenderStructuredMember(
        MemberSlot member, object target,
        RenderOptions opts, HashSet<object> seen, int depth)
    {
        if (IsRedacted(member, opts))
        {
            return new RenderedValue.StringVal(RedactionPolicy.Marker);
        }

        try
        {
            return RenderStructuredItem(
                member.Read(target), opts, seen, depth + 1);
        }
        catch (Exception ex)
        {
            return new RenderedValue.StringVal(ErrorMarker(ex));
        }
    }

    private static RenderedValue RenderStructuredDictionary(
        System.Collections.IDictionary dict, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (!seen.Add(dict))
        {
            return new RenderedValue.StringVal(CircularRef(dict));
        }

        try
        {
            return new RenderedValue.ObjectVal(
                "Map", CollectStructuredEntries(dict, opts, seen, depth));
        }
        finally
        {
            seen.Remove(dict);
        }
    }

    private static Dictionary<string, RenderedValue>
        CollectStructuredEntries(
            System.Collections.IDictionary dict, RenderOptions opts,
            HashSet<object> seen, int depth)
    {
        // dict.GetEnumerator() here resolves to IDictionary's overload
        // (IDictionaryEnumerator, yielding DictionaryEntry) precisely
        // because dict is statically typed IDictionary at this call site —
        // routing through the generic IEnumerable-typed SafeCollect would
        // instead bind IEnumerable.GetEnumerator(), which for a generic
        // Dictionary<TKey,TValue> yields boxed KeyValuePair, not
        // DictionaryEntry.
        var fields = new Dictionary<string, RenderedValue>();
        var enumerator = dict.GetEnumerator();
        try
        {
            DriveStructuredEntries(enumerator, opts, seen, depth, fields);
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }

        return fields;
    }

    private static void DriveStructuredEntries(
        System.Collections.IEnumerator enumerator, RenderOptions opts,
        HashSet<object> seen, int depth,
        Dictionary<string, RenderedValue> fields)
    {
        while (fields.Count < opts.MaxObjectKeys
            && TryMoveNext(enumerator, out var moved) && moved)
        {
            AddStructuredEntrySafely(fields, enumerator, opts, seen, depth);
        }
    }

    // A hostile DictionaryEntry cast/Current getter degrades only this one
    // entry — MoveNext already succeeded, so the outer loop keeps going.
    private static void AddStructuredEntrySafely(
        Dictionary<string, RenderedValue> fields,
        System.Collections.IEnumerator enumerator,
        RenderOptions opts, HashSet<object> seen, int depth)
    {
        try
        {
            var entry = (System.Collections.DictionaryEntry)enumerator.Current!;
            AddStructuredEntry(fields, entry, opts, seen, depth);
        }
        catch
        {
            fields[$"<error-{fields.Count}>"] =
                new RenderedValue.StringVal("<error>");
        }
    }

    private static void AddStructuredEntry(
        Dictionary<string, RenderedValue> fields,
        System.Collections.DictionaryEntry entry,
        RenderOptions opts, HashSet<object> seen, int depth)
    {
        var key = RenderMapKey(entry.Key, opts, seen, depth);
        fields[key] = opts.RedactionPolicy.ShouldRedact(key)
            ? new RenderedValue.StringVal(RedactionPolicy.Marker)
            : RenderStructuredItem(entry.Value, opts, seen, depth + 1);
    }

    private static RenderedValue RenderStructuredItem(
        object? value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        return value is null
            ? new RenderedValue.NullVal()
            : RenderStructuredValue(value, opts, seen, depth);
    }

    private static string RenderValue(
        object value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        return value switch
        {
            string s => RenderString(s, opts),
            bool b => b ? "true" : "false",
            char c => $"'{c}'",
            // decimal exposes a public Scale property, so without this case it
            // would fall through to the object renderer as "Decimal{Scale: n}".
            decimal m => m.ToString(CultureInfo.InvariantCulture),
            double d => d.ToString(CultureInfo.InvariantCulture),
            float f => f.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            Type t => $"typeof({t.Name})",
            Exception ex => RenderExceptionSummary(ex),
            Delegate => "<function>",
            System.Threading.Tasks.Task task =>
                RenderTask(task, opts, seen, depth),
            _ => RenderComplex(value, opts, seen, depth),
        };
    }

    // ex.Message is virtual — a hostile subclass overriding it to throw must
    // still yield the (safe, non-overridable) type name rather than escape.
    // ExceptionMessage.Text guards the read itself (degrading to "<error>");
    // the try/catch here is a second, redundant net for whatever else this
    // interpolation could throw.
    private static string RenderExceptionSummary(Exception ex)
    {
        try
        {
            return $"{ex.GetType().Name}: {ExceptionMessage.Text(ex)}";
        }
        catch
        {
            return $"{ex.GetType().Name}: <error>";
        }
    }

    private static string RenderTask(
        System.Threading.Tasks.Task task,
        RenderOptions opts, HashSet<object> seen,
        int depth)
    {
        return task.Status switch
        {
            System.Threading.Tasks.TaskStatus
                .RanToCompletion =>
                RenderTaskResult(
                    task, opts, seen, depth),
            System.Threading.Tasks.TaskStatus.Canceled =>
                "<cancelled>",
            System.Threading.Tasks.TaskStatus.Faulted =>
                "<faulted>",
            _ => "<pending>",
        };
    }

    private static string RenderTaskResult(
        System.Threading.Tasks.Task task,
        RenderOptions opts, HashSet<object> seen,
        int depth)
    {
        if (!TryTaskResult(task, out var result))
        {
            return "<error>";
        }

        return result is null
            ? "null"
            : RenderValue(result, opts, seen, depth);
    }

    // The reflected Result getter is not guaranteed non-throwing — a
    // subclass hiding Task&lt;T&gt;.Result with `new` can make it throw even
    // though Status already reports RanToCompletion.
    private static bool TryTaskResult(
        System.Threading.Tasks.Task task, out object? result)
    {
        result = null;
        var type = task.GetType();
        if (!type.IsGenericType)
        {
            return true;
        }

        // GetProperty itself can throw (e.g. AmbiguousMatchException for a
        // subclass that hides Result with `new`), not just GetValue — both
        // must stay inside the guard.
        try
        {
            var resultProp = type.GetProperty("Result");
            if (resultProp is null)
            {
                return true;
            }

            result = resultProp.GetValue(task);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string RenderComplex(
        object value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (depth >= opts.MaxDepth)
        {
            return "<...>";
        }

        return value switch
        {
            System.Collections.IDictionary dict =>
                RenderDictionary(dict, opts, seen, depth),
            System.Collections.IEnumerable seq =>
                RenderEnumerable(seq, opts, seen, depth),
            _ => RenderShaped(value, opts, seen, depth),
        };
    }

    // A found [NarrativeSummary] member always short-circuits this decision,
    // success or failure: a throwing summary must degrade to a typed
    // placeholder for that part, never fall through to a full reflective field
    // dump of the same object — the summary was curated precisely so the
    // fields wouldn't be shown raw. A record keeps the parenthesised form even
    // with no public members, which is why IsRecord is asked before the member
    // count rather than folded into it.
    private static string RenderShaped(
        object value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        var shape = TypeShape.Of(value.GetType());
        if (shape.SummaryMethod is { } summary)
        {
            return InvokeSummary(summary, value);
        }

        if (shape.IsRecord)
        {
            return RenderObject(value, shape, opts, seen, "(", ")", depth);
        }

        return shape.Members.Length > 0
            ? RenderObject(value, shape, opts, seen, "{", "}", depth)
            : SafeToString(value, opts);
    }

    private static string RenderItem(
        object? value, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        return value is null
            ? "null"
            : RenderValue(value, opts, seen, depth);
    }

    private static string InvokeSummary(
        System.Reflection.MethodInfo method, object value)
    {
        try
        {
            return ControlEscape.Sanitize(
                method.Invoke(value, null)?.ToString() ?? "null");
        }
        catch (Exception ex)
        {
            return ErrorMarker(ex);
        }
    }

    private static string CircularRef(object value)
    {
        var hash = System.Runtime.CompilerServices
            .RuntimeHelpers.GetHashCode(value);
        return $"<{value.GetType().Name}@{hash:x}>";
    }

    private static string RenderObject(
        object value, TypeShape shape, RenderOptions opts,
        HashSet<object> seen, string open, string close, int depth)
    {
        if (!seen.Add(value))
        {
            return CircularRef(value);
        }

        try
        {
            return JoinWithTruncation(
                RenderMembers(value, shape, opts, seen, depth),
                shape.Members.Length, opts.MaxObjectKeys,
                $"{shape.TypeName}{open}", close);
        }
        finally
        {
            seen.Remove(value);
        }
    }

    // An indexed loop rather than Select+Take: the projection captured
    // value/opts/seen/depth into a display class on every render, and this path
    // runs for every member of every rendered object.
    private static List<string> RenderMembers(
        object value, TypeShape shape, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        var members = shape.Members;
        var limit = Math.Min(members.Length, opts.MaxObjectKeys);
        var rendered = new List<string>(Math.Max(limit, 0));
        for (var i = 0; i < limit; i++)
        {
            rendered.Add(
                $"{members[i].Name}: {RenderMember(
                    members[i], value, opts, seen, depth + 1)}");
        }

        return rendered;
    }

    // Deferred to RedactionPolicy.IsRedacted so this renderer and template
    // resolution (NarrationResolver) apply one rule rather than two
    // implementations of it. The annotation half of the answer is a property of
    // the member alone, so TypeShape resolves it once per type; only the
    // name-based deny-list, which belongs to the caller's policy rather than to
    // the type, is asked per render.
    private static bool IsRedacted(MemberSlot member, RenderOptions opts)
    {
        return opts.RedactionPolicy.IsRedacted(
            member.Name, member.Annotated);
    }

    private static string RenderMember(
        MemberSlot member, object target,
        RenderOptions opts, HashSet<object> seen, int depth)
    {
        if (IsRedacted(member, opts))
        {
            return RedactionPolicy.Marker;
        }

        try
        {
            return RenderItem(
                member.Read(target), opts, seen, depth);
        }
        catch (Exception ex)
        {
            return ErrorMarker(ex);
        }
    }

    private static string RenderDictionary(
        System.Collections.IDictionary dict, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (!seen.Add(dict))
        {
            return CircularRef(dict);
        }

        try
        {
            // dict.GetEnumerator() (IDictionary's overload) yields
            // DictionaryEntry — see the matching note on
            // CollectStructuredEntries for why this can't route through the
            // generic IEnumerable-typed SafeCollect.
            var (entries, total) = SafeCollectFrom(
                dict.GetEnumerator(), opts.MaxObjectKeys,
                boxed => RenderMapEntry(boxed, opts, seen, depth + 1),
                "<error>");
            return JoinMapEntries(entries, total, opts.MaxObjectKeys);
        }
        finally
        {
            seen.Remove(dict);
        }
    }

    // Java renderMap shape: `{key=value, …}` with a bare ellipsis (no count)
    // when the entry cap is exceeded.
    private static string JoinMapEntries(
        List<string> entries, int totalCount, int max)
    {
        var joined = string.Join(", ", entries);
        return totalCount > max
            ? $"{{{joined}, …}}"
            : $"{{{joined}}}";
    }

    // boxed is a DictionaryEntry for any well-behaved IDictionary; the cast
    // itself runs inside SafeCollect's per-item guard, so a hostile
    // enumerator that violates the contract degrades to "<error>" too.
    private static string RenderMapEntry(
        object? boxed, RenderOptions opts, HashSet<object> seen, int depth)
    {
        var entry = (System.Collections.DictionaryEntry)boxed!;
        var key = RenderMapKey(entry.Key, opts, seen, depth);
        var rendered = opts.RedactionPolicy.ShouldRedact(key)
            ? RedactionPolicy.Marker
            : RenderItem(entry.Value, opts, seen, depth);
        return $"{key}={rendered}";
    }

    private static string RenderEnumerable(
        System.Collections.IEnumerable seq, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        if (!seen.Add(seq))
        {
            return CircularRef(seq);
        }

        try
        {
            var (items, total) = SafeCollect(
                seq, opts.MaxArrayItems,
                item => RenderItem(item, opts, seen, depth + 1),
                "<error>");
            return JoinWithTruncation(
                items, total, opts.MaxArrayItems, "[", "]");
        }
        finally
        {
            seen.Remove(seq);
        }
    }

    private static string JoinWithTruncation(
        List<string> items, int totalCount, int max,
        string open, string close)
    {
        var joined = string.Join(", ", items);
        if (totalCount > max)
        {
            return $"{open}{joined}, ... ({totalCount} total){close}";
        }

        return $"{open}{joined}{close}";
    }

    /// <summary>
    /// Renders a map key through the same guarded path as any other value, so
    /// key objects honour <see cref="NotTracedAttribute"/>, the redaction
    /// deny-list, cycle detection and the depth/size bounds. String keys keep
    /// their bare form (no quotes) for readability.
    /// </summary>
    private static string RenderMapKey(
        object key, RenderOptions opts,
        HashSet<object> seen, int depth)
    {
        return key is string s
            ? Truncate(ControlEscape.Sanitize(s), opts)
            : RenderValue(key, opts, seen, depth);
    }

    private static string SafeToString(object value, RenderOptions opts)
    {
        try
        {
            return value.ToString() is { } s
                ? Truncate(ControlEscape.Sanitize(s), opts)
                : $"<{value.GetType().Name}>";
        }
        catch (Exception ex)
        {
            return ErrorMarker(ex);
        }
    }

    /// <summary>
    /// The typed placeholder for a part of the render that failed: a custom
    /// <see cref="object.ToString"/>, a <see cref="NarrativeSummaryAttribute"/>
    /// member, or a property/field getter reached during reflective
    /// introspection — the three extension points into caller code the
    /// README documents as exception-isolated. Carries the caught
    /// exception's own type name only, never <see cref="Exception.Message"/>,
    /// which could carry the very value the render was trying to protect.
    /// </summary>
    /// <remarks>
    /// This is a <see langword="try"/>/<see langword="catch"/> guard, not a
    /// stack-depth guard: a <see cref="StackOverflowException"/> from a
    /// recursing <c>ToString()</c> is an unmanaged fault the CLR cannot
    /// deliver to any managed handler, so it terminates the process before
    /// this method — or anything else in this class — ever runs. What
    /// actually stops that case is <see cref="RenderOptions.MaxDepth"/> and
    /// the reference-identity cycle guard on the reflective walk, which is
    /// why user <c>ToString()</c> is only ever entered for a leaf value —
    /// one with no public members left to walk — never for the composite
    /// doing the recursing.
    /// </remarks>
    private static string ErrorMarker(Exception ex) => $"<error: {Unwrapped(ex).GetType().Name}>";

    // PropertyInfo.GetValue and MethodInfo.Invoke both reach the target
    // member through reflection's own invoker, which wraps whatever that
    // member threw in a TargetInvocationException — the marker must name the
    // real failure (e.g. InvalidOperationException), not that plumbing.
    private static Exception Unwrapped(Exception ex) =>
        ex is System.Reflection.TargetInvocationException { InnerException: { } inner }
            ? inner
            : ex;

    private static string Truncate(string s, RenderOptions opts)
    {
        return s.Length > opts.MaxStringLength
            ? $"{s[..opts.MaxStringLength]}..."
            : s;
    }

    private static string RenderString(string s, RenderOptions opts)
    {
        return opts.RedactionPolicy.ShouldRedactValue(s)
            ? RedactionPolicy.Marker
            : $"\"{Truncate(ControlEscape.Sanitize(s), opts)}\"";
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public new bool Equals(object? x, object? y) =>
            ReferenceEquals(x, y);

        public int GetHashCode(object obj) =>
            System.Runtime.CompilerServices
                .RuntimeHelpers.GetHashCode(obj);
    }
}
