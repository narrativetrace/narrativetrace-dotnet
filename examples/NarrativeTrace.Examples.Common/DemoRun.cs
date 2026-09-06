// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Examples.Common;

/// <summary>
/// The scenario driver every example shares: a shared
/// <see cref="SyncNarrativeContext"/> whose events also stream live through
/// a <see cref="NarrationStreamListener"/>, plus the section markers the demo
/// launcher walks (<c>=== title ===</c>, <c>--- Trace tree ---</c>, …). The
/// example's own prose and the live <c>→ ← !!</c> lines travel through the
/// same <see cref="ILoggerFactory"/>, so the whole run is ordinary logging.
/// </summary>
/// <remarks>
/// Each scenario opens a log scope carrying a sequential <c>traceId</c>
/// (<c>trace-001</c>, <c>trace-002</c>, …) — the stand-in for the
/// request-bound correlation id a web host would supply — and resets the
/// context so every captured tree tells exactly one story.
/// </remarks>
public sealed class DemoRun : IDisposable
{
    /// <summary>Logger category of the live event stream, as in the Java examples.</summary>
    public const string StreamCategory = "narrativetrace";

    private static readonly Action<ILogger, string, Exception?> Line =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, "Line"), "{Text}");

    private static readonly Action<ILogger, string, Exception?> Header =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "Header"), "{Header}\n");

    private static readonly Action<ILogger, string, Exception?> Marker =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, "Marker"), "\n{Marker}\n");

    private static readonly Action<ILogger, string, Exception?> Body =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, "Body"), "\n{Body}");

    private readonly ILogger _logger;
    private readonly DualPathPipeline _pipeline;
    private int _scenarioCount;
    private IDisposable? _scenarioScope;
    private string _scenarioTitle = "";

    /// <summary>
    /// Creates a run whose output goes to <paramref name="loggers"/>;
    /// <paramref name="example"/> names the example's own logger category.
    /// </summary>
    public DemoRun(ILoggerFactory loggers, string example)
    {
        ArgumentNullException.ThrowIfNull(loggers);
        _logger = loggers.CreateLogger(example);
        var stream = new NarrationStreamListener(loggers.CreateLogger(StreamCategory));
        _pipeline = new DualPathPipeline(stream.OnEvent);
        Config = new NarrativeTraceConfig(TracingLevel.Detail);
        Context = new SyncNarrativeContext(Config, _pipeline);
    }

    /// <summary>
    /// The entry point the examples' <c>Program</c> uses: console output in
    /// the format <paramref name="options"/> selects. Names the calling
    /// thread <c>main</c> so the classic thread column reads as it does on
    /// the JVM.
    /// </summary>
    public static DemoRun Create(string example, DemoOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Thread.CurrentThread.Name ??= "main";
        var format = options.Classic ? LogFormat.Classic : LogFormat.Bare;
        return new DemoRun(new ConsoleLoggerFactory(Console.Out, format), example);
    }

    /// <summary>The capture configuration shared by every proxy in the run.</summary>
    public NarrativeTraceConfig Config { get; }

    /// <summary>
    /// The one context every traced service shares, so calls between them
    /// nest into a single trace; its events also feed the live stream.
    /// </summary>
    public SyncNarrativeContext Context { get; }

    /// <summary>
    /// Opens a scenario: closes the previous one, resets the context, opens
    /// the next <c>traceId</c> scope, and prints the <c>=== title ===</c> header.
    /// </summary>
    public void BeginScenario(string title)
    {
        _scenarioScope?.Dispose();
        Context.Reset();
        var traceId = string.Create(CultureInfo.InvariantCulture, $"trace-{++_scenarioCount:D3}");
        _scenarioScope = _logger.BeginScope(new Dictionary<string, object> { ["traceId"] = traceId });
        _scenarioTitle = title;
        Header(_logger, Sections.Title(title), null);
    }

    /// <summary>Prints the <c>--- Trace tree ---</c> section: <see cref="IndentedTextRenderer"/>.</summary>
    /// <remarks>
    /// Also the demo launcher's translated-capture point: every scenario that
    /// renders a tree hands the same tree to <see cref="DemoTraces"/>, which
    /// writes nothing unless <c>./demo.sh --lang</c> asked for it. One hook
    /// here rather than a call in each example keeps demo scaffolding out of
    /// the example sources.
    /// </remarks>
    public void TraceTree(TraceTree trace)
    {
        Section(Sections.TraceTree, IndentedTextRenderer.Render(trace));
        DemoTraces.Capture(_scenarioTitle, trace);
    }

    /// <summary>Prints the <c>--- Prose ---</c> section: <see cref="ProseRenderer"/>.</summary>
    public void Prose(TraceTree trace)
    {
        Section(Sections.Prose, ProseRenderer.Render(trace));
    }

    /// <summary>Prints the <c>--- Mermaid ---</c> section: <see cref="MermaidSequenceRenderer"/>.</summary>
    public void Mermaid(TraceTree trace)
    {
        Section(Sections.Mermaid, MermaidSequenceRenderer.Render(trace));
    }

    /// <summary>Prints the <c>--- PlantUML ---</c> section: <see cref="PlantUmlSequenceRenderer"/>.</summary>
    public void PlantUml(TraceTree trace)
    {
        Section(Sections.PlantUml, PlantUmlSequenceRenderer.Render(trace));
    }

    /// <summary>Prints one line of the example's own prose.</summary>
    public void Info(string text)
    {
        Line(_logger, text, null);
    }

    /// <summary>Prints a section marker followed by its body, in the Java examples' layout.</summary>
    public void Section(string marker, string body)
    {
        Marker(_logger, marker, null);
        Body(_logger, body, null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scenarioScope?.Dispose();
        _pipeline.Dispose();
    }
}
