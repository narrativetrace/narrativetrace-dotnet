// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The port-specific half of the async-adoption audit: work that is
/// <i>detached</i> from an <see cref="AsyncNarrativeContext"/> scope rather
/// than propagated through a snapshot.
/// </summary>
/// <remarks>
/// <see cref="AsyncNarrativeContext"/> holds its inner capture in an
/// <see cref="AsyncLocal{T}"/>, so a continuation, a <c>Task.Run</c> and a
/// thread started inside the scope all inherit it through
/// <c>ExecutionContext</c> — the cross-port contract's "published, therefore
/// reportable" holds here by construction rather than by adoption. These cases
/// pin that, and pin the two places where the flow's answer differs from the
/// snapshot's: placement follows <i>execution</i> time for detached work, and a
/// read taken after the scope closed must not be handed a concurrent scope's
/// trace.
/// <para>Deterministic throughout: latches, never <c>Task.Delay</c>.</para>
/// </remarks>
public sealed class DetachedAsyncWorkAdoptionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Detached_work_started_inside_the_span_reaches_the_capture()
    {
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        Task? detached = null;

        await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            detached = Task.Run(() =>
            {
                ctx.EnterMethod("NotificationService", "NotifyOrderPlaced", []);
                ctx.ExitMethodWithReturn("true");
            });
            await detached.ConfigureAwait(false);
            ctx.ExitMethodWithReturn("\"ORD-1\"");
        });

        var root = Assert.Single(ctx.CaptureTrace().Roots);
        Assert.Equal(
            "NotifyOrderPlaced",
            Assert.Single(root.Children).Signature.MethodName);
    }

    [Fact]
    public async Task A_fire_and_forget_continuation_reaches_the_capture()
    {
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var traced = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        await ctx.RunAsync(async () =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            _ = started.Task.ContinueWith(
                _ =>
                {
                    ctx.EnterMethod("AuditService", "Record", []);
                    ctx.ExitMethodWithReturn("true");
                    traced.SetResult(true);
                },
                TaskScheduler.Default);
            started.SetResult(true);
            await traced.Task.ConfigureAwait(false);
            ctx.ExitMethodWithReturn("\"ORD-1\"");
        });

        var root = Assert.Single(ctx.CaptureTrace().Roots);
        Assert.Equal(
            "Record", Assert.Single(root.Children).Signature.MethodName);
    }

    [Fact]
    public async Task Work_submitted_before_the_scope_closed_reaches_the_capture_after_it()
    {
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        using var scopeClosed = new ManualResetEventSlim(false);
        using var traced = new ManualResetEventSlim(false);
        Task? detached = null;

        ctx.Run(() =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            ctx.ExitMethodWithReturn("\"ORD-1\"");
            detached = Task.Run(() =>
            {
                scopeClosed.Wait(Timeout);
                ctx.EnterMethod("NotificationService", "NotifyOrderPlaced", []);
                ctx.ExitMethodWithReturn("true");
                traced.Set();
            });
        });

        scopeClosed.Set();
        Assert.True(traced.Wait(Timeout));
        Assert.NotNull(detached);
        await detached!;

        Assert.Equal(
            ["PlaceOrder", "NotifyOrderPlaced"],
            LiveSnapshotScopeVisibilityTests.MethodNames(ctx.CaptureTrace()));
    }

    [Fact]
    public void Bare_detached_work_is_placed_where_it_runs_not_where_it_was_submitted()
    {
        // The documented divergence from the snapshot path: a bare Task.Run
        // carries no submit-time lineage, so the flow's live stack decides.
        // Take a snapshot at the submission point to pin placement instead.
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        using var parentReturned = new ManualResetEventSlim(false);

        ctx.Run(() =>
        {
            ctx.EnterMethod("OrderService", "PlaceOrder", []);
            var detached = Task.Run(() =>
            {
                parentReturned.Wait(Timeout);
                ctx.EnterMethod("NotificationService", "NotifyOrderPlaced", []);
                ctx.ExitMethodWithReturn("true");
            });
            ctx.ExitMethodWithReturn("\"ORD-1\"");
            parentReturned.Set();
            detached.GetAwaiter().GetResult();
        });

        var roots = ctx.CaptureTrace().Roots;

        Assert.Equal(2, roots.Count);
        Assert.Equal("NotifyOrderPlaced", roots[1].Signature.MethodName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Each_flow_reads_the_trace_of_its_own_completed_scope(int lastToClose)
    {
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        using var scopes = new OverlappingScopes(ctx, lastToClose);

        scopes.RunBothToCompletion();

        // Both scopes had closed before either read, so a single shared
        // "last completed" slot can only be right for one of them.
        Assert.Equal(["First"], scopes.NamesReadBy(0));
        Assert.Equal(["Second"], scopes.NamesReadBy(1));
    }

    // Both closing orders, because a rule that only asks "was anything open
    // when I closed?" is right for the scope that closes first and wrong for
    // the one that closes last — a 50% flake rather than a failure.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Concurrent_scopes_do_not_hand_a_foreign_trace_to_a_later_read(
        int lastToClose)
    {
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        using (var scopes = new OverlappingScopes(ctx, lastToClose))
        {
            scopes.RunBothToCompletion();
        }

        // Neither scope's trace belongs to this flow, so the shared fallback
        // must stay silent rather than name one of them.
        Assert.Empty(ctx.CaptureTrace().Roots);
    }

    [Fact]
    public void A_solitary_scope_after_a_concurrent_burst_is_readable_again()
    {
        var ctx = new AsyncNarrativeContext(new NarrativeTraceConfig());
        using (var scopes = new OverlappingScopes(ctx, lastToClose: 0))
        {
            scopes.RunBothToCompletion();
        }

        ctx.Run(() =>
        {
            ctx.EnterMethod("OrderService", "Afterwards", []);
            ctx.ExitMethodWithReturn("true");
        });

        // Contention is a property of the scopes that overlapped, not a latch
        // on the context: an uncontended scope answers again.
        Assert.Equal(
            ["Afterwards"],
            LiveSnapshotScopeVisibilityTests.MethodNames(ctx.CaptureTrace()));
    }

    /// <summary>
    /// Two scopes that provably overlap, and whose reads provably both happen
    /// after both scopes have closed — the interleaving a shared
    /// "last completed scope" field cannot serve, held by latches rather than
    /// hoped for.
    /// </summary>
    private sealed class OverlappingScopes : IDisposable
    {
        private readonly AsyncNarrativeContext _context;
        private readonly CountdownEvent _bothInside = new(2);
        private readonly CountdownEvent _bothClosed = new(2);
        private readonly ManualResetEventSlim _firstClosed = new(false);
        private readonly IReadOnlyList<string>[] _names =
            new IReadOnlyList<string>[2];

        private readonly Thread[] _threads = new Thread[2];

        /// <param name="context">The context both scopes run against.</param>
        /// <param name="lastToClose">
        /// Which slot keeps its scope open until the other has closed, so both
        /// interleavings are reachable deliberately rather than by luck.
        /// </param>
        internal OverlappingScopes(
            AsyncNarrativeContext context, int lastToClose)
        {
            _context = context;
            _threads[0] = ScopeThread("First", 0, lastToClose == 0);
            _threads[1] = ScopeThread("Second", 1, lastToClose == 1);
        }

        internal void RunBothToCompletion()
        {
            for (var i = 0; i < _threads.Length; i++)
            {
                _threads[i].Start();
            }

            for (var i = 0; i < _threads.Length; i++)
            {
                Assert.True(_threads[i].Join(Timeout));
            }
        }

        internal IReadOnlyList<string> NamesReadBy(int slot) => _names[slot];

        public void Dispose()
        {
            _bothInside.Dispose();
            _bothClosed.Dispose();
            _firstClosed.Dispose();
        }

        private Thread ScopeThread(
            string methodName, int slot, bool closesLast)
        {
            return new Thread(() =>
            {
                _context.Run(() =>
                {
                    _context.EnterMethod("OrderService", methodName, []);
                    _context.ExitMethodWithReturn("true");
                    _bothInside.Signal();
                    _bothInside.Wait(Timeout);
                    if (closesLast)
                    {
                        _firstClosed.Wait(Timeout);
                    }
                });
                _firstClosed.Set();
                _bothClosed.Signal();
                _bothClosed.Wait(Timeout);
                _names[slot] = LiveSnapshotScopeVisibilityTests.MethodNames(
                    _context.CaptureTrace());
            })
            { IsBackground = true };
        }
    }
}
