// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Diagnostics;

namespace NarrativeTrace.Runtime;

/// <summary>
/// A capture that can answer for a set of events: the ones it recorded itself
/// plus everything a worker published under a snapshot of it.
/// </summary>
/// <remarks>
/// The one definition both directions of propagation use, so a call can never
/// be visible while a worker's scope is open and absent after it closes.
/// </remarks>
internal interface IReportableCapture
{
    /// <summary>
    /// Appends every event this capture may report — its own first, then its
    /// live and adopted workers', transitively.
    /// </summary>
    void CollectReportable(List<TraceEvent> into);
}

/// <summary>
/// The bookkeeping behind "published, therefore reportable": which worker
/// captures a capture is currently answering for, which it has taken over for
/// good, and what it refused because it was full.
/// </summary>
/// <remarks>
/// <para>
/// Live workers are held <b>weakly</b> — a worker that never closes its scope
/// must not keep its capture alive through this registry, and a cleared entry
/// simply contributes nothing. Adopted workers are held <b>strongly</b>: their
/// hand-over is the reason the origin can still tell the story after the worker
/// thread is gone.
/// </para>
/// <para>
/// Thread-safe. Worker threads register and adopt while the origin thread
/// reads; the lock is held only to move entries between the two lists and to
/// copy them out, never while walking a child, so a deep chain cannot serialize
/// on one lock.
/// </para>
/// </remarks>
internal sealed class AdoptionLedger
{
    /// <summary>
    /// Ceiling on spans one capture will adopt from workers.
    /// </summary>
    /// <remarks>
    /// The bound is not about the memory of a request-scoped capture, which is
    /// dropped whole by <c>Reset()</c>. It exists for the capture that never
    /// resets — a daemon loop dispatching async work for the life of the
    /// process — where an unbounded set is a genuine leak.
    /// </remarks>
    internal const int DefaultCeiling = 10_000;

    private readonly int _ceiling;
    private readonly object _lock = new();
    private readonly IReportableCapture _owner;

    private List<WeakReference<IReportableCapture>>? _live;
    private List<IReportableCapture>? _adopted;
    private int _adoptedSpans;
    private long _refusedScopes;
    private long _refusedSpans;

    internal AdoptionLedger(IReportableCapture owner)
        : this(owner, DefaultCeiling)
    {
    }

