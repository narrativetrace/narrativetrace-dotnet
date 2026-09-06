// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Diagnostics;
using System.Globalization;
using NarrativeTrace.Core;

namespace NarrativeTrace.Observability;

/// <summary>
/// Writes NarrativeTrace attributes onto an OpenTelemetry <see cref="Activity"/>
/// using a single, shared convention. Both the batch
/// <see cref="TraceActivityExporter"/> and the live
/// <see cref="OtelTraceEventListener"/> map to identical tag keys and values
/// through this type, so a span looks the same regardless of which path emitted
/// it.
/// </summary>
public static class SpanContextAttributeMapper
{
    /// <summary>Writes class, method, and parameter tags.</summary>
    public static void WriteSignature(Activity activity, MethodSignature signature)
    {
        activity.SetTag("narrative.class", signature.ClassName);
        activity.SetTag("narrative.method", signature.MethodName);
        WriteParameters(activity, signature.Parameters);
    }

    /// <summary>Writes a <c>narrative.param.*</c> tag per non-redacted, non-empty parameter.</summary>
    public static void WriteParameters(
        Activity activity, IReadOnlyList<ParameterCapture> parameters)
    {
        WriteParameters((key, value) => activity.SetTag(key, value), parameters);
    }

    /// <summary>
    /// Writes each parameter through <paramref name="setTag"/> — the shared sink used by
    /// both the span-tag path and the child-event attribute path.
    /// </summary>
    private static void WriteParameters(
        Action<string, object?> setTag, IReadOnlyList<ParameterCapture> parameters)
    {
        for (var i = 0; i < parameters.Count; i++)
        {
            var p = parameters[i];
            if (p.Redacted || string.IsNullOrEmpty(p.RenderedValue))
            {
                continue;
            }

            SetParamAttribute(setTag, $"narrative.param.{p.Name}", p);
        }
    }

    /// <summary>
    /// Writes one parameter: flattens its structured value when present, else
    /// falls back to typed inference from the rendered string (Java parity).
    /// </summary>
    private static void SetParamAttribute(
        Action<string, object?> setTag, string key, ParameterCapture param)
    {
        if (param.StructuredValue is { } structured)
        {
            FlattenStructured(setTag, key, structured, 0);
        }
        else
        {
            SetTypedTag(setTag, key, param.RenderedValue);
        }
    }

    private const int MaxFlattenDepth = 3;

    private static void FlattenStructured(
        Action<string, object?> setTag, string key, RenderedValue value, int depth)
    {
        if (TrySetScalar(setTag, key, value))
        {
            return;
        }

        if (value is RenderedValue.ObjectVal o && depth < MaxFlattenDepth)
        {
            foreach (var field in o.Fields)
            {
                FlattenStructured(
                    setTag, $"{key}.{field.Key}", field.Value, depth + 1);
            }
        }
        else if (value is RenderedValue.ListVal list)
        {
            SetListTag(setTag, key, list);
        }
    }

    /// <summary>Sets a typed-array tag for a homogeneous list; skips empty/mixed lists.</summary>
    private static void SetListTag(
        Action<string, object?> setTag, string key, RenderedValue.ListVal list)
    {
        var elements = list.Elements;
        if (elements.Count == 0
            || elements.Any(e => e.GetType() != elements[0].GetType()))
        {
            return;
        }

        var array = ToTypedArray(elements);
        if (array is not null)
        {
            setTag(key, array);
        }
    }

    /// <summary>Projects a homogeneous element list to a typed array (null if unsupported).</summary>
    private static object? ToTypedArray(IReadOnlyList<RenderedValue> elements)
    {
        return elements[0] switch
        {
            RenderedValue.StringVal => elements
                .Select(e => ((RenderedValue.StringVal)e).Value).ToArray(),
            RenderedValue.LongVal => elements
                .Select(e => ((RenderedValue.LongVal)e).Value).ToArray(),
            RenderedValue.DoubleVal => elements
                .Select(e => ((RenderedValue.DoubleVal)e).Value).ToArray(),
            RenderedValue.BooleanVal => elements
                .Select(e => ((RenderedValue.BooleanVal)e).Value).ToArray(),
            _ => null,
        };
    }

    /// <summary>Sets a scalar-typed tag; returns false for non-scalar values.</summary>
    private static bool TrySetScalar(
        Action<string, object?> setTag, string key, RenderedValue value)
    {
        object? scalar = value switch
        {
            RenderedValue.StringVal s => s.Value,
            RenderedValue.LongVal l => l.Value,
            RenderedValue.DoubleVal d => d.Value,
            RenderedValue.BooleanVal b => b.Value,
            RenderedValue.InstantVal i => i.EpochMillis,
            _ => null,
        };
        if (scalar is null)
        {
            return false;
        }

        setTag(key, scalar);
        return true;
    }

