// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using Xunit;

namespace NarrativeTrace.Logging.Tests;

public class LoggingTraceEventListenerTests
{
    private const string Span1 = "00000000000000a1";
    private const string Span2 = "00000000000000b2";

    private static SpanContext SpanCtx(
        string spanId, string? parentSpanId = null,
        ServiceIdentity? identity = null)
    {
        return SpanContext.Create(
            new TraceId(new string('a', 32)),
            new SpanId(spanId),
            parentSpanId is null ? null : new SpanId(parentSpanId),
            identity);
    }

    private static EnterEvent Enter(
        SpanContext sc, string className, string methodName)
    {
        return new EnterEvent(
            sc, 0,
            new MethodSignature(className, methodName, []));
    }

    [Fact]
    public void OnEvent_enter_logs_class_method_and_span_scope()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(
            Enter(SpanCtx(Span1), "Svc", "Run"));

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("Svc", entry.Message);
        Assert.Contains("Run", entry.Message);
        Assert.Equal("Svc", entry.Scopes["nt.class"]);
        Assert.Equal("Run", entry.Scopes["nt.method"]);
        Assert.Equal(Span1, entry.Scopes["nt.spanId"]);
    }

    [Fact]
    public void OnEvent_enter_carries_service_identity_from_span_context()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);
        var identity = new ServiceIdentity("checkout", "1.2.3", "prod");

        listener.OnEvent(
            Enter(SpanCtx(Span1, identity: identity), "Svc", "Run"));

        var scopes = logger.Entries[0].Scopes;
        Assert.Equal("checkout", scopes["service.name"]);
        Assert.Equal("1.2.3", scopes["service.version"]);
        Assert.Equal("prod", scopes["service.environment"]);
    }

    [Fact]
    public void OnEvent_enter_omits_service_keys_when_absent()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(Enter(SpanCtx(Span1), "Svc", "Run"));

        Assert.False(
            logger.Entries[0].Scopes.ContainsKey("service.name"));
    }

    [Fact]
    public void OnEvent_enter_records_parent_span_id()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(
            Enter(SpanCtx(Span2, Span1), "Svc", "Run"));

        Assert.Equal(
            Span1, logger.Entries[0].Scopes["nt.parentSpanId"]);
    }

    [Fact]
    public void OnEvent_depth_increments_on_enter_and_decrements_on_exit()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(Enter(SpanCtx(Span1), "A", "Run"));
        listener.OnEvent(Enter(SpanCtx(Span2, Span1), "B", "Do"));
        listener.OnEvent(new ExitEvent(
            SpanCtx(Span2, Span1), 0, new Returned("1")));
        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1), 0, new Returned("2")));

        Assert.Equal(1, logger.Entries[0].Scopes["nt.depth"]);
        Assert.Equal(2, logger.Entries[1].Scopes["nt.depth"]);
        Assert.Equal(1, logger.Entries[2].Scopes["nt.depth"]);
        Assert.Equal(0, logger.Entries[3].Scopes["nt.depth"]);
    }

    [Fact]
    public void OnEvent_enter_renders_the_parameter_list_like_java()
    {
        // Java: "\u2192 {}.{}({})" with params as "name: value" pairs.
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new EnterEvent(
            SpanCtx(Span1), 0,
            new MethodSignature("OrderService", "PlaceOrder", [
                new ParameterCapture("orderId", "42", false),
                new ParameterCapture("coupon", "SUMMER", true),
            ])));

        Assert.Equal(
            "\u2192 OrderService.PlaceOrder(orderId: 42, coupon: [REDACTED])",
            logger.Entries[0].Message);
    }

    [Fact]
    public void OnEvent_enter_with_no_parameters_renders_empty_parentheses()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(Enter(SpanCtx(Span1), "Svc", "Run"));

        Assert.Equal("\u2192 Svc.Run()", logger.Entries[0].Message);
    }

    [Fact]
    public void OnEvent_exit_carries_trace_correlation_keys()
    {
        // An exit line with only spanId cannot be correlated to its trace by the
        // field aggregators filter on. Java's logExit carries the full span set.
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span2, Span1), 0, new Returned("1")));

        var scopes = logger.Entries[0].Scopes;
        Assert.Equal(new string('a', 32), scopes["nt.traceId"]);
        Assert.Equal(Span2, scopes["nt.spanId"]);
        Assert.Equal(Span1, scopes["nt.parentSpanId"]);
        Assert.True(scopes.ContainsKey("nt.traceName"));
    }

    [Fact]
    public void OnEvent_exit_carries_service_identity_from_span_context()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);
        var identity = new ServiceIdentity("checkout", "1.2.3", "prod");

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1, identity: identity), 0, new Returned("1")));

        var scopes = logger.Entries[0].Scopes;
        Assert.Equal("checkout", scopes["service.name"]);
        Assert.Equal("1.2.3", scopes["service.version"]);
        Assert.Equal("prod", scopes["service.environment"]);
    }

    [Fact]
    public void OnEvent_returned_exit_logs_rendered_value()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1), 0, new Returned("42")));

        Assert.Contains("42", logger.Entries[0].Message);
    }

    [Fact]
    public void OnEvent_void_exit_logs_completed_instead_of_a_null_value()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1), 0, new Returned(null)));

        Assert.Equal("\u2190 completed", logger.Entries[0].Message);
    }

    [Fact]
    public void OnEvent_returning_null_logs_the_rendered_null()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1), 0, new Returned("null")));

        Assert.Equal("\u2190 returned: null", logger.Entries[0].Message);
    }

    [Fact]
    public void OnEvent_threw_exit_logs_sanitized_message_and_context()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1), 0,
            new Threw(new InvalidOperationException("boom\nFAKE")),
            "while charging card"));

        var message = logger.Entries[0].Message;
        Assert.Contains("InvalidOperationException", message);
        Assert.Contains("boom\\nFAKE", message);
        Assert.DoesNotContain("\n", message);
        Assert.Contains("while charging card", message);
    }

    [Fact]
    public void OnEvent_threw_exit_defaults_to_Warning_level()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ExitEvent(
            SpanCtx(Span1), 0,
            new Threw(new InvalidOperationException("boom"))));

        Assert.Equal(LogLevel.Warning, logger.Entries[0].Level);
    }

    [Fact]
    public void OnEvent_enter_defaults_to_Trace_level()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(Enter(SpanCtx(Span1), "Svc", "Run"));

        Assert.Equal(LogLevel.Trace, logger.Entries[0].Level);
    }

    [Fact]
    public void OnEvent_honors_custom_option_levels()
    {
        var logger = new CapturingLogger();
        var options = new TraceLoggingOptions
        {
            EnterLevel = LogLevel.Debug,
        };
        var listener = new LoggingTraceEventListener(logger, options);

        listener.OnEvent(Enter(SpanCtx(Span1), "Svc", "Run"));

        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
    }

    [Fact]
    public void OnEvent_logs_fork_merge_and_fire_and_forget()
    {
        var logger = new CapturingLogger();
        var listener = new LoggingTraceEventListener(logger);

        listener.OnEvent(new ForkCreatedEvent("g1", 0));
        listener.OnEvent(new MergeEvent("g1", 3, 0, 0));
        listener.OnEvent(new FireAndForgetEvent("g2", 0));

        Assert.Equal("g1", logger.Entries[0].Scopes["nt.groupId"]);
        Assert.Contains("fork", logger.Entries[0].Message);
        Assert.Contains("fork joined", logger.Entries[1].Message);
        Assert.Contains("3", logger.Entries[1].Message);
        Assert.Contains(
            "fire-and-forget", logger.Entries[2].Message);
        Assert.Equal("g2", logger.Entries[2].Scopes["nt.groupId"]);
    }

    internal sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        private Dictionary<string, object> _currentScope = new();

        public IDisposable BeginScope<TState>(
            TState state) where TState : notnull
        {
            // Snapshot BEFORE merging: restoring a post-merge copy would leave
            // this scope's keys installed forever, so an exit line would appear
            // to carry keys that only the preceding enter scope set.
            var previous = new Dictionary<string, object>(_currentScope);
            if (state is IEnumerable<
                KeyValuePair<string, object>> kvps)
            {
                foreach (var kvp in kvps)
                {
                    _currentScope[kvp.Key] = kvp.Value;
                }
            }

            return new ScopeDisposable(this, previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId,
            TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(
                formatter(state, exception),
                new Dictionary<string, object>(_currentScope),
                logLevel));
        }

        private sealed class ScopeDisposable : IDisposable
        {
            private readonly CapturingLogger _logger;
            private readonly Dictionary<string, object> _previous;

            public ScopeDisposable(
                CapturingLogger logger,
                Dictionary<string, object> previous)
            {
                _logger = logger;
                _previous = previous;
            }

            public void Dispose()
            {
                _logger._currentScope = _previous;
            }
        }
    }

    internal sealed record LogEntry(
        string Message,
        Dictionary<string, object> Scopes,
        LogLevel Level = LogLevel.Information);
}
