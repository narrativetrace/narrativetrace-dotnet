// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

public class NarrativeTraceProxyTests
{
    [Fact]
    public void Proxy_intercepts_method_call_and_records_in_context()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(1, 2);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("ICalculator", trace.Roots[0].Signature.ClassName);
        Assert.Equal("Add", trace.Roots[0].Signature.MethodName);
    }

    [Fact]
    public void Proxy_captures_parameter_names_via_reflection()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(3, 4);

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(2, node.Signature.Parameters.Count);
        Assert.Equal("a", node.Signature.Parameters[0].Name);
        Assert.Equal("b", node.Signature.Parameters[1].Name);
        Assert.Equal("3", node.Signature.Parameters[0].RenderedValue);
        Assert.Equal("4", node.Signature.Parameters[1].RenderedValue);
    }

    [Fact]
    public void Proxy_suppresses_parameter_values_at_narrative_level()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Narrative));
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(3, 4);

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal("a", node.Signature.Parameters[0].Name);
        Assert.Equal("", node.Signature.Parameters[0].RenderedValue);
        Assert.Equal("", node.Signature.Parameters[1].RenderedValue);
    }

    [Fact]
    public void Proxy_renders_return_value()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(2, 3);

        var outcome = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Equal("5", outcome.RenderedValue);
    }

    [Fact]
    public void Proxy_records_exception_and_rethrows()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IService>(
            new FailingService(), ctx);

        Assert.Throws<InvalidOperationException>(
            () => proxy.Fail());

        var outcome = Assert.IsType<Threw>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.IsType<InvalidOperationException>(outcome.Error);
        Assert.Equal("boom", outcome.Error!.Message);
    }

    [Fact]
    public void Proxy_at_errors_level_captures_a_failing_call()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig(TracingLevel.Errors));
        var proxy = NarrativeTraceProxy.Create<IService>(
            new FailingService(), ctx);

        Assert.Throws<InvalidOperationException>(
            () => proxy.Fail());

        var outcome = Assert.IsType<Threw>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.IsType<InvalidOperationException>(outcome.Error);
    }

    [Fact]
    public async Task Proxy_handles_async_task_return()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAsyncService>(
            new AsyncService(), ctx);

        await proxy.RunAsync();

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("RunAsync", trace.Roots[0].Signature.MethodName);
        Assert.IsType<Returned>(trace.Roots[0].Outcome);
    }

    [Fact]
    public async Task Proxy_handles_async_task_with_result()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAsyncService>(
            new AsyncService(), ctx);

        var result = await proxy.ComputeAsync(5);

        Assert.Equal(10, result);
        var outcome = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Equal("10", outcome.RenderedValue);
    }

    [Fact]
    public async Task Proxy_handles_async_exception()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAsyncService>(
            new FailingAsyncService(), ctx);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.RunAsync());

        var outcome = Assert.IsType<Threw>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.IsType<InvalidOperationException>(outcome.Error);
    }

    [Fact]
    public async Task Proxy_handles_value_task_return()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IValueTaskService>(
            new ValueTaskService(), ctx);

        await proxy.RunAsync();

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.IsType<Returned>(trace.Roots[0].Outcome);
    }

    [Fact]
    public async Task Proxy_handles_value_task_with_result()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IValueTaskService>(
            new ValueTaskService(), ctx);

        var result = await proxy.ComputeAsync(7);

        Assert.Equal(14, result);
        var outcome = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Equal("14", outcome.RenderedValue);
    }

    [Fact]
    public void Nested_traced_calls_produce_child_nodes()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var outer = NarrativeTraceProxy.Create<IService>(
            new NestedService(ctx), ctx);

        outer.Run();

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Run", trace.Roots[0].Signature.MethodName);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal(
            "Add", trace.Roots[0].Children[0].Signature.MethodName);
    }

    [Fact]
    public void ClassName_override_via_proxy_options()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx,
            new ProxyOptions(ClassName: "MathService"));

        proxy.Add(1, 2);

        Assert.Equal(
            "MathService",
            ctx.CaptureTrace().Roots[0].Signature.ClassName);
    }

    [Fact]
    public void Method_with_no_parameters()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IService>(
            new NoopService(), ctx);

        proxy.Run();

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Empty(node.Signature.Parameters);
    }

    [Fact]
    public void Void_method_returns_null_outcome()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IService>(
            new NoopService(), ctx);

        proxy.Run();

        var outcome = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Null(outcome.RenderedValue);
    }

    [Fact]
    public void Returning_null_is_recorded_as_null_not_as_a_void_completion()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ILookupService>(
            new MissingLookupService(), ctx);

        proxy.Find("missing");

        var outcome = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Equal("null", outcome.RenderedValue);
    }

    [Fact]
    public void Proxy_is_transparent_to_caller()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        var result = proxy.Add(10, 20);

        Assert.Equal(30, result);
    }

    [Fact]
    public void IncludeReturnValues_false_suppresses_rendering()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx,
            new ProxyOptions(IncludeReturnValues: false));

        proxy.Add(1, 2);

        var outcome = Assert.IsType<Returned>(
            ctx.CaptureTrace().Roots[0].Outcome);
        Assert.Null(outcome.RenderedValue);
    }

    [Fact]
    public void Narrated_template_resolves_placeholders()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Greet("Alice");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "Greeting Alice",
            node.Signature.Narration);
    }

    [Fact]
    public void Proxy_records_the_declared_identity_of_the_intercepted_method()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(1, 2);

        var signature = ctx.CaptureTrace().Roots[0].Signature;
        Assert.Equal(typeof(ICalculator).Namespace, signature.Namespace);
        Assert.Equal("System.Int32", signature.ReturnType);
        Assert.Equal("System.Int32", signature.Parameters[0].DeclaredType);
        Assert.Equal("System.Int32", signature.Parameters[1].DeclaredType);
    }

    [Fact]
    public void Proxy_records_a_void_return_as_its_declared_type()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Greet("Alice");

        Assert.Equal("System.Void", ctx.CaptureTrace().Roots[0].Signature.ReturnType);
    }

    [Fact]
    public void Proxy_keeps_the_raw_narration_template_beside_the_resolved_prose()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Greet("Alice");

        var signature = ctx.CaptureTrace().Roots[0].Signature;
        Assert.Equal("Greeting Alice", signature.Narration);
        Assert.Equal("Greeting {name}", signature.NarrationTemplate);
    }

    [Fact]
    public void An_unnarrated_method_carries_no_template()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(1, 2);

        var signature = ctx.CaptureTrace().Roots[0].Signature;
        Assert.Null(signature.NarrationTemplate);
        Assert.Null(signature.Narration);
    }

    [Fact]
    public void Inactive_context_bypasses_capture_entirely()
    {
        var ctx = new CountingInactiveContext();
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        var result = proxy.Add(1, 2);

        Assert.Equal(3, result);
        Assert.Equal(0, ctx.EnterCount);
    }

    [Fact]
    public void Inactive_context_still_surfaces_the_original_exception()
    {
        var ctx = new CountingInactiveContext();
        var proxy = NarrativeTraceProxy.Create<IService>(
            new FailingService(), ctx);

        var ex = Assert.Throws<InvalidOperationException>(
            () => proxy.Fail());

        Assert.Equal("boom", ex.Message);
        Assert.Equal(0, ctx.EnterCount);
    }

    [Fact]
    public void Null_argument_preserves_the_placeholder_literal()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Greet(null!);

        Assert.Equal(
            "Greeting {name}",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void Missing_property_preserves_the_full_placeholder_literal()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Audit(new Order(1, "book"));

        Assert.Equal(
            "Auditing {order.Missing}",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void Throwing_property_getter_preserves_the_placeholder_literal()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Poke(new ThrowingProbe());

        Assert.Equal(
            "Poking {probe.Boom}",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void NotTraced_parameter_is_redacted_in_narrated_template()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarratedAuth>(
            new NarratedAuthService(), ctx);

        proxy.Login("alice", "s3cret!");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "Login alice with [REDACTED]",
            node.Signature.Narration);
    }

    [Fact]
    public void NotTraced_parameter_is_redacted_in_on_error_template()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarratedAuth>(
            new NarratedAuthService(), ctx);

        Assert.Throws<InvalidOperationException>(
            () => proxy.Verify("alice", "s3cret!"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "Login failed for alice with [REDACTED]",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void OnError_catch_all_template_resolves_for_any_exception()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IErrorService>(
            new ErrorService(), ctx);

        Assert.Throws<InvalidOperationException>(
            () => proxy.Process("data"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "Failed processing data",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void OnError_selects_the_template_matching_the_thrown_type()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ISiblingErrorService>(
            new SiblingErrorService(), ctx);

        Assert.Throws<IOException>(() => proxy.Run("f.txt"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "io failure on f.txt",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void OnError_yields_no_context_when_thrown_type_matches_nothing()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ISiblingErrorService>(
            new SiblingErrorService(), ctx);

        Assert.Throws<InvalidOperationException>(
            () => proxy.Strict("x"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Null(node.Signature.ErrorContext);
    }

    [Fact]
    public void OnError_leaves_no_context_on_a_successful_call()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IErrorService>(
            new SucceedingErrorService(), ctx);

        proxy.Process("data");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Null(node.Signature.ErrorContext);
    }

    [Fact]
    public async Task OnError_resolves_against_thrown_type_on_async_paths()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAsyncErrorService>(
            new AsyncErrorService(), ctx);

        await Assert.ThrowsAsync<IOException>(
            () => proxy.FetchAsync("feed"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "io failure on feed",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void OnError_most_specific_exception_wins()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IErrorService>(
            new ErrorService(), ctx);

        Assert.Throws<ArgumentException>(
            () => proxy.Validate("bad"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal(
            "Argument error for bad",
            node.Signature.ErrorContext);
    }

    [Fact]
    public void Method_metadata_is_cached()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        proxy.Add(1, 2);
        var afterFirst = NarrativeInterceptor.MetadataCacheCount;

        proxy.Add(3, 4);
        var afterSecond = NarrativeInterceptor.MetadataCacheCount;

        Assert.Equal(afterFirst, afterSecond);
    }

    [Fact]
    public void Traced_overrides_parameter_names()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<ITracedService>(
            new TracedService(), ctx);

        proxy.DoWork(1, "hello");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal("id", node.Signature.Parameters[0].Name);
        Assert.Equal("message", node.Signature.Parameters[1].Name);
    }

    [Fact]
    public void Narrated_template_with_property_access()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Process(new Order(42, "Widget"));

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal("Processing 42", node.Signature.Narration);
    }

    [Fact]
    public void A_notraced_property_reached_through_a_template_path_is_redacted()
    {
        // Owner decision 2026-08-31: naming a path never weakens the rules
        // that apply to the value directly. Cvv carries [NotTraced] on the
        // record's primary constructor parameter, not on the argument
        // itself, so only the property-path check (not the parameter-level
        // one) can catch this.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.Charge(new Card("4111", "123", "tok-1"));

        Assert.Equal(
            "Charging [REDACTED]",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void A_deny_listed_property_name_reached_through_a_template_path_is_redacted()
    {
        // No [NotTraced] anywhere on Token — the name-based deny-list alone
        // (RedactionPolicy.Default matches "token") must catch this, the
        // same as it would for reflective introspection.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.ChargeWithToken(new Card("4111", "123", "tok-1"));

        Assert.Equal(
            "Charging [REDACTED]",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void An_unredacted_property_reached_through_a_template_path_still_resolves()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<INarrated>(
            new NarratedService(), ctx);

        proxy.ShowLast4(new Card("4111", "123", "tok-1"));

        Assert.Equal(
            "Showing 4111",
            ctx.CaptureTrace().Roots[0].Signature.Narration);
    }

    [Fact]
    public void NotTraced_parameter_is_redacted()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IAuth>(
            new AuthService(), ctx);

        proxy.Login("user", "secret");

        var node = ctx.CaptureTrace().Roots[0];
        Assert.Equal("\"user\"", node.Signature.Parameters[0].RenderedValue);
        Assert.False(node.Signature.Parameters[0].Redacted);
        Assert.Equal(
            RedactionPolicy.Marker,
            node.Signature.Parameters[1].RenderedValue);
        Assert.True(node.Signature.Parameters[1].Redacted);
    }

    public interface ICalculator
    {
        int Add(int a, int b);
    }

    public interface IAuth
    {
        void Login(string user, [NotTraced] string password);
    }

    public interface INarratedAuth
    {
        [Narrated("Login {user} with {password}")]
        void Login(string user, [NotTraced] string password);

        [OnError("Login failed for {user} with {password}")]
        void Verify(string user, [NotTraced] string password);
    }

    private sealed class NarratedAuthService : INarratedAuth
    {
        public void Login(string user, string password) { }

        public void Verify(string user, string password) =>
            throw new InvalidOperationException("denied");
    }

    public interface IService
    {
        void Fail();
        void Run();
    }

    public interface ILookupService
    {
        string? Find(string key);
    }

    private sealed class MissingLookupService : ILookupService
    {
        public string? Find(string key) => null;
    }

    private sealed class Calculator : ICalculator
    {
        public int Add(int a, int b) => a + b;
    }

    public interface IAsyncService
    {
        Task RunAsync();
        Task<int> ComputeAsync(int x);
    }

    private sealed class AsyncService : IAsyncService
    {
        public async Task RunAsync()
        {
            await Task.Delay(1);
        }

        public async Task<int> ComputeAsync(int x)
        {
            await Task.Delay(1);
            return x * 2;
        }
    }

    public interface IValueTaskService
    {
        ValueTask RunAsync();
        ValueTask<int> ComputeAsync(int x);
    }

    private sealed class ValueTaskService : IValueTaskService
    {
        public async ValueTask RunAsync()
        {
            await Task.Delay(1);
        }

        public async ValueTask<int> ComputeAsync(int x)
        {
            await Task.Delay(1);
            return x * 2;
        }
    }

    private sealed class FailingAsyncService : IAsyncService
    {
        public async Task RunAsync()
        {
            await Task.Delay(1);
            throw new InvalidOperationException("async boom");
        }

        public async Task<int> ComputeAsync(int x)
        {
            await Task.Delay(1);
            throw new InvalidOperationException("async boom");
        }
    }

    private sealed class NestedService : IService
    {
        private readonly ICalculator _calc;

        public NestedService(INarrativeContext ctx)
        {
            _calc = NarrativeTraceProxy.Create<ICalculator>(
                new Calculator(), ctx);
        }

        public void Fail() { }

        public void Run()
        {
            _calc.Add(1, 2);
        }
    }

    public interface INarrated
    {
        [Narrated("Greeting {name}")]
        void Greet(string name);

        [Narrated("Processing {order.Id}")]
        void Process(Order order);

        [Narrated("Auditing {order.Missing}")]
        void Audit(Order order);

        [Narrated("Poking {probe.Boom}")]
        void Poke(ThrowingProbe probe);

        [Narrated("Charging {card.Cvv}")]
        void Charge(Card card);

        [Narrated("Charging {card.Token}")]
        void ChargeWithToken(Card card);

        [Narrated("Showing {card.Last4}")]
        void ShowLast4(Card card);
    }

    public record Card(string Last4, [NotTraced] string Cvv, string Token);

    public sealed class ThrowingProbe
    {
        // Must stay an INSTANCE property: NarrationResolver looks templates
        // up with BindingFlags.Public | BindingFlags.Instance, so a static
        // Boom would be invisible and the test would pass on the
        // "unknown property" path instead of the throwing-getter path.
        private readonly string _failure = "getter blew up";

        public string Boom =>
            throw new InvalidOperationException(_failure);
    }

    public record Order(int Id, string Item);

    public interface ITracedService
    {
        [Traced("id", "message")]
        void DoWork(int x, string y);
    }

    private sealed class TracedService : ITracedService
    {
        public void DoWork(int x, string y) { }
    }

    public interface IErrorService
    {
        [OnError("Failed processing {input}")]
        void Process(string input);

        [OnError("General error for {input}")]
        [OnError("Argument error for {input}",
            ExceptionType = typeof(ArgumentException))]
        void Validate(string input);
    }

    private sealed class ErrorService : IErrorService
    {
        public void Process(string input) =>
            throw new InvalidOperationException("fail");

        public void Validate(string input) =>
            throw new ArgumentException("bad arg");
    }

    public interface IAsyncErrorService
    {
        [OnError("io failure on {input}",
            ExceptionType = typeof(IOException))]
        [OnError("range failure on {input}",
            ExceptionType = typeof(ArgumentOutOfRangeException))]
        Task<string> FetchAsync(string input);
    }

    private sealed class AsyncErrorService : IAsyncErrorService
    {
        public async Task<string> FetchAsync(string input)
        {
            await Task.Yield();
            throw new IOException("socket closed");
        }
    }

    public interface ISiblingErrorService
    {
        // ArgumentOutOfRangeException sits deeper in the hierarchy than
        // IOException — depth-based selection would always pick it.
        [OnError("io failure on {input}",
            ExceptionType = typeof(IOException))]
        [OnError("range failure on {input}",
            ExceptionType = typeof(ArgumentOutOfRangeException))]
        void Run(string input);

        [OnError("argument trouble with {input}",
            ExceptionType = typeof(ArgumentException))]
        void Strict(string input);
    }

    private sealed class SiblingErrorService : ISiblingErrorService
    {
        public void Run(string input) =>
            throw new IOException("disk full");

        public void Strict(string input) =>
            throw new InvalidOperationException("unrelated");
    }

    private sealed class SucceedingErrorService : IErrorService
    {
        public void Process(string input) { }

        public void Validate(string input) { }
    }

    private sealed class CountingInactiveContext : INarrativeContext
    {
        public int EnterCount { get; private set; }

        public bool IsActive => false;

        public bool CapturesParameterValues => false;

        public TraceId? CurrentTraceId => null;

        public TraceId EnsureTraceId() =>
            SpanIdGenerator.GenerateTraceId();

        public string? StoryId => null;

        public string? ChapterId => null;

        public SpanId EnterMethod(
            string className, string methodName,
            IReadOnlyList<ParameterCapture> parameters,
            MethodOptions? options = null)
        {
            EnterCount++;
            return default;
        }

        public void ExitMethodWithReturn(
            string? renderedValue, SpanId? handle = null)
        { }

        public void ExitMethodWithReturn(
            string? renderedValue,
            RenderedValue? structuredValue,
            SpanId? handle = null)
        { }

        public void ExitMethodWithException(
            Exception? exception, SpanId? handle = null)
        { }

        public void ExitMethodWithException(
            Exception? exception, string? errorContext,
            SpanId? handle = null)
        { }

        public void DetachFrame(SpanId handle) { }

        public TraceTree CaptureTrace() => new([]);

        public void Reset() { }

        public IContextSnapshot Snapshot() =>
            NoopContext.Instance.Snapshot();

        public SpanId? ParentOf(SpanId handle) => null;

        public T RunScoped<T>(SpanId handle, Func<T> fn) => fn();

        public void GraftChild(TraceNode node) { }

        public void SetRequestContext(
            string? httpMethod, HttpRoute? httpRoute,
            ClientIp? clientIp)
        { }

        public void SetUserContext(
            EnduserId? enduserId, SessionId? sessionId,
            TenantId? tenantId)
        { }
    }

    private sealed class NarratedService : INarrated
    {
        public void Greet(string name) { }
        public void Process(Order order) { }
        public void Audit(Order order) { }
        public void Poke(ThrowingProbe probe) { }
        public void Charge(Card card) { }
        public void ChargeWithToken(Card card) { }
        public void ShowLast4(Card card) { }
    }

    private sealed class AuthService : IAuth
    {
        public void Login(string user, string password) { }
    }

    private sealed class NoopService : IService
    {
        public void Fail() { }
        public void Run() { }
    }

    private sealed class FailingService : IService
    {
        public void Fail() =>
            throw new InvalidOperationException("boom");

        public void Run() { }
    }

    [Fact]
    public void Nested_proxy_calls_see_parent_via_scopedParent()
    {
        var ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());
        var inner = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);
        var outer = NarrativeTraceProxy
            .Create<IOuterService>(
                new OuterService(inner), ctx);

        outer.Compute();

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("IOuterService",
            trace.Roots[0].Signature.ClassName);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("ICalculator",
            trace.Roots[0].Children[0].Signature.ClassName);
    }

    public interface IOuterService
    {
        int Compute();
    }

    private sealed class OuterService : IOuterService
    {
        private readonly ICalculator _calc;

        public OuterService(ICalculator calc)
        {
            _calc = calc;
        }

        public int Compute() => _calc.Add(1, 2);
    }
}
