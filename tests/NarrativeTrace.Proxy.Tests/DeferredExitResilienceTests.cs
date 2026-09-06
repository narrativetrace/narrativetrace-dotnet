// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

// Regression tests for the deferred-exit completion guarantee (mirrors a java
// flagship fix): trace-recording failure on the async exit path must never
// mask the business result/exception the caller awaits.
public class DeferredExitResilienceTests
{
    [Fact]
    public async Task Task_result_survives_throwing_exit_recording()
    {
        var proxy = NarrativeTraceProxy.Create<IAsyncWork>(
            new AsyncWork(), new ExitFailingContext());

        var result = await proxy.ComputeAsync(21);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Void_task_completes_despite_throwing_exit_recording()
    {
        var context = new ExitFailingContext();
        var proxy = NarrativeTraceProxy.Create<IAsyncWork>(
            new AsyncWork(), context);

        var running = proxy.RunAsync();
        await running;

        Assert.Equal(TaskStatus.RanToCompletion, running.Status);
        Assert.Equal(1, context.ExitAttempts);
    }

    [Fact]
    public async Task Value_task_result_survives_throwing_exit_recording()
    {
        var proxy = NarrativeTraceProxy.Create<IAsyncWork>(
            new AsyncWork(), new ExitFailingContext());

        var result = await proxy.ComputeValueAsync(21);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task Void_value_task_completes_despite_throwing_exit_recording()
    {
        var context = new ExitFailingContext();
        var proxy = NarrativeTraceProxy.Create<IAsyncWork>(
            new AsyncWork(), context);

        var running = proxy.RunValueAsync().AsTask();
        await running;

        Assert.Equal(TaskStatus.RanToCompletion, running.Status);
        Assert.Equal(1, context.ExitAttempts);
    }

    [Fact]
    public async Task Business_failure_propagates_through_throwing_exit_recording()
    {
        var proxy = NarrativeTraceProxy.Create<IAsyncWork>(
            new FailingAsyncWork(), new ExitFailingContext());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.ComputeAsync(1));

        Assert.Equal("business failure", thrown.Message);
    }

    [Fact]
    public async Task Value_task_business_failure_propagates_through_throwing_exit()
    {
        var proxy = NarrativeTraceProxy.Create<IAsyncWork>(
            new FailingAsyncWork(), new ExitFailingContext());

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await proxy.ComputeValueAsync(1));

        Assert.Equal("business failure", thrown.Message);
    }

    public interface IAsyncWork
    {
        Task RunAsync();
        Task<int> ComputeAsync(int x);
        ValueTask RunValueAsync();
        ValueTask<int> ComputeValueAsync(int x);
    }

    private sealed class AsyncWork : IAsyncWork
    {
        public async Task RunAsync() => await Task.Yield();

        public async Task<int> ComputeAsync(int x)
        {
            await Task.Yield();
            return x * 2;
        }

        public async ValueTask RunValueAsync() => await Task.Yield();

        public async ValueTask<int> ComputeValueAsync(int x)
        {
            await Task.Yield();
            return x * 2;
        }
    }

    private sealed class FailingAsyncWork : IAsyncWork
    {
        public async Task RunAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException("business failure");
        }

        public async Task<int> ComputeAsync(int x)
        {
            await Task.Yield();
            throw new InvalidOperationException("business failure");
        }

        public async ValueTask RunValueAsync()
        {
            await Task.Yield();
            throw new InvalidOperationException("business failure");
        }

        public async ValueTask<int> ComputeValueAsync(int x)
        {
            await Task.Yield();
            throw new InvalidOperationException("business failure");
        }
    }

    // Delegates real behaviour to a live context but always throws when the
    // proxy tries to record the deferred exit.
    private sealed class ExitFailingContext : INarrativeContext
    {
        private readonly SyncNarrativeContext _inner =
            new(new NarrativeTraceConfig());

        public bool IsActive => _inner.IsActive;

        public bool CapturesParameterValues =>
            _inner.CapturesParameterValues;

        public TraceId? CurrentTraceId => _inner.CurrentTraceId;

        public TraceId EnsureTraceId() => _inner.EnsureTraceId();

        public string? StoryId => _inner.StoryId;

        public string? ChapterId => _inner.ChapterId;

        // Lets the void-returning tests prove the exit WAS recorded and the
        // failure swallowed, rather than the exit never being attempted.
        public int ExitAttempts { get; private set; }

        public SpanId EnterMethod(
            string className, string methodName,
            IReadOnlyList<ParameterCapture> parameters,
            MethodOptions? options = null) =>
            _inner.EnterMethod(className, methodName, parameters, options);

        public void ExitMethodWithReturn(
            string? renderedValue, SpanId? handle = null) =>
            throw RecordedFailure();

        public void ExitMethodWithReturn(
            string? renderedValue,
            RenderedValue? structuredValue,
            SpanId? handle = null) =>
            throw RecordedFailure();

        public void ExitMethodWithException(
            Exception? exception, SpanId? handle = null) =>
            throw RecordedFailure();

        public void ExitMethodWithException(
            Exception? exception, string? errorContext,
            SpanId? handle = null) =>
            throw RecordedFailure();

        private InvalidOperationException RecordedFailure()
        {
            ExitAttempts++;
            return new InvalidOperationException("exit recording failed");
        }

        public void DetachFrame(SpanId handle) => _inner.DetachFrame(handle);

        public TraceTree CaptureTrace() => _inner.CaptureTrace();

        public void Reset() => _inner.Reset();

        public IContextSnapshot Snapshot() => _inner.Snapshot();

        public SpanId? ParentOf(SpanId handle) => _inner.ParentOf(handle);

        public T RunScoped<T>(SpanId handle, Func<T> fn) =>
            _inner.RunScoped(handle, fn);

        public void GraftChild(TraceNode node) => _inner.GraftChild(node);

        public void SetRequestContext(
            string? httpMethod, HttpRoute? httpRoute, ClientIp? clientIp) =>
            _inner.SetRequestContext(httpMethod, httpRoute, clientIp);

        public void SetUserContext(
            EnduserId? enduserId, SessionId? sessionId, TenantId? tenantId) =>
            _inner.SetUserContext(enduserId, sessionId, tenantId);
    }
}
