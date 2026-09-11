// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.Proxy;

/// <summary>
/// The <see cref="DispatchProxy"/> that intercepts interface calls and
/// records them into an <see cref="INarrativeContext"/>. Instances are
/// created and wired up by <see cref="NarrativeTraceProxy"/>.
/// </summary>
/// <remarks>
/// On each call the interceptor forwards to the wrapped target, then
/// captures parameter names and values (redacting <c>[NotTraced]</c>
/// positions), resolves any <see cref="NarratedAttribute"/> narration on
/// entry, and — when the call throws — the most specific matching
/// <see cref="OnErrorAttribute"/> context on exit. Synchronous,
/// <see cref="System.Threading.Tasks.Task"/>, and
/// <see cref="System.Threading.Tasks.ValueTask"/> returns are all traced,
/// with the exit deferred until an async result settles. When the context
/// is inactive the call degrades to a plain pass-through. Per-method
/// metadata is reflected once and cached. This type is public only because
/// <see cref="DispatchProxy"/> requires it; construct proxies through
/// <see cref="NarrativeTraceProxy"/> rather than directly.
/// </remarks>
public class NarrativeInterceptor : DispatchProxy
{
    private static readonly ConcurrentDictionary<
        MethodInfo, MethodMetadata> Cache = new();

    private object _target = null!;
    private INarrativeContext _context = null!;
    private ProxyOptions? _options;

    internal void Initialize(
        object target,
        INarrativeContext context,
        ProxyOptions? options)
    {
        _target = target;
        _context = context;
        _options = options;
    }

    internal static int MetadataCacheCount => Cache.Count;

    /// <summary>
    /// Intercepts a call on the proxied interface, tracing it when the
    /// context is active and otherwise forwarding it unchanged.
    /// </summary>
    /// <param name="targetMethod">The interface method being invoked.</param>
    /// <param name="args">The arguments passed to the call.</param>
    /// <returns>The value returned by the wrapped target.</returns>
    protected override object? Invoke(
        MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod is null)
        {
            return null;
        }

        var actualArgs = args ?? [];
        if (!_context.IsActive)
        {
            return InvokeRaw(targetMethod, actualArgs);
        }