    /// <summary>
    /// Test seam: the ceiling is a boundary condition, reachable in a unit test
    /// only by lowering it.
    /// </summary>
    internal AdoptionLedger(IReportableCapture owner, int ceiling)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (ceiling <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ceiling), ceiling, "Ceiling must be positive.");
        }

        _owner = owner;
        _ceiling = ceiling;
    }

    /// <summary>Worker scopes refused whole because the ceiling had no room.</summary>
    internal long RefusedScopeCount
    {
        get { lock (_lock) { return _refusedScopes; } }
    }

    /// <summary>Spans lost to those refusals — what a reader of the trace is missing.</summary>
    internal long RefusedSpanCount
    {
        get { lock (_lock) { return _refusedSpans; } }
    }

    /// <summary>
    /// Whether nothing has ever been propagated through this capture — the
    /// common case, and the one where reporting can skip the union entirely.
    /// </summary>
    internal bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return (_live is null || _live.Count == 0)
                    && (_adopted is null || _adopted.Count == 0);
            }
        }
    }

    /// <summary>Test seam: how many worker captures are currently registered live.</summary>
    internal int LiveCount
    {
        get { lock (_lock) { return PruneDead(); } }
    }

    /// <summary>
    /// Announces a worker capture for the lifetime of its scope, so its spans
    /// are reportable here from the moment they are published rather than from
    /// scope close.
    /// </summary>
    /// <param name="child">The freshly activated worker capture; never the owner itself.</param>
    /// <returns>
    /// The handle to hand back to <see cref="Adopt"/>, or <see langword="null"/>
    /// when the ceiling refused the registration. A refused registration loses
    /// nothing — the worker's spans still arrive at scope close — so it is not
    /// counted as a refusal.
    /// </returns>
    internal WeakReference<IReportableCapture>? Register(
        IReportableCapture child)
    {
        if (child is null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        if (ReferenceEquals(child, _owner))
        {
            throw new ArgumentException(
                "A capture cannot be its own live child.", nameof(child));
        }

        lock (_lock)
        {
            return RegisterWithinCeiling(child);
        }
    }

    private WeakReference<IReportableCapture>? RegisterWithinCeiling(
        IReportableCapture child)
    {
        _live ??= [];
        if (PruneDead() >= _ceiling)
        {
            return null;
        }

        var registration = new WeakReference<IReportableCapture>(child);
        _live.Add(registration);
        Debug.Assert(Invariant(), "AdoptionLedger invariant after Register");
        return registration;
    }

    /// <summary>
    /// Takes a worker's reportable set over for good and ends its live
    /// registration, in one step so a racing capture sees the worker through
    /// one side or the other and never through neither.
    /// </summary>
    /// <param name="child">The worker capture whose scope is closing.</param>
    /// <param name="registration">
    /// The handle <see cref="Register"/> returned, or <see langword="null"/> if
    /// the registration was refused.
    /// </param>
    /// <remarks>
    /// All-or-nothing by design: a batch that would cross the ceiling is
    /// refused whole and counted. Adopting a prefix of it would strand children
    /// whose parent stayed out, and a parentless node renders as a root — so
    /// the artifact would assert a call graph that never happened, differently
    /// on every run. An incomplete trace is honest; a wrong-shaped one is not.
    /// </remarks>
    internal void Adopt(
        IReportableCapture child,
        WeakReference<IReportableCapture>? registration)
    {
        if (child is null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        var spans = ReportableSpanCount(child);
        lock (_lock)
        {
            Release(registration);
            AdoptWhole(child, spans);
            Debug.Assert(Invariant(), "AdoptionLedger invariant after Adopt");
        }
    }

    /// <summary>Ends a live registration without adopting anything.</summary>
    /// <param name="registration">The handle from <see cref="Register"/>; a <see langword="null"/> handle is a no-op.</param>
    internal void Unregister(
        WeakReference<IReportableCapture>? registration)
    {
        lock (_lock)
        {
            Release(registration);
        }
    }

    /// <summary>
    /// Appends every event the registered workers can answer for, transitively.
    /// </summary>
    /// <remarks>
    /// The recursion cannot cycle: a worker capture is always freshly created
    /// by an activation and registered with exactly one owner, so the registry
    /// is a forest of new nodes with no back edge. Depth is the nesting depth
    /// of simultaneously open async scopes, which is bounded by live threads.
    /// </remarks>
    internal void CollectReportable(List<TraceEvent> into)
    {
        if (into is null)
        {
            throw new ArgumentNullException(nameof(into));
        }

        var children = Children();
        for (var i = 0; i < children.Count; i++)
        {
            children[i].CollectReportable(into);
        }
    }

    /// <summary>
    /// True iff the ledger is self-consistent: non-negative counters that agree
    /// with each other, an adopted total within the ceiling, and no null or
    /// cleared entries left where a live one is expected.
    /// </summary>
    internal bool Invariant()
    {
        lock (_lock)
        {
            if (_adoptedSpans < 0 || _refusedScopes < 0 || _refusedSpans < 0)
            {
                return false;
            }

            if (_adoptedSpans > _ceiling)
            {
                return false;
            }

            // A refused scope always cost at least one span, and spans can only
            // be lost by refusing a scope.
            if ((_refusedScopes == 0) != (_refusedSpans == 0))
            {
                return false;
            }

            return NoNullEntries();
        }
    }

    private bool NoNullEntries()
    {
        if (_adopted is not null && _adopted.Contains(null!))
        {
            return false;
        }

        return _live is null || !_live.Contains(null!);
    }

    private void AdoptWhole(IReportableCapture child, int spans)
    {
        if (spans == 0)
        {
            return;
        }

        if (_adoptedSpans + spans > _ceiling)
        {
            _refusedScopes++;
            _refusedSpans += spans;
            return;
        }

        _adoptedSpans += spans;
        (_adopted ??= []).Add(child);
    }

    private void Release(WeakReference<IReportableCapture>? registration)
    {
        if (registration is not null)
        {
            _live?.Remove(registration);
        }
    }

    private static int ReportableSpanCount(IReportableCapture child)
    {
        var events = new List<TraceEvent>();
        child.CollectReportable(events);
        var spans = 0;
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is EnterEvent)
            {
                spans++;
            }
        }

        return spans;
    }

    /// <summary>
    /// A stable copy of everything currently registered — live entries that are
    /// still alive, then adopted ones — taken under the lock so the walk itself
    /// holds nothing.
    /// </summary>
    private List<IReportableCapture> Children()
    {
        lock (_lock)
        {
            var children = new List<IReportableCapture>(
                (_live?.Count ?? 0) + (_adopted?.Count ?? 0));
            AddLive(children);
            if (_adopted is not null)
            {
                children.AddRange(_adopted);
            }

            return children;
        }
    }

    private void AddLive(List<IReportableCapture> children)
    {
        if (_live is null)
        {
            return;
        }

        PruneDead();
        for (var i = 0; i < _live.Count; i++)
        {
            if (_live[i].TryGetTarget(out var child))
            {
                children.Add(child);
            }
        }
    }

    /// <summary>
    /// Drops registrations whose worker has been collected and returns how many
    /// remain: a worker that died without closing its scope must not hold a
    /// slot against the ceiling forever.
    /// </summary>
    private int PruneDead()
    {
        if (_live is null)
        {
            return 0;
        }

        for (var i = _live.Count - 1; i >= 0; i--)
        {
            if (!_live[i].TryGetTarget(out _))
            {
                _live.RemoveAt(i);
            }
        }

        return _live.Count;
    }
}
