// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Checkout.Tests;

/// <summary>
/// The logger configuration the correlation recipes run under: one line per message —
/// <c>LEVEL [category] [trace id] - message</c> — with the trace id read from the <c>nt.traceId</c>
/// field NarrativeTrace puts in each log scope. The trace id is the thing those pages follow; the
/// built-in console formatter would print it only as part of a scope dump. It writes the
/// <c>NarrativeTrace</c> categories only, so a hosted service's request logging stays out of the page. Deliberately no
/// timestamp: the embedded block is a real run's bytes, and a clock differs on every run.
/// </summary>
internal sealed class CorrelationLoggerProvider(TextWriter output) : ILoggerProvider, ISupportExternalScope
{
    private const string OwnCategories = "NarrativeTrace";

    private readonly object _writing = new();
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    // Only the narrative's own categories: the framework's request logging is not what these pages follow.
    public ILogger CreateLogger(string categoryName) =>
        categoryName.StartsWith(OwnCategories, StringComparison.Ordinal)
            ? new CorrelationLogger(categoryName, output, _writing, () => _scopes)
            : NullLogger.Instance;

    public void Dispose()
    {
    }

    private sealed class CorrelationLogger(string category, TextWriter output, object writing, Func<IExternalScopeProvider> scopes) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => scopes().Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var line = $"{logLevel.ToString().ToUpperInvariant(),-5} [{category}] [{TraceIdOfScope()}] - {formatter(state, exception)}";
            lock (writing)
            {
                output.WriteLine(line);
            }
        }

        private string TraceIdOfScope()
        {
            var traceId = "";
            scopes().ForEachScope(
                (scope, _) =>
                {
                    if (scope is IEnumerable<KeyValuePair<string, object>> fields)
                    {
                        foreach (var (key, value) in fields)
                        {
                            traceId = key == "nt.traceId" ? value.ToString() ?? "" : traceId;
                        }
                    }
                },
                0);
            return traceId;
        }
    }
}