        var handle = EnterTrace(targetMethod, actualArgs);
        return InvokeAndTrace(targetMethod, actualArgs, handle);
    }

    // Inactive contexts skip all capture, metadata, and template work —
    // the proxy degrades to a plain pass-through (JVM-edition fast path).
    private object? InvokeRaw(
        MethodInfo method, object?[] args)
    {
        try
        {
            return method.Invoke(_target, args);
        }
        catch (TargetInvocationException tie)
            when (tie.InnerException is not null)
        {
            throw tie.InnerException;
        }
    }

    private static MethodMetadata GetMetadata(
        MethodInfo method)
    {
        return Cache.GetOrAdd(method, BuildMetadata);
    }

    private static MethodMetadata BuildMetadata(
        MethodInfo method)
    {
        var (names, redacted) = BuildParameterInfo(
            method);
        var narration = method
            .GetCustomAttribute<NarratedAttribute>()
            ?.Template;
        var errors = method
            .GetCustomAttributes<OnErrorAttribute>()
            .ToArray();

        return new MethodMetadata(
            method.DeclaringType?.Name ?? "Unknown",
            method.Name,
            names,
            redacted,
            narration,
            errors,
            method.DeclaringType?.Namespace,
            method.ReturnType.ToString(),
            method.GetParameters().Select(p => p.ParameterType.ToString()).ToArray());
    }

    // The name deny-list decides here, beside the [NotTraced] annotation, and
    // not in a renderer: BuildParameterInfo runs once per method (behind the
    // Cache in GetMetadata), so the lookup happens once and never on a traced
    // call. Deciding at capture also means a denied value never enters the
    // captured trace at all, so it cannot reach the audit emitter, the
    // buffered consumer, or any SPI listener either (mirrors the java
    // flagship's ProxyMethodMetadata/AgentMethodMetadata fix).
    //
    // Tested against parameters[i].Name (the REFLECTED name), not the
    // possibly-overridden names[i] display name: RedactionPolicy.IsRedacted
    // is the single "is this redacted?" decision every named-member surface
    // asks (see its own remarks), and NarrationResolver's template path
    // already asks it with the reflected parameter name — asking it with a
    // different input here would let the two capture paths drift, and a
    // [Traced] display-name override (chosen for narrative readability, not
    // security) would then decide whether a secret is hidden.
    private static (string[] Names, HashSet<int> Redacted)
        BuildParameterInfo(MethodInfo method)
    {
        var parameters = method.GetParameters();
        var traced = method
            .GetCustomAttribute<TracedAttribute>();
        var names = new string[parameters.Length];
        var redacted = new HashSet<int>();

        for (var i = 0; i < parameters.Length; i++)
        {
            names[i] = GetParameterName(
                parameters[i], traced?.ParameterNames, i);
            var annotated = parameters[i]
                .GetCustomAttribute<NotTracedAttribute>()
                is not null;
            if (RedactionPolicy.Default.IsRedacted(
                parameters[i].Name, annotated))
            {
                redacted.Add(i);
            }
        }

        return (names, redacted);
    }

    private SpanId EnterTrace(
        MethodInfo method, object?[] args)
    {
        var meta = GetMetadata(method);
        var className = _options?.ClassName
            ?? meta.ClassName;
        var parameters = CaptureParameters(
            meta, args, _context.CapturesParameterValues);
        var options = new MethodOptions(
            ResolveNarration(meta, args, method),
            Namespace: meta.Namespace,
            ReturnType: meta.ReturnType,
            NarrationTemplate: meta.NarrationTemplate);
        return _context.EnterMethod(
            className, meta.MethodName,
            parameters, options);
    }

    private static string? ResolveNarration(
        MethodMetadata meta, object?[] args,
        MethodInfo method)
    {
        if (meta.NarrationTemplate is null)
        {
            return null;
        }

        return NarrationResolver.Resolve(
            meta.NarrationTemplate,
            args,
            method.GetParameters());
    }

    // Resolved at exception time: only templates whose declared type
    // matches the THROWN exception compete, and the most specific match
    // wins. No match means no error context (JVM-edition parity).
    private static string? ResolveErrorContext(
        MethodMetadata meta, object?[] args,
        MethodInfo method, Exception thrown)
    {
        var match = meta.ErrorTemplates
            .Where(a => a.ExceptionType.IsInstanceOfType(thrown))
            .OrderByDescending(a =>
                GetInheritanceDepth(a.ExceptionType))
            .FirstOrDefault();
        if (match is null)
        {
            return null;
        }

        return NarrationResolver.Resolve(
            match.Template,
            args,
            method.GetParameters());
    }

    private static int GetInheritanceDepth(Type type)
    {
        var depth = 0;
        var current = type;
        while (current is not null)
        {
            depth++;
            current = current.BaseType;
        }

        return depth;
    }

    private object? InvokeAndTrace(
        MethodInfo method, object?[] args, SpanId handle)
    {
        try
        {
            var result = _context.RunScoped(
                handle,
                () => method.Invoke(_target, args));
            return TraceResult(result, method, args, handle);
        }
        catch (TargetInvocationException tie)
            when (tie.InnerException is not null)
        {
            var inner = tie.InnerException;
            _context.ExitMethodWithException(
                inner,
                ErrorContextFor(method, args, inner),
                handle);
            throw inner;
        }
    }

    private static string? ErrorContextFor(
        MethodInfo method, object?[] args, Exception thrown)
    {
        return ResolveErrorContext(
            GetMetadata(method), args, method, thrown);
    }

    private object? TraceResult(
        object? result, MethodInfo method, object?[] args,
        SpanId handle)
    {
        var asyncTask = ConvertToTask(
            result, method.ReturnType);
        if (asyncTask is not null)
        {
            _context.DetachFrame(handle);
            return TraceAsyncResult(
                asyncTask, method,
                ex => ErrorContextFor(method, args, ex),
                handle);
        }

        ExitWithRenderedReturn(result, method.ReturnType, handle);
        return result;
    }