    /// <summary>
    /// Sets a parameter tag, inferring a typed value from its rendered string
    /// (mirrors Java's <c>setTypedFromString</c> fallback for unstructured captures).
    /// </summary>
    private static void SetTypedTag(
        Action<string, object?> setTag, string key, string rendered)
    {
        if (rendered is "true" or "false")
        {
            setTag(key, rendered == "true");
        }
        else if (long.TryParse(
            rendered, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
        {
            setTag(key, l);
        }
        else if (double.TryParse(
            rendered, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            setTag(key, d);
        }
        else
        {
            setTag(key, Unquote(rendered));
        }
    }

    /// <summary>Strips a single pair of surrounding double quotes, if present.</summary>
    private static string Unquote(string rendered)
    {
        return rendered.Length >= 2
            && rendered[0] == '"'
            && rendered[rendered.Length - 1] == '"'
            ? rendered.Substring(1, rendered.Length - 2)
            : rendered;
    }

    /// <summary>
    /// Emits a child method-completion as a timestamped <see cref="ActivityEvent"/> on
    /// the parent span (name <c>Class.Method</c>, timestamp = child end wall-clock),
    /// mirroring Java's <c>emitChildEvent</c>.
    /// </summary>
    public static void EmitChildEvent(Activity parent, TraceNode child)
    {
        var timestamp =
            MonotonicClock.ToWallClockFromTimeSpanTicks(child.StartTimestamp)
            + TimeSpan.FromTicks(child.DurationTicks);
        EmitCompletionEvent(
            parent, child.Signature, child.Outcome, timestamp);
    }

    /// <summary>
    /// Adds a child method-completion <see cref="ActivityEvent"/> (name
    /// <c>Class.Method</c>, typed params, outcome) to the parent span at the given
    /// timestamp. Shared by the batch exporter and the live listener.
    /// </summary>
    public static void EmitCompletionEvent(
        Activity parent, MethodSignature sig, TraceOutcome outcome,
        DateTimeOffset timestamp)
    {
        var name = $"{sig.ClassName}.{sig.MethodName}";
        parent.AddEvent(new ActivityEvent(
            name, timestamp, BuildEventAttributes(sig, outcome)));
    }

    /// <summary>Builds child-event attributes: typed params plus an outcome tag.</summary>
    private static ActivityTagsCollection BuildEventAttributes(
        MethodSignature sig, TraceOutcome outcome)
    {
        var tags = new ActivityTagsCollection();
        WriteParameters((key, value) => tags[key] = value, sig.Parameters);
        AddOutcomeToEvent(tags, outcome);
        return tags;
    }

    private static void AddOutcomeToEvent(
        ActivityTagsCollection tags, TraceOutcome outcome)
    {
        switch (outcome)
        {
            case Returned { RenderedValue: { } value }:
                tags["narrative.outcome"] = value;
                break;
            case Threw t:
                tags["narrative.outcome"] = $"error: {ExceptionMessage.Text(t.Error)}";
                break;
        }
    }

    /// <summary>Writes the outcome tag and sets error status for thrown outcomes.</summary>
    public static void WriteOutcome(Activity activity, TraceOutcome outcome)
    {
        switch (outcome)
        {
            case Returned r:
                WriteOptional(activity, "narrative.outcome", r.RenderedValue);
                break;
            case Threw t:
                activity.SetStatus(ActivityStatusCode.Error, ExceptionMessage.Text(t.Error));
                RecordException(activity, t.Error);
                break;
            case Incomplete:
                activity.SetTag("narrative.outcome", "in-flight");
                break;
        }
    }

    /// <summary>
    /// Writes trace-identity and schema tags; trace-level tags are written only
    /// when <paramref name="isRoot"/> is true (they describe the whole trace).
    /// </summary>
    public static void WriteIdentity(Activity activity, SpanContext? sc, bool isRoot)
    {
        if (sc is null)
        {
            return;
        }

        activity.SetTag("narrative.trace_id", sc.TraceId.Value);
        activity.SetTag("narrative.trace_name", sc.TraceId.HumanName);
        activity.SetTag("nt.entryType", "entry");
        activity.SetTag("nt.schemaVersion", CanonicalSchema.Version);
        WriteOptional(activity, "nt.storyId", sc.StoryId);
        WriteOptional(activity, "nt.chapterId", sc.ChapterId);

        if (isRoot)
        {
            WriteTraceLevel(activity, sc);
        }
    }

    /// <summary>Writes trace-level (whole-request) tags from a span context.</summary>
    public static void WriteTraceLevel(Activity activity, SpanContext sc)
    {
        WriteOptional(activity, "narrative.service.name", sc.ServiceName);
        WriteOptional(activity, "narrative.service.version", sc.ServiceVersion);
        WriteOptional(activity, "narrative.service.environment", sc.Environment);
        WriteOptional(activity, "narrative.http.method", sc.HttpMethod);
        WriteOptional(activity, "narrative.http.route", sc.HttpRoute?.Value);
        WriteOptional(activity, "narrative.client_ip", sc.ClientIp?.Value);
        WriteOptional(activity, "narrative.enduser.id", sc.EnduserId?.Value);
        WriteOptional(activity, "narrative.session.id", sc.SessionId?.Value);
        WriteOptional(activity, "narrative.tenant.id", sc.TenantId?.Value);
    }

    /// <summary>
    /// Records an OTel <c>exception</c> event (type/message/stacktrace) on the
    /// span, mirroring Java's <c>span.recordException</c>.
    /// </summary>
    private static void RecordException(Activity activity, Exception? error)
    {
        if (error is null)
        {
            return;
        }

        var tags = new ActivityTagsCollection
        {
            { "exception.type", error.GetType().ToString() },
            { "exception.message", ExceptionMessage.Text(error) },
            { "exception.stacktrace", error.ToString() },
        };
        activity.AddEvent(new ActivityEvent("exception", tags: tags));
    }

    private static void WriteOptional(Activity activity, string key, string? value)
    {
        if (value is not null)
        {
            activity.SetTag(key, value);
        }
    }
}
