// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Glossary;

/// <summary>
/// Live translated-trace stream: a pipeline listener that renders every event
/// into one locale as it arrives.
/// </summary>
/// <remarks>
/// <para>
/// Register it with a pipeline via <c>consumer.Subscribe(subscriber.OnEvent)</c>.
/// Each event maps to a canonical entry
/// (<see cref="CanonicalEntryMapper.FromEvent"/>) and renders through
/// <see cref="TraceTranslationView.RenderEntry"/> with per-trace state held in
/// a <see cref="PerishableMap{TKey,TValue}"/>. Rendered lines go to the sink
/// one line at a time, without trailing newlines. Two sink shapes ship: an
/// <see cref="Action{T}"/> of lines and a file-writing variant (the
/// output-directory constructors) appending each trace's lines live to
/// <c>&lt;outputDir&gt;/&lt;traceId&gt;.md</c>.
/// </para>
/// <para>
/// Lifecycle of a trace's state: created on the trace's first event, dropped
/// when its root span exits (the glossary-gaps footer for that trace is
/// emitted at that point), and evicted by capacity or TTL for traces that
/// never complete (the footer is emitted on eviction too). A late event
/// arriving after its trace's state was dropped renders at depth 0 —
/// degraded, never wrong.
/// </para>
/// <para>
/// <b>Platform note.</b> The Java runtime offers a default sink writing to the
/// <c>narrativetrace.i18n.&lt;locale&gt;</c> SLF4J logger, because every JVM
/// application already carries that façade. .NET has no equivalent a leaf
/// module may assume — this module deliberately takes no logging package — so
/// the sink is always explicit: pass an <see cref="Action{T}"/> that writes to
/// whatever logger the host already has, or an output directory.
/// </para>
/// <para>
/// The async pipeline path is lossy under load by design (drain-loop
/// shedding, bounded offer timeout) — this subscriber renders what arrives and
/// never blocks the capture path. Concurrency events (fork/join/
/// fire-and-forget) carry no trace identity and are not rendered in translated
/// views yet, and neither are grafted subtrees. Context resolution relies on
/// the captured <c>nt.package</c> field (schema 1.2); there is no class-package
/// index on the live path.
/// </para>
/// </remarks>
public sealed class TranslationSubscriber
{
    /// <summary>Default cap on concurrently tracked traces.</summary>
    public const int DefaultCapacity = 1024;

    private readonly TraceTranslationView view;
    private readonly CanonicalEntryMapper mapper = new();
    private readonly string locale;
    private readonly ITraceLineSink sink;
    private readonly PerishableMap<string, TraceState> states;

    /// <summary>Receives each rendered line together with the trace it belongs to.</summary>
    internal interface ITraceLineSink
    {
        /// <param name="traceId">The trace the line belongs to.</param>
        /// <param name="line">One rendered line, without a trailing newline.</param>
        void Emit(string traceId, string line);
    }

    /// <summary>Default TTL after which an incomplete trace's state is evicted.</summary>
    public static TimeSpan DefaultTtl => TimeSpan.FromMinutes(5);

    /// <summary>Creates a subscriber with default capacity and TTL.</summary>
    /// <param name="glossary">Glossary providing per-context terms and translations; must not be null.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <param name="sink">Receives each rendered line, without a trailing newline; must not be null.</param>
    public TranslationSubscriber(Glossary glossary, string locale, Action<string> sink)
        : this(glossary, locale, sink, DefaultCapacity, DefaultTtl)
    {
    }

    /// <summary>Creates a subscriber with explicit state bounds.</summary>
    /// <param name="glossary">Glossary providing per-context terms and translations; must not be null.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <param name="sink">Receives each rendered line, without a trailing newline; must not be null.</param>
    /// <param name="capacity">Maximum concurrently tracked traces before the oldest state is evicted.</param>
    /// <param name="ttl">Maximum age of an incomplete trace's state before eviction.</param>
    public TranslationSubscriber(
        Glossary glossary, string locale, Action<string> sink, int capacity, TimeSpan ttl)
        : this(glossary, locale, Adapt(sink), capacity, ttl)
    {
    }

    /// <summary>
    /// Creates a file-writing subscriber: each trace's translated lines append
    /// live to <c>&lt;outputDir&gt;/&lt;traceId&gt;.md</c>, with default
    /// capacity and TTL.
    /// </summary>
    /// <remarks>
    /// The same rendering as the callback variant, attached to any run's
    /// pipeline instead of stored test artifacts. Writing is best-effort: an
    /// unwritable file is reported once per subscriber and never propagates.
    /// </remarks>
    /// <param name="glossary">Glossary providing per-context terms and translations; must not be null.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <param name="outputDir">Directory receiving one file per trace; created if absent.</param>
    public TranslationSubscriber(Glossary glossary, string locale, string outputDir)
        : this(glossary, locale, outputDir, DefaultCapacity, DefaultTtl)
    {
    }