#pragma warning disable S4586
    private static Task? ConvertToTask(
        object? result, Type returnType)
    {
        if (result is Task task)
        {
            return task;
        }

        if (returnType == typeof(ValueTask))
        {
            return ((ValueTask)result!).AsTask();
        }

        if (returnType.IsGenericType
            && returnType.GetGenericTypeDefinition()
                == typeof(ValueTask<>))
        {
            return (Task)returnType
                .GetMethod("AsTask")!
                .Invoke(result, null)!;
        }

        return null;
    }
#pragma warning restore S4586

    private object TraceAsyncResult(
        Task task, MethodInfo method,
        Func<Exception, string?> errorContextFor,
        SpanId handle)
    {
        var returnType = method.ReturnType;

        if (HasGenericResult(returnType))
        {
            var resultType = returnType
                .GetGenericArguments()[0];
            var traced = TraceAsyncGeneric(
                task, resultType, errorContextFor, handle);
            return WrapIfValueTask(
                traced, returnType, resultType);
        }

        var tracedVoid = TraceAsyncVoid(
            task, errorContextFor, handle);
        if (returnType == typeof(ValueTask))
        {
            return new ValueTask(tracedVoid);
        }

        return tracedVoid;
    }

    private static bool HasGenericResult(Type returnType)
    {
        if (!returnType.IsGenericType)
        {
            return false;
        }

        var def = returnType.GetGenericTypeDefinition();
        return def == typeof(Task<>)
            || def == typeof(ValueTask<>);
    }

    private static object WrapIfValueTask(
        object tracedTask, Type returnType,
        Type resultType)
    {
        if (!returnType.IsGenericType
            || returnType.GetGenericTypeDefinition()
                != typeof(ValueTask<>))
        {
            return tracedTask;
        }

        var ctor = typeof(ValueTask<>)
            .MakeGenericType(resultType)
            .GetConstructor([
                typeof(Task<>).MakeGenericType(resultType),
            ])!;
        return ctor.Invoke([tracedTask]);
    }

    private Task TraceAsyncVoid(
        Task task, Func<Exception, string?> errorContextFor,
        SpanId handle)
    {
        return task.ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var ex = t.Exception!.InnerException!;
                SafeRecordException(ex, errorContextFor(ex), handle);
                throw ex;
            }

            SafeRecordVoidReturn(handle);
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }

#pragma warning disable S3011
    private object TraceAsyncGeneric(
        Task task, Type resultType,
        Func<Exception, string?> errorContextFor,
        SpanId handle)
    {
        var method = typeof(NarrativeInterceptor)
            .GetMethod(
                nameof(TraceAsyncGenericCore),
                BindingFlags.NonPublic
                    | BindingFlags.Instance)!
            .MakeGenericMethod(resultType);
        return method.Invoke(
            this, [task, errorContextFor, handle])!;
    }
