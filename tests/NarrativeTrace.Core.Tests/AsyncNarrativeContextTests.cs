// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class AsyncNarrativeContextTests
{
    [Fact]
    public void Run_creates_isolated_context()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "run", []);
            ctx.ExitMethodWithReturn(null);
        });

        var tree = ctx.CaptureTrace();
        Assert.Single(tree.Roots);
    }

    [Fact]
    public void Throws_outside_run_scope()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        Assert.Throws<InvalidOperationException>(
            () => ctx.EnterMethod("Svc", "run", []));
    }

    [Fact]
    public async Task Async_preserves_context()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("Svc", "start", []);
            await Task.Delay(1);
            ctx.ExitMethodWithReturn(null);
        });

        var tree = ctx.CaptureTrace();
        Assert.Single(tree.Roots);
    }

    [Fact]
    public async Task Parallel_runs_are_isolated()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());
        var counts = new int[2];

        var t1 = Task.Run(() => ctx.Run(() =>
        {
            ctx.EnterMethod("A", "one", []);
            ctx.ExitMethodWithReturn(null);
            counts[0] = ctx.CaptureTrace().Roots.Count;
        }));

        var t2 = Task.Run(() => ctx.Run(() =>
        {
            ctx.EnterMethod("B", "two", []);
            ctx.ExitMethodWithReturn(null);
            ctx.EnterMethod("B", "three", []);
            ctx.ExitMethodWithReturn(null);
            counts[1] = ctx.CaptureTrace().Roots.Count;
        }));

        await Task.WhenAll(t1, t2);

        Assert.Equal(1, counts[0]);
        Assert.Equal(2, counts[1]);
    }

    [Fact]
    public void Run_T_returns_value()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var result = ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "calc", []);
            ctx.ExitMethodWithReturn("42");
            return 42;
        });

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_T_returns_value()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var result = await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("Svc", "calc", []);
            await Task.Delay(1);
            ctx.ExitMethodWithReturn("42");
            return 42;
        });

        Assert.Equal(42, result);
    }

    [Fact]
    public void Reset_clears_last_completed()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "run", []);
            ctx.ExitMethodWithReturn(null);
        });
        ctx.Reset();

        var tree = ctx.CaptureTrace();
        Assert.Empty(tree.Roots);
    }

    [Fact]
    public void EnterMethod_returns_handle_from_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var h0 = ctx.EnterMethod("Svc", "A", []);
            ctx.ExitMethodWithReturn(null, h0);
            var h1 = ctx.EnterMethod("Svc", "B", []);
            ctx.ExitMethodWithReturn(null, h1);

            Assert.False(h0.IsEmpty);
            Assert.False(h1.IsEmpty);
            Assert.NotEqual(h0, h1);
        });
    }

    [Fact]
    public void DetachFrame_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var h0 = ctx.EnterMethod("Svc", "Async", []);
            ctx.DetachFrame(h0);
            var h1 = ctx.EnterMethod("Svc", "Next", []);
            ctx.ExitMethodWithReturn(null, h1);
            ctx.ExitMethodWithReturn(null, h0);

            var trace = ctx.CaptureTrace();
            Assert.Equal(2, trace.Roots.Count);
        });
    }

    [Fact]
    public void Exit_with_handle_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var h0 = ctx.EnterMethod("Svc", "First", []);
            var h1 = ctx.EnterMethod("Svc", "Second", []);
            ctx.ExitMethodWithReturn("s", h1);
            ctx.ExitMethodWithReturn("f", h0);

            var trace = ctx.CaptureTrace();
            Assert.Single(trace.Roots);
            Assert.Single(trace.Roots[0].Children);
        });
    }

    [Fact]
    public void ParentOf_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var h0 = ctx.EnterMethod("Outer", "Run", []);
            var h1 = ctx.EnterMethod("Inner", "Do", []);

            Assert.Equal(h0, ctx.ParentOf(h1));

            ctx.ExitMethodWithReturn(null, h1);
            ctx.ExitMethodWithReturn(null, h0);
        });
    }

    [Fact]
    public void RunScoped_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var h0 = ctx.EnterMethod("A", "Run", []);
            var h1 = ctx.RunScoped(h0, () =>
                ctx.EnterMethod("B", "Do", []));

            Assert.Equal(h0, ctx.ParentOf(h1));

            ctx.ExitMethodWithReturn(null, h1);
            ctx.ExitMethodWithReturn(null, h0);
        });
    }

    [Fact]
    public void GraftChild_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var h0 = ctx.EnterMethod(
                "Parent", "Run", []);
            var grafted = new TraceNode(
                new MethodSignature("Grafted", "Do", []),
                new Returned(null), [], 100);
            ctx.GraftChild(grafted);
            ctx.ExitMethodWithReturn(null, h0);

            var trace = ctx.CaptureTrace();
            Assert.Single(trace.Roots[0].Children);
        });
    }

    [Fact]
    public void CaptureTrace_returns_correct_trace_after_run_completes()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Work", []);
            ctx.ExitMethodWithReturn("done");
        });

        // After Run completes, _lastCompleted should
        // be set and _current.Value should be null
        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Work",
            trace.Roots[0].Signature.MethodName);
        var returned = Assert.IsType<Returned>(
            trace.Roots[0].Outcome);
        Assert.Equal("done", returned.RenderedValue);
    }

    [Fact]
    public void CaptureTrace_returns_correct_trace_after_run_T_completes()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var result = ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Calc", []);
            ctx.ExitMethodWithReturn("99");
            return 99;
        });

        Assert.Equal(99, result);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Calc",
            trace.Roots[0].Signature.MethodName);
    }

    [Fact]
    public async Task CaptureTrace_returns_correct_trace_after_run_async_completes()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("Svc", "AsyncWork", []);
            await Task.Yield();
            ctx.ExitMethodWithReturn("async-done");
        });

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("AsyncWork",
            trace.Roots[0].Signature.MethodName);
        var returned = Assert.IsType<Returned>(
            trace.Roots[0].Outcome);
        Assert.Equal("async-done",
            returned.RenderedValue);
    }

    [Fact]
    public async Task CaptureTrace_returns_correct_trace_after_run_async_T_completes()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var result = await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("Svc", "AsyncCalc", []);
            await Task.Yield();
            ctx.ExitMethodWithReturn("77");
            return 77;
        });

        Assert.Equal(77, result);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("AsyncCalc",
            trace.Roots[0].Signature.MethodName);
    }

    [Fact]
    public async Task Async_methods_complete_normally_with_configure_await()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        // RunAsync uses ConfigureAwait(false).
        // Verify the async method completes and
        // the context captures correctly.
        await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("Svc", "Step1", []);
            await Task.Delay(1);
            ctx.ExitMethodWithReturn("s1");
            ctx.EnterMethod("Svc", "Step2", []);
            await Task.Delay(1);
            ctx.ExitMethodWithReturn("s2");
        });

        var trace = ctx.CaptureTrace();
        Assert.Equal(2, trace.Roots.Count);
        Assert.Equal("Step1",
            trace.Roots[0].Signature.MethodName);
        Assert.Equal("Step2",
            trace.Roots[1].Signature.MethodName);
    }

    [Fact]
    public async Task Async_T_methods_complete_normally_with_configure_await()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var result = await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("Svc", "Compute", []);
            await Task.Delay(1);
            ctx.ExitMethodWithReturn("result");
            return "hello";
        });

        Assert.Equal("hello", result);
        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
    }

    [Fact]
    public void Reset_clears_active_context()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Work", []);
            ctx.ExitMethodWithReturn(null);
        });

        // Verify trace is present before reset
        Assert.Single(ctx.CaptureTrace().Roots);

        ctx.Reset();

        // After reset: _lastCompleted is null and
        // _current.Value?.Reset() was called
        var trace = ctx.CaptureTrace();
        Assert.Empty(trace.Roots);
    }

    [Fact]
    public void Level_gates_activity_and_parameter_capture()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Narrative));

        Assert.True(ctx.IsActive);
        Assert.False(ctx.CapturesParameterValues);
    }

    [Fact]
    public void Detail_level_captures_parameter_values()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Detail));

        Assert.True(ctx.CapturesParameterValues);
    }

    [Fact]
    public void Off_level_is_not_active()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Off));

        Assert.False(ctx.IsActive);
    }

    [Fact]
    public void EnsureTraceId_is_stable_within_run_and_after()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());
        TraceId? inside = null;

        ctx.Run(() =>
        {
            inside = ctx.EnsureTraceId();
            Assert.Equal(inside, ctx.EnsureTraceId());
            Assert.Equal(inside, ctx.CurrentTraceId);
            ctx.EnterMethod("Svc", "Run", []);
            ctx.ExitMethodWithReturn(null);
        });

        Assert.False(inside!.Value.IsEmpty);
        Assert.Equal(inside, ctx.CurrentTraceId);
    }

    [Fact]
    public void EnsureTraceId_throws_outside_run_scope()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        Assert.Throws<InvalidOperationException>(
            () => ctx.EnsureTraceId());
    }

    [Fact]
    public void Story_and_chapter_ids_derive_from_first_root_enter()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Checkout", "PlaceOrder", []);
            Assert.Equal("Checkout.PlaceOrder", ctx.StoryId);
            Assert.Equal("Checkout.PlaceOrder", ctx.ChapterId);
            ctx.ExitMethodWithReturn(null);
        });

        Assert.Equal("Checkout.PlaceOrder", ctx.StoryId);
        Assert.Equal("Checkout.PlaceOrder", ctx.ChapterId);
    }

    [Fact]
    public void Structured_return_overload_carries_structured_value()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Calc", []);
            ctx.ExitMethodWithReturn(
                "42", new RenderedValue.LongVal(42));
        });

        var returned = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Equal("42", returned.RenderedValue);
        Assert.Equal(
            new RenderedValue.LongVal(42), returned.StructuredValue);
    }

    [Fact]
    public void Exception_exit_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Fail", []);
            ctx.ExitMethodWithException(
                new InvalidOperationException("boom"));
        });

        var threw = Assert.IsType<Threw>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Equal("boom", threw.Error!.Message);
    }

    [Fact]
    public void Exception_exit_with_error_context_delegates_to_inner()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Fail", []);
            ctx.ExitMethodWithException(
                new InvalidOperationException("boom"),
                "Payment declined");
        });

        var root = ctx.CaptureTrace().Roots[0];
        Assert.IsType<Threw>(root.Outcome);
        Assert.Equal(
            "Payment declined", root.Signature.ErrorContext);
    }

    [Fact]
    public void Snapshot_delegates_to_inner_context()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.EnterMethod("Svc", "Run", []);
            var snapshot = ctx.Snapshot();

            Assert.NotNull(snapshot);
            using (var scope = snapshot.Activate())
            {
                Assert.NotNull(scope);
            }

            ctx.ExitMethodWithReturn(null);
        });
    }

    [Fact]
    public void Snapshot_throws_outside_run_scope()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        Assert.Throws<InvalidOperationException>(
            () => ctx.Snapshot());
    }

    [Fact]
    public void Request_and_user_context_stamp_spans()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            ctx.SetRequestContext(
                "GET", new HttpRoute("/api/orders"),
                new ClientIp("203.0.113.5"));
            ctx.SetUserContext(
                new EnduserId("user-42"), new SessionId("sess-9"),
                new TenantId("acme"));
            ctx.EnterMethod("Svc", "Run", []);
            ctx.ExitMethodWithReturn(null);
        });

        var span = ctx.CaptureTrace().Roots[0].SpanContext!;
        Assert.Equal("GET", span.HttpMethod);
        Assert.Equal("/api/orders", span.HttpRoute!.Value);
        Assert.Equal("203.0.113.5", span.ClientIp!.Value);
        Assert.Equal("user-42", span.EnduserId!.Value);
        Assert.Equal("sess-9", span.SessionId!.Value);
        Assert.Equal("acme", span.TenantId!.Value);
    }

    [Fact]
    public void CreateConcurrentChild_inherits_parent_trace_id()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        ctx.Run(() =>
        {
            var parentId = ctx.EnsureTraceId();
            var child = ctx.CreateConcurrentChild();

            child.EnterMethod("Fork", "Work", []);
            child.ExitMethodWithReturn(null);

            Assert.Equal(
                parentId,
                child.CaptureTrace().Roots[0].SpanContext!.TraceId);
        });
    }

    [Fact]
    public void CreateConcurrentChild_throws_outside_run_scope()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        Assert.Throws<InvalidOperationException>(
            () => ctx.CreateConcurrentChild());
    }

    [Fact]
    public void RequireCurrent_throws_with_exact_message()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.EnterMethod("Svc", "Run", []));

        Assert.Equal(
            "No active Run() scope.", ex.Message);
    }

    [Fact]
    public void RequireCurrent_throws_for_exit_return()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.ExitMethodWithReturn("val"));

        Assert.Equal(
            "No active Run() scope.", ex.Message);
    }

    [Fact]
    public void RequireCurrent_throws_for_exit_exception()
    {
        var ctx = new AsyncNarrativeContext(
            new NarrativeTraceConfig());

        var ex = Assert.Throws<InvalidOperationException>(
            () => ctx.ExitMethodWithException(
                new Exception("boom")));

        Assert.Equal(
            "No active Run() scope.", ex.Message);
    }
}