    /// <summary>Creates a file-writing subscriber with explicit state bounds.</summary>
    /// <param name="glossary">Glossary providing per-context terms and translations; must not be null.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <param name="outputDir">Directory receiving one file per trace; created if absent.</param>
    /// <param name="capacity">Maximum concurrently tracked traces before the oldest state is evicted.</param>
    /// <param name="ttl">Maximum age of an incomplete trace's state before eviction.</param>
    public TranslationSubscriber(
        Glossary glossary, string locale, string outputDir, int capacity, TimeSpan ttl)
        : this(glossary, locale, outputDir, capacity, ttl, Console.Error)
    {
    }

    /// <summary>
    /// File-writing subscriber with an explicit diagnostics sink for the
    /// one-time write-failure report.
    /// </summary>
    /// <param name="glossary">Glossary providing per-context terms and translations; must not be null.</param>
    /// <param name="locale">Target locale tag (e.g. <c>es</c>); must not be blank.</param>
    /// <param name="outputDir">Directory receiving one file per trace; created if absent.</param>
    /// <param name="capacity">Maximum concurrently tracked traces before the oldest state is evicted.</param>
    /// <param name="ttl">Maximum age of an incomplete trace's state before eviction.</param>
    /// <param name="diagnostics">
    /// Receives the single write-failure report; the default is standard
    /// error, which is where an unconfigured JVM logger would put the same
    /// warning.
    /// </param>
    public TranslationSubscriber(
        Glossary glossary,
        string locale,
        string outputDir,
        int capacity,
        TimeSpan ttl,
        TextWriter diagnostics)
        : this(glossary, locale, new TranslationFileSink(outputDir, diagnostics), capacity, ttl)
    {
    }

    private TranslationSubscriber(
        Glossary glossary, string locale, ITraceLineSink sink, int capacity, TimeSpan ttl)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            throw new ArgumentException("locale must not be blank", nameof(locale));
        }

        // Java passes className -> null here for the same reason: the live
        // path has no class-package index, so context comes from the captured
        // nt.package field alone.
        view = new TraceTranslationView(glossary, _ => null);
        this.locale = locale;
        this.sink = sink;
        states = new PerishableMap<string, TraceState>(capacity, ttl, EmitFooter);
    }

    /// <summary>Renders one trace event into the target locale.</summary>
    /// <param name="traceEvent">The event to render; must not be null.</param>
    /// <exception cref="ArgumentNullException"><paramref name="traceEvent"/> is null.</exception>
    public void OnEvent(TraceEvent traceEvent)
    {
        if (traceEvent is null)
        {
            throw new ArgumentNullException(nameof(traceEvent));
        }

        // A grafted subtree is a node, not a per-event narration: the mapper
        // has no canonical entry for it and the translated stream renders the
        // enters and exits it is built from instead.
        if (traceEvent is GraftEvent)
        {
            return;
        }

        var entry = mapper.FromEvent(traceEvent);
        if (string.IsNullOrEmpty(entry.TraceId))
        {
            return; // concurrency events: no trace identity, not rendered yet
        }

        Render(entry, entry.TraceId!);
    }

    private void Render(CanonicalEntry entry, string traceId)
    {
        var state = states.Get(traceId);
        if (state is null)
        {
            state = new TraceState(traceId, view.NewRenderState(locale));
            states.Put(traceId, state);
        }

        EmitLines(state.TraceId, view.RenderEntry(entry, state.Render));
        if (!IsRootExit(entry))
        {
            return;
        }

        var finished = states.Remove(traceId);
        if (finished is not null)
        {
            EmitFooter(finished);
        }
    }

    /// <summary>Per-trace render state paired with its trace id, so eviction still knows the trace.</summary>
    private sealed record TraceState(string TraceId, TraceTranslationView.RenderState Render);

    private static ITraceLineSink Adapt(Action<string> sink)
    {
        if (sink is null)
        {
            throw new ArgumentNullException(nameof(sink));
        }

        return new CallbackSink(sink);
    }

    private static bool IsRootExit(CanonicalEntry entry)
    {
        return string.Equals(entry.NtEventType, "method_exit", StringComparison.Ordinal)
            && entry.ParentSpanId is null;
    }

    private void EmitFooter(TraceState state)
    {
        EmitLines(state.TraceId, view.RenderGapsFooter(state.Render));
    }

    private void EmitLines(string traceId, string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.Length > 0)
            {
                sink.Emit(traceId, line);
            }
        }
    }

    private sealed class CallbackSink(Action<string> callback) : ITraceLineSink
    {
        public void Emit(string traceId, string line)
        {
            callback(line);
        }
    }
}
