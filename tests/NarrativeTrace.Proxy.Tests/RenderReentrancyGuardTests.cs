// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Proxy.Tests;

/// <summary>
/// Pins the render-reentrancy defect (a cross-port finding): rendering a
/// traced call's parameter or return value must never itself open a span,
/// even when the rendered value is a live tracing proxy.
/// </summary>
/// <remarks>
/// <see cref="NarrativeInterceptor"/> is the single call-interception surface
/// in this runtime (unlike the JVM edition, which has both a bytecode-woven
/// agent and a proxy engine that can share one JVM). When
/// <see cref="ValueRenderer"/> walks a rendered value's public members via
/// <see cref="TypeShape"/> and that value's runtime type is a
/// <see cref="System.Reflection.DispatchProxy"/>-generated proxy, reading a
/// property reflectively invokes the proxy's generated getter, which calls
/// straight back into <see cref="NarrativeInterceptor.Invoke"/> — a call the
/// application never made, opening a span attributed as a root (parameter
/// rendering runs before <c>EnterMethod</c>) or as a child of whatever span
/// happens to still be open (return-value rendering runs before the owning
/// span's own exit is recorded). Genuine calls the traced method body itself
/// makes to a proxied collaborator must remain unaffected — the guard is
/// scoped to rendering, not a blanket suppression.
/// </remarks>
public class RenderReentrancyGuardTests
{
    public interface ICollaborator
    {
        string Name { get; }
    }

    private sealed class Collaborator : ICollaborator
    {
        public string Name => "widget";
    }

    public interface IReceiver
    {
        // §127: ValueRenderer/TypeShape no longer reads a property through
        // its getter at all (only a backing field, never an accessor — see
        // TypeShape.BackingFieldOf), so a plain reflective member walk can
        // no longer reach a proxy's generated Name getter to exercise the
        // guard. The narration placeholder is the one remaining path that
        // still falls back to the accessor for a property with no backing
        // field (NarrationResolver.ReadProperty — the author explicitly
        // named this value), so it is what now drives PausableCollaborator's
        // getter for the reentrancy tests below.
        [Narrated("Received {c.Name}")]
        void Receive(ICollaborator c);

        ICollaborator MakeCollaborator();
        int TouchViaField();
    }

    private sealed class ReceiverService : IReceiver
    {
        private readonly ICollaborator _collaborator;

        public ReceiverService(ICollaborator collaborator)
        {
            _collaborator = collaborator;
        }

        public void Receive(ICollaborator c) { }

        public ICollaborator MakeCollaborator() => _collaborator;

        // The genuine call the traced method body itself makes — must stay
        // traced as a child, unlike the reflective read rendering performs.
        public int TouchViaField() => _collaborator.Name.Length;
    }

    [Fact]
    public void Rendering_a_proxied_parameter_produces_no_spans_of_its_own()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var collaboratorProxy = NarrativeTraceProxy.Create<ICollaborator>(
            new Collaborator(), ctx);
        var receiver = NarrativeTraceProxy.Create<IReceiver>(
            new ReceiverService(collaboratorProxy), ctx);

