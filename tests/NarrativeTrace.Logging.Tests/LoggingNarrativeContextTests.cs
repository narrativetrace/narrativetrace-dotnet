// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using NarrativeTrace.Logging;
using Xunit;

namespace NarrativeTrace.Logging.Tests;

public class LoggingNarrativeContextTests
{
    [Fact]
    public void EnterMethod_logs_with_nt_spanId_and_nt_parentSpanId()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        Assert.Single(logger.Entries);
        var entry = logger.Entries[0];
        Assert.Contains("Svc", entry.Message);
        Assert.Contains("Run", entry.Message);
        Assert.True(entry.Scopes.ContainsKey("nt.spanId"));
        Assert.True(entry.Scopes.ContainsKey("nt.parentSpanId"));
    }

    [Fact]
    public void EnterMethod_logs_with_nt_traceId_and_nt_traceName()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        var entry = logger.Entries[0];
        var traceId = Assert.IsType<string>(
            entry.Scopes["nt.traceId"]);
        Assert.Equal(32, traceId.Length);
        Assert.Equal(
            TraceNamer.Name(traceId),
            entry.Scopes["nt.traceName"]);
    }

    [Fact]
    public void EnterMethod_logs_canonical_schema_scope_fields()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        var scopes = logger.Entries[0].Scopes;
        Assert.Equal("entry", scopes["nt.entryType"]);
        Assert.Equal("1.2", scopes["nt.schemaVersion"]);
        Assert.Equal(inner.StoryId, scopes["nt.storyId"]);
        Assert.Equal(inner.ChapterId, scopes["nt.chapterId"]);
    }

    [Fact]
    public void ExitMethodWithReturn_logs_with_nt_spanId()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn("42", handle);

        Assert.Equal(2, logger.Entries.Count);
        var exit = logger.Entries[1];
        Assert.Contains("\u2190", exit.Message);
        Assert.Contains("42", exit.Message);
        Assert.True(exit.Scopes.ContainsKey("nt.spanId"));
    }

    [Fact]
    public void ExitMethodWithReturn_carries_trace_correlation_keys()
    {
        // Same defect as the listener bridge: an exit line with only spanId
        // cannot be correlated to its trace.
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        var enterTraceId = logger.Entries[0].Scopes["nt.traceId"];
        ctx.ExitMethodWithReturn("42", handle);

        var exit = logger.Entries[1].Scopes;
        Assert.Equal(enterTraceId, exit["nt.traceId"]);
        Assert.True(exit.ContainsKey("nt.parentSpanId"));
        Assert.True(exit.ContainsKey("nt.traceName"));
    }

    [Fact]
    public void ExitMethodWithException_logs_with_nt_spanId()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("boom"),
            handle);

        Assert.Equal(2, logger.Entries.Count);
        var exit = logger.Entries[1];
        Assert.StartsWith("!! ", exit.Message);
        Assert.Contains(
            "InvalidOperationException", exit.Message);
        Assert.True(exit.Scopes.ContainsKey("nt.spanId"));
    }

    [Fact]
    public void EnterMethod_defaults_to_Trace_level()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        Assert.Equal(LogLevel.Trace, logger.Entries[0].Level);
    }

    [Fact]
    public void ExitMethodWithReturn_defaults_to_Trace_level()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn("42", handle);

        Assert.Equal(LogLevel.Trace, logger.Entries[1].Level);
    }

    [Fact]
    public void Custom_options_route_events_to_configured_levels()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var options = new TraceLoggingOptions
        {
            EnterLevel = LogLevel.Debug,
            ReturnLevel = LogLevel.Information,
            ExceptionLevel = LogLevel.Error,
        };
        var ctx = new LoggingNarrativeContext(
            inner, logger, options);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("boom"), handle);

        Assert.Equal(LogLevel.Debug, logger.Entries[0].Level);
        Assert.Equal(LogLevel.Error, logger.Entries[1].Level);
    }

    [Fact]
    public void ExitMethodWithException_logs_exception_message()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("kaboom"), handle);

        Assert.Contains("kaboom", logger.Entries[1].Message);
    }

    [Fact]
    public void ExitMethodWithException_logs_error_context_when_present()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("boom"),
            "while charging card", handle);

        Assert.Contains(
            "while charging card", logger.Entries[1].Message);
    }

    [Fact]
    public void ExitMethodWithException_sanitizes_control_chars_in_message()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        var handle = ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithException(
            new InvalidOperationException("line1\nFAKE"),
            handle);

        var message = logger.Entries[1].Message;
        Assert.DoesNotContain("\n", message);
        Assert.Contains("line1\\nFAKE", message);
    }

    [Fact]
    public void EnterMethod_scope_carries_incrementing_nt_depth()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        ctx.EnterMethod("A", "Run", []);
        ctx.EnterMethod("B", "Do", []);

        Assert.Equal(1, logger.Entries[0].Scopes["nt.depth"]);
        Assert.Equal(2, logger.Entries[1].Scopes["nt.depth"]);
    }

    [Fact]
    public void ExitMethod_scope_carries_decrementing_nt_depth()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        var h0 = ctx.EnterMethod("A", "Run", []);
        var h1 = ctx.EnterMethod("B", "Do", []);
        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);

        Assert.Equal(1, logger.Entries[2].Scopes["nt.depth"]);
        Assert.Equal(0, logger.Entries[3].Scopes["nt.depth"]);
    }

    [Fact]
    public void EnterMethod_scope_carries_service_identity_when_present()
    {
        var logger = new CapturingLogger();
        var identity = new ServiceIdentity(
            "checkout", "1.2.3", "prod");
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig(
                serviceIdentity: identity));
        var ctx = new LoggingNarrativeContext(
            inner, logger, TraceLoggingOptions.Default, identity);

        ctx.EnterMethod("Svc", "Run", []);

        var scopes = logger.Entries[0].Scopes;
        Assert.Equal("checkout", scopes["service.name"]);
        Assert.Equal("1.2.3", scopes["service.version"]);
        Assert.Equal("prod", scopes["service.environment"]);
    }

    [Fact]
    public void EnterMethod_omits_service_keys_when_identity_absent()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        var scopes = logger.Entries[0].Scopes;
        Assert.False(scopes.ContainsKey("service.name"));
    }

    [Fact]
    public void EnterMethod_scope_carries_nt_class_and_nt_method()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        var scopes = logger.Entries[0].Scopes;
        Assert.Equal("Svc", scopes["nt.class"]);
        Assert.Equal("Run", scopes["nt.method"]);
    }

    [Fact]
    public void ParentOf_delegates_to_inner()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var h0 = ctx.EnterMethod("Outer", "Run", []);
        var h1 = ctx.EnterMethod("Inner", "Do", []);

        Assert.Equal(h0, ctx.ParentOf(h1));

        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);
    }

    [Fact]
    public void RunScoped_delegates_to_inner()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var h0 = ctx.EnterMethod("A", "Run", []);
        var h1 = ctx.RunScoped(h0, () =>
            ctx.EnterMethod("B", "Do", []));

        Assert.Equal(h0, ctx.ParentOf(h1));

        ctx.ExitMethodWithReturn(null, h1);
        ctx.ExitMethodWithReturn(null, h0);
    }

    [Fact]
    public void GraftChild_delegates_to_inner_and_logs()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var grafted = new TraceNode(
            new MethodSignature("Grafted", "Do", []),
            new Returned(null), [], 100);
        ctx.GraftChild(grafted);
        ctx.ExitMethodWithReturn(null, h0);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots[0].Children);
    }

    [Fact]
    public void CaptureTrace_delegates_to_inner()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Run",
            trace.Roots[0].Signature.MethodName);
    }

    [Fact]
    public void IsActive_reflects_inner()
    {
        var logger = new CapturingLogger();
        var active = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var inactive = NoopContext.Instance;

        var ctxActive = new LoggingNarrativeContext(
            active, logger);
        var ctxInactive = new LoggingNarrativeContext(
            inactive, logger);

        Assert.True(ctxActive.IsActive);
        Assert.False(ctxInactive.IsActive);
    }

    [Fact]
    public void Root_entry_has_empty_nt_parentSpanId()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        ctx.EnterMethod("Svc", "Run", []);

        var entry = logger.Entries[0];
        Assert.Equal("", entry.Scopes["nt.parentSpanId"]);
    }

    [Fact]
    public void GraftChild_logs_concurrency_fields_when_present()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var info = new ConcurrencyInfo(
            "fork-1", "Svc.Do", 4, null, true,
            ConcurrencyKind.ForkJoin);
        var grafted = new TraceNode(
            new MethodSignature("Svc", "Do", []),
            new Returned(null), [], 100,
            Concurrency: info);
        ctx.GraftChild(grafted);
        ctx.ExitMethodWithReturn(null, h0);

        var graftEntry = logger.Entries.First(
            e => e.Message.Contains("Svc.Do"));
        Assert.Equal(
            "fork-1", graftEntry.Scopes["nt.groupId"]);
        Assert.Equal(
            "ForkJoin", graftEntry.Scopes["nt.kind"]);
    }

    [Fact]
    public void LoggingContext_logs_fork_created_with_groupId()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var group = ForkJoinGroup.Create(ctx);

        var entry = logger.Entries.First(
            e => e.Message.Contains("fork"));
        Assert.Equal(
            group.GroupId,
            entry.Scopes["nt.groupId"]);
    }

    [Fact]
    public async Task LoggingContext_logs_join_with_wallTime_and_count()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var group = ForkJoinGroup.Create(ctx);
        group.Fork(_ => 1);
        group.Fork(_ => 2);
        await group.JoinAsync();

        var entry = logger.Entries.First(
            e => e.Message.Contains("fork joined"));
        Assert.Equal(
            group.GroupId,
            entry.Scopes["nt.groupId"]);
        Assert.Equal(2, entry.Scopes["nt.memberCount"]);
    }

    [Fact]
    public void LoggingContext_logs_fire_and_forget_launch()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var group = FireAndForgetGroup.Create(ctx);
        group.Launch(_ => { });

        var entry = logger.Entries.First(
            e => e.Message.Contains("fire-and-forget"));
        Assert.Equal(
            group.GroupId,
            entry.Scopes["nt.groupId"]);
    }

    [Fact]
    public void BeginScope_carries_groupId_during_graft()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var h0 = ctx.EnterMethod("P", "Run", []);
        var info = new ConcurrencyInfo(
            "fork-99", "X.Do", 1, null, true,
            ConcurrencyKind.ForkJoin);
        var node = new TraceNode(
            new MethodSignature("X", "Do", []),
            new Returned(null), [], 100,
            Concurrency: info);
        ctx.GraftChild(node);
        ctx.ExitMethodWithReturn(null, h0);

        var graft = logger.Entries.First(
            e => e.Message.Contains("X.Do"));
        Assert.Equal(
            "fork-99", graft.Scopes["nt.groupId"]);
    }

    [Fact]
    public void GraftChild_without_concurrency_logs_basic_info()
    {
        var logger = new CapturingLogger();
        var inner = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var ctx = new LoggingNarrativeContext(
            inner, logger);

        var h0 = ctx.EnterMethod("Parent", "Run", []);
        var grafted = new TraceNode(
            new MethodSignature("Svc", "Do", []),
            new Returned(null), [], 100);
        ctx.GraftChild(grafted);
        ctx.ExitMethodWithReturn(null, h0);

        var graftEntry = logger.Entries.First(
            e => e.Message.Contains("Svc.Do"));
        Assert.False(
            graftEntry.Scopes.ContainsKey("nt.groupId"));
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<LogEntry> Entries { get; } = [];

        private Dictionary<string, object>
            _currentScope = new();

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
                new Dictionary<string, object>(
                    _currentScope),
                logLevel));
        }

        private sealed class ScopeDisposable : IDisposable
        {
            private readonly CapturingLogger _logger;
            private readonly Dictionary<string, object>
                _previous;

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
