// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using Xunit;

namespace NarrativeTrace.Logging.Tests;

public class TraceLogExporterTests
{
    [Fact]
    public void Exports_trace_as_log_events()
    {
        var logger = new FakeLogger();
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "OrderService", "placeOrder",
                    [new ParameterCapture(
                        "orderId", "42", false)]),
                new Returned("ok"), [], 1000),
        ]);

        TraceLogExporter.ExportToLogger(tree, logger);

        Assert.NotEmpty(logger.Entries);
        Assert.Contains(
            logger.Entries,
            e => e.Contains("placeOrder"));
    }

    // TraceNode.Children is a type, not a guarantee of acyclicity - a hand-built or replayed tree
    // can hold a genuine reference cycle. ExportToLogger must terminate rather than recurse the
    // call stack forever — the same unbounded-recursion defect fixed in the java flagship.
    [Fact]
    public void ExportToLogger_does_not_hang_on_a_cyclic_tree()
    {
        var logger = new FakeLogger();
        var aChildren = new List<TraceNode>();
        var bChildren = new List<TraceNode>();
        var a = new TraceNode(
            new MethodSignature("Svc", "A", []), new Returned(null), aChildren.AsReadOnly(), 0);
        var b = new TraceNode(
            new MethodSignature("Svc", "B", []), new Returned(null), bChildren.AsReadOnly(), 0);
        aChildren.Add(b);
        bChildren.Add(a);

        TraceLogExporter.ExportToLogger(new TraceTree([a]), logger);

        Assert.Contains(logger.Entries, e => e.Contains(TreeWalk.CycleMarker));
    }

    [Fact]
    public void Exports_errors_with_exception_type()
    {
        var logger = new FakeLogger();
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc", "fail", []),
                new Threw(
                    new InvalidOperationException()),
                [], 500),
        ]);

        TraceLogExporter.ExportToLogger(tree, logger);

        Assert.Contains(
            logger.Entries,
            e => e.Contains("InvalidOperationException"));
    }

    [Fact]
    public void Exports_redacted_parameters()
    {
        var logger = new FakeLogger();
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Auth", "login",
                    [new ParameterCapture(
                        "password", "secret", true)]),
                new Returned(null), [], 100),
        ]);

        TraceLogExporter.ExportToLogger(tree, logger);

        Assert.Contains(
            logger.Entries,
            e => e.Contains("[REDACTED]"));
        Assert.DoesNotContain(
            logger.Entries,
            e => e.Contains("secret"));
    }

    [Fact]
    public void Incomplete_outcome_formats_as_incomplete()
    {
        var logger = new FakeLogger();
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Incomplete(), [], 0),
        ]);

        TraceLogExporter.ExportToLogger(tree, logger);

        Assert.Contains(
            logger.Entries,
            e => e.Contains("incomplete"));
    }

    private sealed class FakeLogger : ILogger
    {
        public List<string> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(
            TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(formatter(state, exception));
        }
    }
}
