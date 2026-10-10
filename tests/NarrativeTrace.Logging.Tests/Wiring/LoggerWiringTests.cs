// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Example.Orders;
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Logging.Tests.Wiring;

/// <summary>
/// Proves the framework table's logger snippet (<see cref="LoggerWiring"/>): a captured call reaches
/// the application's logger, under the <c>NarrativeTrace</c> category.
/// </summary>
public sealed class LoggerWiringTests
{
    private sealed class RecordingLoggerFactory : ILoggerFactory
    {
        public List<(string Category, string Message)> Entries { get; } = [];

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);

        public void AddProvider(ILoggerProvider provider)
        {
            // Not used: every logger this factory creates records into Entries.
        }

        public void Dispose()
        {
            // Nothing to release.
        }
    }

    private sealed class RecordingLogger(string category, List<(string, string)> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => entries.Add((category, formatter(state, exception)));
    }

    [Fact]
    public void A_captured_call_reaches_the_application_logger_under_the_narrativetrace_category()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
        context.EnterMethod("IOrderService", "PlaceOrder", []);
        context.ExitMethodWithReturn("confirmed");
        using var loggerFactory = new RecordingLoggerFactory();

        LoggerWiring.Export(context, loggerFactory);

        Assert.Contains(
            loggerFactory.Entries,
            entry => entry.Category == "NarrativeTrace" && entry.Message.Contains("PlaceOrder", StringComparison.Ordinal));
    }
}
