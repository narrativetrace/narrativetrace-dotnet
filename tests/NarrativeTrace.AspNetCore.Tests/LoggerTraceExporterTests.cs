// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.AspNetCore;
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.AspNetCore.Tests;

public class LoggerTraceExporterTests
{
    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => new Noop();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class Noop : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class SingleLoggerFactory : ILoggerFactory
    {
        private readonly ILogger _logger;
        public SingleLoggerFactory(ILogger logger) => _logger = logger;
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName) => _logger;
        public void Dispose() { }
    }

    private sealed class CategoryCapturingFactory : ILoggerFactory
    {
        public string? Category { get; private set; }
        public void AddProvider(ILoggerProvider provider) { }
        public ILogger CreateLogger(string categoryName)
        {
            Category = categoryName;
            return new CapturingLogger();
        }

        public void Dispose() { }
    }

    [Fact]
    public void Uses_default_export_category_when_unconfigured()
    {
        var factory = new CategoryCapturingFactory();

        _ = new LoggerTraceExporter(factory);

        Assert.Equal("NarrativeTrace.Export", factory.Category);
    }

    [Fact]
    public void Configured_logger_name_sets_the_exporter_category()
    {
        var factory = new CategoryCapturingFactory();
        var options = new NarrativeTraceOptions { LoggerName = "My.Export" };

        _ = new LoggerTraceExporter(factory, options);

        Assert.Equal("My.Export", factory.Category);
    }

    [Fact]
    public void Logs_method_route_status_duration_and_json()
    {
        var logger = new CapturingLogger();
        var exporter = new LoggerTraceExporter(
            new SingleLoggerFactory(logger));
        var ctx = new NarrativeTrace.Runtime.SyncNarrativeContext(
            new NarrativeTraceConfig());
        ctx.SetRequestContext(
            "GET", new HttpRoute("/api/orders"), null);
        ctx.EnterMethod("OrderService", "PlaceOrder", []);
        ctx.ExitMethodWithReturn(null);

        exporter.Export(
            ctx.CaptureTrace(), new RequestContext(200, 42));

        var message = Assert.Single(logger.Messages);
        Assert.Contains("GET /api/orders", message);
        Assert.Contains("[200]", message);
        Assert.Contains("42ms", message);
    }

    [Fact]
    public void Empty_trace_is_not_logged()
    {
        var logger = new CapturingLogger();
        var exporter = new LoggerTraceExporter(
            new SingleLoggerFactory(logger));

        exporter.Export(
            new TraceTree([]), new RequestContext(204, 1));

        Assert.Empty(logger.Messages);
    }
}
