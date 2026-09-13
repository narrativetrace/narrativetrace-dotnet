// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>config-shape</c>: <see cref="TraceLogExporter.ExportToLogger"/> replays a captured tree onto
/// an <see cref="ILogger"/>, one <see cref="LogLevel.Information"/> record per node —
/// sixty-seconds.md "Send it to your logger". A tiny hand-written <see cref="ILogger"/> capturing
/// formatted messages stands in for a real provider; no third-party test-logging package needed
/// for a check this small.
/// </summary>
internal static class TraceLogExporterProbe
{
    public interface IGreeter
    {
        string Greet(string name);
    }

    private sealed class Greeter : IGreeter
    {
        public string Greet(string name) => $"hello {name}";
    }

    private sealed class CapturingLogger : ILogger
    {
        public readonly List<string> Messages = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    public static string Observe()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IGreeter>(new Greeter(), context);
        proxy.Greet("world");

        var logger = new CapturingLogger();
        TraceLogExporter.ExportToLogger(context.CaptureTrace(), logger);

        var text = string.Join("\n", logger.Messages);
        return text.Contains("Greet", StringComparison.Ordinal) && text.Contains("hello world", StringComparison.Ordinal)
            ? "true"
            : "false";
    }
}