#pragma warning restore S3011

    private Task<T> TraceAsyncGenericCore<T>(
        Task task, Func<Exception, string?> errorContextFor,
        SpanId handle)
    {
        return ((Task<T>)task).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                var ex = t.Exception!.InnerException!;
                SafeRecordException(ex, errorContextFor(ex), handle);
                throw ex;
            }

            SafeRecordReturn(t.Result, handle);
            return t.Result;
        },
        CancellationToken.None,
        TaskContinuationOptions.ExecuteSynchronously,
        TaskScheduler.Default);
    }

    // Deferred-exit completion guarantee (mirrors a java flagship fix): once a business
    // Task/ValueTask has settled, an observability failure while recording the
    // exit must never replace the business outcome the caller awaits.
    private void SafeRecordException(
        Exception ex, string? errorContext, SpanId handle)
    {
        try
        {
            _context.ExitMethodWithException(ex, errorContext, handle);
        }
        catch (Exception)
        {
            // Original business exception is re-thrown by the caller unchanged.
        }
    }

    private void SafeRecordReturn(object? result, SpanId handle)
    {
        try
        {
            var rendered = ShouldRenderReturn
                ? ValueRenderer.Render(result)
                : null;
            _context.ExitMethodWithReturn(rendered, handle);
        }
        catch (Exception)
        {
            // Business result is returned to the caller unchanged.
        }
    }

    private void SafeRecordVoidReturn(SpanId handle)
    {
        try
        {
            _context.ExitMethodWithReturn(null, handle);
        }
        catch (Exception)
        {
            // Completed Task reflects the business outcome, not this failure.
        }
    }

    private bool ShouldRenderReturn =>
        _options?.IncludeReturnValues ?? true;

    /// <summary>
    /// Void-completion contract: a <c>void</c> method carries no rendered
    /// value at all, so a rendered <c>"null"</c> always means the method
    /// really returned null.
    /// </summary>
    private void ExitWithRenderedReturn(
        object? result, Type returnType, SpanId handle)
    {
        var rendered = ShouldRenderReturn
            && returnType != typeof(void)
            ? ValueRenderer.Render(result)
            : null;
        _context.ExitMethodWithReturn(rendered, handle);
    }

    // The name axis (meta.RedactedIndices) is decided once per method, at
    // metadata-build time, because it depends only on the parameter's
    // declared name. A VALUE-shape secret (JWT/PAN/national-id/SSN) cannot
    // be decided there — it depends on the actual argument THIS call
    // carries — so RenderParameter/ValueRenderer.Render already catches it
    // per call, in the rendered text. Without this, that rendered text was
    // correctly masked while ParameterCapture.Redacted stayed false for the
    // same value: a downstream consumer trusting the flag (rather than
    // string-sniffing RenderedValue) would misclassify it as visible. Found
    // by RedactionCapturePathConformanceTests, which replays the shared
    // corpus through this real capture path rather than through
    // ValueRenderer directly.
    private static IReadOnlyList<ParameterCapture>
        CaptureParameters(
            MethodMetadata meta, object?[] args,
            bool captureValues)
    {
        var captures =
            new ParameterCapture[meta.ParameterNames.Length];

        for (var i = 0; i < captures.Length; i++)
        {
            var nameRedacted = meta.RedactedIndices.Contains(i);
            var rendered = RenderParameter(args[i], nameRedacted, captureValues);
            captures[i] = new ParameterCapture(
                meta.ParameterNames[i],
                rendered,
                nameRedacted || IsMarker(rendered),
                DeclaredType: DeclaredTypeAt(meta, i));
        }

        return captures;
    }

    // Reads the decision back off the text ValueRenderer already rendered,
    // rather than asking RedactionPolicy a second time (which would repeat
    // the exact check ValueRenderer just made, on every string parameter of
    // every traced call): an unredacted string is always quote-wrapped by
    // ValueRenderer.Render, so it can never collide with the bare marker
    // constant, and neither can any other scalar's rendering.
    private static bool IsMarker(string rendered) =>
        rendered == RedactionPolicy.Marker;

    /// <summary>
    /// The declared type of parameter <paramref name="index"/>, or null when
    /// the metadata predates type capture (a hand-built metadata record).
    /// </summary>
    private static string? DeclaredTypeAt(MethodMetadata meta, int index)
    {
        return meta.ParameterTypes is { } types && index < types.Length
            ? types[index]
            : null;
    }

    private static string RenderParameter(
        object? arg, bool redacted, bool captureValues)
    {
        if (!captureValues)
        {
            return "";
        }

        return redacted
            ? RedactionPolicy.Marker
            : ValueRenderer.Render(arg);
    }

    private static string GetParameterName(
        ParameterInfo param, string[]? overrides,
        int index)
    {
        if (overrides is not null
            && index < overrides.Length)
        {
            return overrides[index];
        }

        return param.Name ?? $"arg{index}";
    }
}
