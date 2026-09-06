// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The registry behind "published, therefore reportable", tested directly: its
/// guards, its ceiling, the pruning of a collected registration, and the rule
/// that a worker contributes exactly what it will hand over at close.
/// </summary>
/// <remarks>
/// The .NET mirror of Java's <c>TraceStackLiveChildTest</c>. Its subject is
/// <see cref="AdoptionLedger"/> rather than the context, so the boundary
/// conditions are reachable without racing threads.
/// </remarks>
public sealed class AdoptionLedgerTests
{
    [Fact]
    public void A_ledger_with_no_children_reports_nothing()
    {
        var owner = new FakeCapture();

        Assert.Empty(Reported(owner.Ledger));
        Assert.True(owner.Ledger.IsEmpty);
    }

    [Fact]
    public void A_live_childs_calls_are_reportable_by_the_owner()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        child.Publish("Send");

        owner.Ledger.Register(child);

        Assert.Equal(["Send"], Reported(owner.Ledger));
        Assert.False(owner.Ledger.IsEmpty);
    }

    [Fact]
    public void A_live_child_contributes_exactly_what_it_will_hand_over_at_close()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        var grandchild = new FakeCapture();
        child.Publish("NotifyOrderPlaced");
        grandchild.Publish("Send");
        child.Ledger.Register(grandchild);

        owner.Ledger.Register(child);

        // Everything the child can answer for — not merely what it ran itself.
        Assert.Equal(
            ["NotifyOrderPlaced", "Send"], Reported(owner.Ledger));
    }

    [Fact]
    public void A_live_grandchild_reaches_the_owner_through_its_parent()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        var grandchild = new FakeCapture();
        grandchild.Publish("Send");
        child.Ledger.Register(grandchild);
        owner.Ledger.Register(child);

        Assert.Equal(["Send"], Reported(owner.Ledger));
    }

    [Fact]
    public void A_chain_of_live_captures_is_walked_to_its_end()
    {
        var owner = new FakeCapture();
        var current = owner;

        // Register holds a live child only weakly (by design — see
        // AdoptionLedger's own remarks). Reassigning the loop variable each
        // hop would leave every intermediate FakeCapture reachable solely
        // through that weak reference, so a GC between construction and the
        // walk below could collect one and silently truncate the chain.
        // `chain` keeps every hop strongly referenced until the walk is done.
        var chain = new List<FakeCapture>();
        for (var depth = 0; depth < 6; depth++)
        {
            var next = new FakeCapture();
            next.Publish("Hop" + depth.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
            current.Ledger.Register(next);
            chain.Add(next);
            current = next;
        }

        Assert.Equal(
            ["Hop0", "Hop1", "Hop2", "Hop3", "Hop4", "Hop5"],
            Reported(owner.Ledger));

        GC.KeepAlive(chain);
    }

    [Fact]
    public void Every_live_child_contributes()
    {
        var owner = new FakeCapture();
        var first = new FakeCapture();
        var second = new FakeCapture();
        first.Publish("First");
        second.Publish("Second");

        owner.Ledger.Register(first);
        owner.Ledger.Register(second);

        Assert.Equal(["First", "Second"], Reported(owner.Ledger));
    }

    [Fact]
    public void Unregistering_ends_the_contribution()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        child.Publish("Send");
        var registration = owner.Ledger.Register(child);

        owner.Ledger.Unregister(registration);

        Assert.Empty(Reported(owner.Ledger));
        Assert.Equal(0, owner.Ledger.LiveCount);
    }

    [Fact]
    public void Adopting_keeps_the_calls_and_ends_the_registration()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        child.Publish("Send");
        var registration = owner.Ledger.Register(child);

        owner.Ledger.Adopt(child, registration);

        Assert.Equal(["Send"], Reported(owner.Ledger));
        Assert.Equal(0, owner.Ledger.LiveCount);
    }

    [Fact]
    public void Adoption_hands_over_the_whole_chain_not_only_its_own_calls()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        var grandchild = new FakeCapture();
        child.Publish("NotifyOrderPlaced");
        grandchild.Publish("Send");
        child.Ledger.Register(grandchild);
        var registration = owner.Ledger.Register(child);

        owner.Ledger.Adopt(child, registration);

        Assert.Equal(
            ["NotifyOrderPlaced", "Send"], Reported(owner.Ledger));
    }

    [Fact]
    public void A_call_reachable_through_both_adoption_and_a_live_child_is_reported_once()
    {
        var owner = new FakeCapture();
        var child = new FakeCapture();
        child.Publish("Send");
        var registration = owner.Ledger.Register(child);
        owner.Ledger.Adopt(child, registration);

        // A second dispose would have re-adopted; the registration is already
        // gone, so nothing can arrive through both sides at once.
        Assert.Equal(["Send"], Reported(owner.Ledger));
    }

    [Fact]
    public void Unregistering_a_refused_registration_is_harmless()
    {
        var owner = new FakeCapture();

        owner.Ledger.Unregister(null);

        Assert.Empty(Reported(owner.Ledger));
    }

    [Fact]
    public void Unregistering_on_a_ledger_that_never_registered_is_harmless()
    {
        var owner = new FakeCapture();
        var other = new FakeCapture();
        var foreign = other.Ledger.Register(new FakeCapture());

        owner.Ledger.Unregister(foreign);

        Assert.Empty(Reported(owner.Ledger));
        Assert.Equal(1, other.Ledger.LiveCount);
    }

    [Fact]
    public void The_ceiling_refuses_further_registrations()
    {
        var owner = new FakeCapture(ceiling: 2);
        Assert.NotNull(owner.Ledger.Register(new FakeCapture()));
        Assert.NotNull(owner.Ledger.Register(new FakeCapture()));

        Assert.Null(owner.Ledger.Register(new FakeCapture()));
    }

    [Fact]
    public void A_refused_registration_is_not_counted_as_a_lost_scope()
    {
        var owner = new FakeCapture(ceiling: 1);
        owner.Ledger.Register(new FakeCapture());

        owner.Ledger.Register(new FakeCapture());

        // Nothing is lost by refusing a live registration: the worker's spans
        // still arrive at scope close.
        Assert.Equal(0, owner.Ledger.RefusedScopeCount);
        Assert.Equal(0, owner.Ledger.RefusedSpanCount);
    }

    [Fact]
    public void An_over_ceiling_batch_is_refused_whole_and_counted()
    {
        var owner = new FakeCapture(ceiling: 1);
        var child = new FakeCapture();
        child.Publish("First");
        child.Publish("Second");
        var registration = owner.Ledger.Register(child);

        owner.Ledger.Adopt(child, registration);

        Assert.Empty(Reported(owner.Ledger));
        Assert.Equal(1, owner.Ledger.RefusedScopeCount);
        Assert.Equal(2, owner.Ledger.RefusedSpanCount);
    }

    [Fact]
    public void The_ceiling_of_the_receiving_ledger_bounds_a_whole_chain()
    {
        var owner = new FakeCapture(ceiling: 2);
        var child = new FakeCapture(ceiling: 2);
        var grandchild = new FakeCapture();
        child.Publish("NotifyOrderPlaced");
        grandchild.Publish("Send");
        grandchild.Publish("Retry");
        child.Ledger.Adopt(grandchild, child.Ledger.Register(grandchild));

        owner.Ledger.Adopt(child, owner.Ledger.Register(child));

        // Three spans arriving at a ceiling of two: refused whole, at the hop
        // where it stops fitting, rather than stranding the grandchild.
        Assert.Empty(Reported(owner.Ledger));
        Assert.Equal(1, owner.Ledger.RefusedScopeCount);
        Assert.Equal(3, owner.Ledger.RefusedSpanCount);
    }

    [Fact]
    public void A_later_smaller_batch_still_fits_after_a_refusal()
    {
        var owner = new FakeCapture(ceiling: 2);
        var big = new FakeCapture();
        big.Publish("A");
        big.Publish("B");
        big.Publish("C");
        owner.Ledger.Adopt(big, owner.Ledger.Register(big));

        var small = new FakeCapture();
        small.Publish("D");
        owner.Ledger.Adopt(small, owner.Ledger.Register(small));

        Assert.Equal(["D"], Reported(owner.Ledger));
        Assert.Equal(1, owner.Ledger.RefusedScopeCount);
    }

    [Fact]
    public void An_empty_batch_is_neither_adopted_nor_refused()
    {
        var owner = new FakeCapture(ceiling: 1);
        var child = new FakeCapture();

        owner.Ledger.Adopt(child, owner.Ledger.Register(child));

        Assert.Empty(Reported(owner.Ledger));
        Assert.Equal(0, owner.Ledger.RefusedScopeCount);
        Assert.True(owner.Ledger.IsEmpty);
    }

    [Fact]
    public void A_collected_child_contributes_nothing_and_frees_its_slot()
    {
        var owner = new FakeCapture(ceiling: 1);
        RegisterCollectableChild(owner);

        // Collecting an unreferenced worker deliberately is the assertion.
#pragma warning disable S1215
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
#pragma warning restore S1215

        Assert.Empty(Reported(owner.Ledger));
        Assert.Equal(0, owner.Ledger.LiveCount);
        Assert.NotNull(owner.Ledger.Register(new FakeCapture()));
    }

    [Fact]
    public void A_null_child_is_rejected()
    {
        var owner = new FakeCapture();

        Assert.Throws<ArgumentNullException>(
            () => owner.Ledger.Register(null!));
        Assert.Throws<ArgumentNullException>(
            () => owner.Ledger.Adopt(null!, null));
    }

    [Fact]
    public void A_capture_cannot_register_itself()
    {
        var owner = new FakeCapture();

        Assert.Throws<ArgumentException>(
            () => owner.Ledger.Register(owner));
    }

    [Fact]
    public void A_non_positive_ceiling_is_rejected()
    {
        var owner = new FakeCapture();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new AdoptionLedger(owner, 0));
        Assert.Throws<ArgumentNullException>(
            () => new AdoptionLedger(null!));
    }

    [Fact]
    public void The_invariant_holds_across_registration_adoption_and_refusal()
    {
        var owner = new FakeCapture(ceiling: 1);
        Assert.True(owner.Ledger.Invariant());

        var fits = new FakeCapture();
        fits.Publish("A");
        owner.Ledger.Adopt(fits, owner.Ledger.Register(fits));
        Assert.True(owner.Ledger.Invariant());

        var refused = new FakeCapture();
        refused.Publish("B");
        owner.Ledger.Adopt(refused, owner.Ledger.Register(refused));
        Assert.True(owner.Ledger.Invariant());
    }

    /// <summary>
    /// Registers a child that nothing else references, so the collector can
    /// take it — kept in its own method so no local on the test's frame keeps
    /// it alive.
    /// </summary>
    private static void RegisterCollectableChild(FakeCapture owner)
    {
        var child = new FakeCapture();
        child.Publish("Send");
        owner.Ledger.Register(child);
    }

    private static IReadOnlyList<string> Reported(AdoptionLedger ledger)
    {
        var events = new List<TraceEvent>();
        ledger.CollectReportable(events);
        var names = new List<string>(events.Count);
        for (var i = 0; i < events.Count; i++)
        {
            if (events[i] is EnterEvent enter)
            {
                names.Add(enter.Signature.MethodName);
            }
        }

        return names;
    }

    private sealed class FakeCapture : IReportableCapture
    {
        private static int _spanCounter;

        private readonly List<TraceEvent> _events = [];

        internal FakeCapture(int ceiling = AdoptionLedger.DefaultCeiling)
        {
            Ledger = new AdoptionLedger(this, ceiling);
        }

        internal AdoptionLedger Ledger { get; }

        internal void Publish(string methodName)
        {
            var handle = Interlocked.Increment(ref _spanCounter);
            _events.Add(TestEvents.Enter(
                handle, -1, handle,
                new MethodSignature("Svc", methodName, [])));
        }

        public void CollectReportable(List<TraceEvent> into)
        {
            into.AddRange(_events);
            Ledger.CollectReportable(into);
        }
    }
}