        receiver.Receive(collaboratorProxy);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Receive", trace.Roots[0].Signature.MethodName);
        Assert.Empty(trace.Roots[0].Children);
    }

    [Fact]
    public void Rendering_a_proxied_return_value_produces_no_spans_of_its_own()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var collaboratorProxy = NarrativeTraceProxy.Create<ICollaborator>(
            new Collaborator(), ctx);
        var receiver = NarrativeTraceProxy.Create<IReceiver>(
            new ReceiverService(collaboratorProxy), ctx);

        receiver.MakeCollaborator();

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("MakeCollaborator", trace.Roots[0].Signature.MethodName);
        Assert.Empty(trace.Roots[0].Children);
    }

    [Fact]
    public void The_method_bodys_own_call_to_the_proxied_collaborator_is_still_traced_as_a_child()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var collaboratorProxy = NarrativeTraceProxy.Create<ICollaborator>(
            new Collaborator(), ctx);
        var receiver = NarrativeTraceProxy.Create<IReceiver>(
            new ReceiverService(collaboratorProxy), ctx);

        var length = receiver.TouchViaField();

        Assert.Equal("widget".Length, length);
        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("TouchViaField", trace.Roots[0].Signature.MethodName);
        Assert.Single(trace.Roots[0].Children);
        Assert.Equal("get_Name", trace.Roots[0].Children[0].Signature.MethodName);
    }

    public interface IThrowingCollaborator
    {
        string Boom { get; }
    }

    private sealed class ThrowingCollaboratorImpl : IThrowingCollaborator
    {
        public string Boom => throw new InvalidOperationException("kaboom");
    }

    public interface IReceiverThrowing
    {
        // See IReceiver.Receive's remark: the narration placeholder is what
        // still reaches Boom's accessor (no backing field) under §127.
        [Narrated("Received {c.Boom}")]
        void ReceiveThrowing(IThrowingCollaborator c);
    }

    private sealed class ReceiverThrowingService : IReceiverThrowing
    {
        public void ReceiveThrowing(IThrowingCollaborator c) { }
    }

    public interface ICalculator
    {
        int Add(int a, int b);
    }

    private sealed class Calculator : ICalculator
    {
        public int Add(int a, int b) => a + b;
    }

    [Fact]
    public void A_throwing_getter_during_rendering_leaves_the_guard_cleared_for_the_next_genuine_call()
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var throwingProxy = NarrativeTraceProxy.Create<IThrowingCollaborator>(
            new ThrowingCollaboratorImpl(), ctx);
        var receiver = NarrativeTraceProxy.Create<IReceiverThrowing>(
            new ReceiverThrowingService(), ctx);
        var calc = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctx);

        receiver.ReceiveThrowing(throwingProxy);
        var sum = calc.Add(2, 3);

        Assert.Equal(5, sum);
        var trace = ctx.CaptureTrace();
        Assert.Equal(2, trace.Roots.Count);
        Assert.Equal("ReceiveThrowing", trace.Roots[0].Signature.MethodName);
        Assert.Empty(trace.Roots[0].Children);
        Assert.Equal("Add", trace.Roots[1].Signature.MethodName);
        var addOutcome = Assert.IsType<Returned>(trace.Roots[1].Outcome);
        Assert.Equal("5", addOutcome.RenderedValue);
    }

    private sealed class PausableCollaborator : ICollaborator
    {
        private readonly TaskCompletionSource _enteredRendering;
        private readonly Task _releaseRendering;

        public PausableCollaborator(
            TaskCompletionSource enteredRendering, Task releaseRendering)
        {
            _enteredRendering = enteredRendering;
            _releaseRendering = releaseRendering;
        }

        public string Name
        {
            get
            {
                _enteredRendering.TrySetResult();
                _releaseRendering.Wait();
                return "paused";
            }
        }
    }

    [Fact]
    public async Task Rendering_in_progress_on_one_task_does_not_suppress_a_genuine_span_on_another_task()
    {
        var enteredRendering = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRendering = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var ctxA = new SyncNarrativeContext(new NarrativeTraceConfig());
        var pausable = new PausableCollaborator(
            enteredRendering, releaseRendering.Task);
        var pausableProxy = NarrativeTraceProxy.Create<ICollaborator>(
            pausable, ctxA);
        var receiverA = NarrativeTraceProxy.Create<IReceiver>(
            new ReceiverService(pausableProxy), ctxA);

        var ctxB = new SyncNarrativeContext(new NarrativeTraceConfig());
        var calcB = NarrativeTraceProxy.Create<ICalculator>(
            new Calculator(), ctxB);

        var taskA = Task.Run(() => receiverA.Receive(pausableProxy));

        // Real barrier, not a delay (retro rule 3): proceed only once thread
        // A has genuinely entered the rendering-time getter and is blocked
        // there.
        await enteredRendering.Task;

        var resultB = await Task.Run(() => calcB.Add(2, 3));

        releaseRendering.SetResult();
        await taskA;

        Assert.Equal(5, resultB);
        var traceB = ctxB.CaptureTrace();
        Assert.Single(traceB.Roots);
        Assert.Equal("Add", traceB.Roots[0].Signature.MethodName);
        var addOutcome = Assert.IsType<Returned>(traceB.Roots[0].Outcome);
        Assert.Equal("5", addOutcome.RenderedValue);

        var traceA = ctxA.CaptureTrace();
        Assert.Single(traceA.Roots);
        Assert.Equal("Receive", traceA.Roots[0].Signature.MethodName);
        Assert.Empty(traceA.Roots[0].Children);
    }
}
