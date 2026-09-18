// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections;
using System.Dynamic;
using System.Linq.Expressions;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NarrativeTrace.Core.Annotation;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Pins the owner's rendering rule — a value renderer reads a value's STATE and never runs
/// its own behaviour — as a family of pending regression tests, one per site the rule covers.
/// </summary>
/// <remarks>
/// <para>
/// The rule: rendering reads fields (and, for an auto-property, the compiler-generated
/// backing field) and never runs a getter with a body, a method, a composite's
/// <c>ToString()</c>, an <see cref="IEnumerable"/> walk of a user type, <c>Equals</c>/
/// <c>GetHashCode</c> via <see cref="Dictionary{TKey,TValue}"/>/<see cref="HashSet{T}"/>,
/// <c>IComparable</c> via sorting, an indexer, or <c>dynamic</c>. The only user code
/// rendering may run is the two documented hooks (the narrative-summary hook and a
/// stateless leaf's <c>ToString()</c>), and only under the rendering guard. Collections are
/// walked only when the runtime type is exactly a framework collection type — never a user
/// subclass, never a user <see cref="IEnumerable"/> — with two exceptions: a user subclass of
/// a framework collection is walked through the framework ancestor's own state/enumerator
/// (never the subclass's override), and a user type may opt a collection back in with a third,
/// declared hook.
/// </para>
/// <para>
/// Most facts below are RED against today's code and carry
/// <see cref="FactAttribute.Skip"/> for that reason — each records the failure it showed
/// before being marked pending. A handful already hold today, for reasons documented on each
/// one; those stay live as regression guards. <c>NeverTrustToStringPinningTests</c> (the
/// security-fuzz suite) already pins "a composite's own <c>ToString()</c> is never trusted";
/// this file's composite tests use a different technique (invocation-counting spies rather
/// than a curated-output corpus) and cover different members (<c>Equals</c>,
/// <c>GetHashCode</c>, <c>CompareTo</c>, an indexer, <c>dynamic</c>) that suite does not touch,
/// so nothing here duplicates it.
/// </para>
/// </remarks>
public class RenderReadsStateTests
{
    private const string Pending =
        "pending: rendering reads state, never runs behaviour — see the rendering rule in the "
        + "repository's agent guide";

    // ------------------------------------------------------------------
    // Group 1 — a property's own getter body is never the source of truth.
    // ------------------------------------------------------------------

    private sealed class CountingValue
    {
        public int Reads;
        private readonly int _value;

        public CountingValue(int value)
        {
            _value = value;
        }

        // Not a true `{ get; }` auto-property any more — a getter with a body can't be —
        // but it models exactly what an auto-property compiles to: a private backing field
        // plus a getter. The side effect stands in for "any code a getter could run."
        public int Value
        {
            get
            {
                Reads++;
                return _value;
            }
        }
    }

    [Fact]
    public void An_auto_propertys_side_effecting_getter_is_never_invoked_by_rendering()
    {
        // RED against today's code: TypeShape.MemberSlot.Read calls PropertyInfo.GetValue,
        // which runs the getter body. Observed: rendered == "CountingValue{Value: 42}" but
        // obj.Reads == 1, not 0.
        var obj = new CountingValue(42);

        var rendered = ValueRenderer.Render(obj);

        Assert.Equal(0, obj.Reads);
        Assert.Contains("42", rendered, StringComparison.Ordinal);
    }

    [Property]
    public bool Rendering_never_increments_a_propertys_read_counter(int value)
    {
        // Same law as above, generalized over arbitrary backing values via FsCheck. RED
        // against today's code for the same reason (PropertyInfo.GetValue runs the getter).
        var obj = new CountingValue(value);

        ValueRenderer.Render(obj);

        return obj.Reads == 0;
    }

    private sealed class Combustible
    {
        public readonly int RealValue = 7;

        public int Value => throw new InvalidOperationException("getter blew up");
    }

    [Fact]
    public void A_throwing_getter_still_renders_the_backing_fields_value_with_no_failure_marker()
    {
        // RED against today's code. Observed: rendered contains
        // "Value: <error: InvalidOperationException>" — the getter is invoked, it throws,
        // RenderMember's guard degrades to a typed error marker. Desired: the getter is never
        // called at all — the field renders its real value, no marker.
        var obj = new Combustible();

        var rendered = ValueRenderer.Render(obj);

        Assert.DoesNotContain("<error", rendered, StringComparison.Ordinal);
        Assert.Contains("7", rendered, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Group 2 — a composite's own ToString/Equals/GetHashCode/CompareTo/indexer/dynamic
    // machinery is never invoked by rendering.
    // ------------------------------------------------------------------

    private sealed class SpiedComposite
    {
        public int ToStringCalls;
        public int Id { get; set; } = 1;

        public override string ToString()
        {
            ToStringCalls++;
            return "SPIED";
        }
    }

    [Fact]
    public void Rendering_a_composite_with_public_state_never_invokes_its_own_ToString()
    {
        // LIVE today: RenderShaped only reaches SafeToString (the sanctioned stateless-leaf
        // hook) when the type has zero public members; a composite with public state always
        // takes the RenderObject/field-walk branch instead, which never touches ToString().
        var obj = new SpiedComposite();

        var rendered = ValueRenderer.Render(obj);

        Assert.Equal(0, obj.ToStringCalls);
        Assert.DoesNotContain("SPIED", rendered, StringComparison.Ordinal);
    }

    private sealed class HostileKey
    {
        public int EqualsCalls;
        public int HashCalls;
        private readonly string _id;

        public HostileKey(string id)
        {
            _id = id;
        }

        public override bool Equals(object? obj)
        {
            EqualsCalls++;
            return obj is HostileKey other && other._id == _id;
        }

        public override int GetHashCode()
        {
            HashCalls++;
            return _id.GetHashCode();
        }
    }

    private sealed class HasDictionaryMember
    {
        public Dictionary<HostileKey, string> Map { get; } = new();
    }

    [Fact]
    public void Rendering_never_invokes_a_keys_overridden_Equals_or_GetHashCode_through_a_dictionary_member()
    {
        // LIVE today: RenderDictionary walks the DictionaryEntry pairs sequentially — a
        // structural enumeration, never a hash lookup — so it never calls the key's own
        // Equals/GetHashCode. Building the dictionary itself does call them (insertion needs
        // to bucket the key), so the counters are reset AFTER construction, before rendering.
        var holder = new HasDictionaryMember();
        var keyA = new HostileKey("a");
        var keyB = new HostileKey("b");
        holder.Map[keyA] = "first";
        holder.Map[keyB] = "second";
        keyA.EqualsCalls = 0;
        keyA.HashCalls = 0;
        keyB.EqualsCalls = 0;
        keyB.HashCalls = 0;

        ValueRenderer.Render(holder);

        Assert.Equal(0, keyA.EqualsCalls);
        Assert.Equal(0, keyA.HashCalls);
        Assert.Equal(0, keyB.EqualsCalls);
        Assert.Equal(0, keyB.HashCalls);
    }

    private sealed class HasHashSetMember
    {
        public HashSet<HostileKey> Set { get; } = new();
    }

    [Fact]
    public void Rendering_never_invokes_an_elements_overridden_Equals_or_GetHashCode_through_a_hashset_member()
    {
        // LIVE today: HashSet<T> implements the non-generic IEnumerable rendering walks
        // sequentially, same reasoning as the dictionary case above.
        var holder = new HasHashSetMember();
        var elementA = new HostileKey("a");
        var elementB = new HostileKey("b");
        holder.Set.Add(elementA);
        holder.Set.Add(elementB);
        elementA.EqualsCalls = 0;
        elementA.HashCalls = 0;
        elementB.EqualsCalls = 0;
        elementB.HashCalls = 0;

        ValueRenderer.Render(holder);

        Assert.Equal(0, elementA.EqualsCalls);
        Assert.Equal(0, elementA.HashCalls);
        Assert.Equal(0, elementB.EqualsCalls);
        Assert.Equal(0, elementB.HashCalls);
    }

    private sealed class HostileComparable : IComparable<HostileComparable>
    {
        public int CompareCalls;
        public int Value { get; }

        public HostileComparable(int value)
        {
            Value = value;
        }

        public int CompareTo(HostileComparable? other)
        {
            CompareCalls++;
            return Value.CompareTo(other?.Value ?? 0);
        }
    }

    private sealed class HasComparableListMember
    {
        public List<HostileComparable> Items { get; } = new();
    }

    [Fact]
    public void Rendering_never_invokes_a_composites_overridden_CompareTo_through_a_sorted_list_member()
    {
        // LIVE today: rendering an IEnumerable member never sorts it — the only List<T>.Sort
        // call anywhere in the renderer sorts the renderer's OWN signature-key strings, never
        // a user IComparable, and it is not on this path at all.
        var holder = new HasComparableListMember();
        var itemA = new HostileComparable(3);
        var itemB = new HostileComparable(1);
        holder.Items.Add(itemA);
        holder.Items.Add(itemB);

        ValueRenderer.Render(holder);

        Assert.Equal(0, itemA.CompareCalls);
        Assert.Equal(0, itemB.CompareCalls);
    }

    private sealed class IndexerOnly
    {
        public int this[int i] => i * 2;
    }

    [Fact]
    public void An_indexer_is_never_invoked_and_never_appears_as_a_rendered_member()
    {
        // RED against today's code. Observed: TypeShape.PublicPropertiesOf picks up the
        // indexer (a public instance property named "Item"), and MemberSlot.Read calls
        // PropertyInfo.GetValue(target) with no index arguments, throwing
        // TargetParameterCountException — caught by RenderMember's guard and shown as
        // "Item: <error: TargetParameterCountException>". Desired: an indexer is not state and
        // is never a candidate member at all.
        var rendered = ValueRenderer.Render(new IndexerOnly());

        Assert.DoesNotContain("Item", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("<error", rendered, StringComparison.Ordinal);
    }

    private sealed class SpyDynamicProvider : IDynamicMetaObjectProvider
    {
        public int GetMetaObjectCalls;

        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            GetMetaObjectCalls++;
            throw new InvalidOperationException("the dynamic binder must never run during rendering");
        }
    }

    [Fact]
    public void A_types_dynamic_binder_is_never_invoked_by_rendering()
    {
        // LIVE today: ValueRenderer's reflective walk never special-cases
        // IDynamicMetaObjectProvider, so nothing ever calls GetMetaObject — confirmed rather
        // than assumed, since a future feature could change that.
        var obj = new SpyDynamicProvider();

        ValueRenderer.Render(obj);

        Assert.Equal(0, obj.GetMetaObjectCalls);
    }

    // ------------------------------------------------------------------
    // Group 3 — collections: walked only by platform origin, never a user override.
    // ------------------------------------------------------------------

    private sealed class HostileListSubclass : List<int>, IEnumerable<int>
    {
        public int GenericGetEnumeratorCalls;

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            GenericGetEnumeratorCalls++;
            throw new InvalidOperationException("hostile generic enumerator");
        }
    }

    [Fact]
    public void A_subclass_of_List_reimplementing_its_generic_enumerator_still_renders_elements_through_the_base_enumerator()
    {
        // RED against today's code — and the honest, MEASURED answer for .NET, correcting a
        // plausible-looking wrong guess: ValueRenderer pattern-matches on the non-generic
        // System.Collections.IEnumerable and calls seq.GetEnumerator() through that interface,
        // so the theory "List<T>.GetEnumerator() is not virtual, so a subclass re-implementing
        // only the GENERIC IEnumerable<T>.GetEnumerator() can't be reached from a non-generic
        // call site" looks sound on paper. It is not: observed, GenericGetEnumeratorCalls == 1
        // — the override IS reached. (The sibling Dictionary<K,V> test below measures the
        // opposite for IDictionary, confirming this is a real, List<T>-specific asymmetry, not
        // a mistake in either test.) Desired fix direction: read List<T>'s own state directly
        // (its private _items/_size backing fields via reflection) rather than trusting ANY
        // enumerator — generic or non-generic — a subclass could still be re-implementing.
        var order = new HostileListSubclass { 1, 2, 3 };

        var rendered = ValueRenderer.Render(order);

        Assert.Equal(0, order.GenericGetEnumeratorCalls);
        Assert.Contains("1", rendered, StringComparison.Ordinal);
        Assert.Contains("2", rendered, StringComparison.Ordinal);
        Assert.Contains("3", rendered, StringComparison.Ordinal);
    }

    private sealed class HostileDictionarySubclass
        : Dictionary<string, int>, IEnumerable<KeyValuePair<string, int>>
    {
        public int GenericGetEnumeratorCalls;

        IEnumerator<KeyValuePair<string, int>> IEnumerable<KeyValuePair<string, int>>.GetEnumerator()
        {
            GenericGetEnumeratorCalls++;
            throw new InvalidOperationException("hostile generic enumerator");
        }
    }

    [Fact]
    public void A_subclass_of_Dictionary_reimplementing_its_generic_enumerator_still_renders_entries_through_the_base_enumerator()
    {
        // LIVE today, MEASURED — and, per the sibling List<T> test above, deliberately not
        // assumed from the same reasoning: RenderDictionary calls dict.GetEnumerator() through
        // the non-generic System.Collections.IDictionary (returning IDictionaryEnumerator), a
        // mechanism entirely distinct from IEnumerable<KeyValuePair<,>>.GetEnumerator() — and
        // here, unlike List<T>, that separation actually holds: GenericGetEnumeratorCalls stays
        // 0. The two BCL types are not symmetric; both are measured independently.
        var map = new HostileDictionarySubclass { ["a"] = 1, ["b"] = 2 };

        var rendered = ValueRenderer.Render(map);

        Assert.Equal(0, map.GenericGetEnumeratorCalls);
        Assert.Contains("a=1", rendered, StringComparison.Ordinal);
        Assert.Contains("b=2", rendered, StringComparison.Ordinal);
    }

    private sealed class ListWrapper
    {
        public List<int> Items { get; } = new() { 1, 2, 3 };
    }

    [Fact]
    public void A_wrapper_holding_a_platform_list_in_a_field_renders_as_an_object_whose_field_renders_the_list()
    {
        // LIVE today: an ordinary public property typed as a framework collection already
        // renders as "Wrapper{Items: [1, 2, 3]}" — the shape the rule explicitly preserves.
        var wrapper = new ListWrapper();

        var rendered = ValueRenderer.Render(wrapper);

        Assert.Contains("Items", rendered, StringComparison.Ordinal);
        Assert.Contains("[1, 2, 3]", rendered, StringComparison.Ordinal);
    }

    private sealed class UndeclaredSequence : IEnumerable<int>
    {
        public int GetEnumeratorCalls;
        private readonly int[] _data = [1, 2, 3];

        public IEnumerator<int> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return ((IEnumerable<int>)_data).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void An_undeclared_user_IEnumerable_never_has_its_GetEnumerator_invoked_by_rendering()
    {
        // RED against today's code. Observed: ValueRenderer's RenderComplex dispatches on the
        // bare `is System.Collections.IEnumerable` pattern with no origin check at all, so a
        // plain user-authored IEnumerable<T> (no framework collection ancestor) is fully
        // walked — GetEnumeratorCalls == 1 and the rendered text contains every element.
        // Desired: an undeclared user IEnumerable is never enumerated at all.
        var seq = new UndeclaredSequence();

        ValueRenderer.Render(seq);

        Assert.Equal(0, seq.GetEnumeratorCalls);
    }

    [NarrativeElements]
    private sealed class AnnotatedSequence<T> : IEnumerable<T>
    {
        public int GetEnumeratorCalls;
        private readonly List<T> _data;

        public AnnotatedSequence(IEnumerable<T> data)
        {
            _data = new List<T>(data);
        }

        public IEnumerator<T> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return _data.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void A_type_declaring_the_elements_hook_still_enumerates_up_to_the_collection_cap()
    {
        // LIVE today, but by accident, not by the (nonexistent) hook: every IEnumerable is
        // still blanket-enumerated today regardless of any attribute, and SafeCollect already
        // bounds every walk at RenderOptions.MaxArrayItems (5). Once the hook gating from the
        // previous test lands, THIS assertion is exactly the "declared → still enumerates,
        // capped" half of the rule the fix must preserve — kept live now so a regression in
        // either direction (over-eager uncapped walk, or the fix silently also breaking the
        // declared case) is caught immediately.
        var seq = new AnnotatedSequence<int>(Enumerable.Range(0, 50));

        var rendered = ValueRenderer.Render(seq);

        Assert.Contains("0", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("49", rendered, StringComparison.Ordinal);
        Assert.Contains("50 total", rendered, StringComparison.Ordinal);
    }

    public interface ICollaboratorX
    {
        string Name { get; }
    }

    private sealed class CollaboratorXImpl : ICollaboratorX
    {
        public string Name => "gadget";
    }

    public interface IElementsHolder
    {
        void Hold(object items);
    }

    private sealed class ElementsHolderImpl : IElementsHolder
    {
        public void Hold(object items)
        {
        }
    }

    [Fact]
    public void Enumerating_a_proxied_collection_element_emits_no_phantom_span()
    {
        // LIVE today: RenderingGuard.Enter() wraps the ENTIRE parameter-rendering call in
        // NarrativeInterceptor, not just the direct-property case RenderReentrancyGuardTests
        // already pins — the collection walk this exercises runs fully inside that same guarded
        // scope, so a proxied element's getter reached during enumeration is suppressed exactly
        // like a proxied element reached directly. New coverage: no existing test walks a
        // proxy through a COLLECTION member.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var collaboratorProxy = NarrativeTraceProxy.Create<ICollaboratorX>(new CollaboratorXImpl(), ctx);
        var seq = new AnnotatedSequence<object>([collaboratorProxy]);
        var holder = NarrativeTraceProxy.Create<IElementsHolder>(new ElementsHolderImpl(), ctx);

        holder.Hold(seq);

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Hold", trace.Roots[0].Signature.MethodName);
        Assert.Empty(trace.Roots[0].Children);
    }

    [NarrativeElements]
    private sealed class ThrowingAnnotatedSequence : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator() => throw new InvalidOperationException("boom");

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void A_throwing_enumerator_under_the_elements_hook_degrades_to_a_failure_marker_not_a_crash()
    {
        // LIVE today: a hostile GetEnumerator() that throws immediately is caught by Render's
        // own top-level guard (nothing has been collected yet, so there is no finer-grained
        // boundary to degrade at) and answers the same "<TypeName>" marker every other
        // unrenderable top-level value does.
        var rendered = ValueRenderer.Render(new ThrowingAnnotatedSequence());

        Assert.Equal("<ThrowingAnnotatedSequence>", rendered);
    }

    // ------------------------------------------------------------------
    // Group 4 — a narration template placeholder reads a property's state, not its behaviour.
    // ------------------------------------------------------------------

    public sealed class HostileOrder
    {
        public int Reads;
        private readonly int _total = 42;

        public int Total
        {
            get
            {
                Reads++;
                return _total;
            }
        }
    }

    public interface IOrderNarrator
    {
        [Narrated("Total is {order.Total}")]
        void Announce(HostileOrder order);
    }

    private sealed class OrderNarratorImpl : IOrderNarrator
    {
        public void Announce(HostileOrder order)
        {
        }
    }

    [Fact]
    public void NarrationResolver_resolving_a_property_placeholder_never_invokes_the_propertys_side_effecting_getter()
    {
        // RED against today's code. Observed: narration resolves to "Total is 42" (correct
        // text) but order.Reads == 1 — NarrationResolver.ReadProperty calls
        // PropertyInfo.GetValue, the same accessor-not-state gap as Group 1.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create<IOrderNarrator>(new OrderNarratorImpl(), ctx);
        var order = new HostileOrder();

        proxy.Announce(order);

        Assert.Equal("Total is 42", ctx.CaptureTrace().Roots[0].Signature.Narration);
        Assert.Equal(0, order.Reads);
    }

    // ------------------------------------------------------------------
    // Group 5 — a thrown exception's message is read, not re-entered.
    // ------------------------------------------------------------------

    private sealed class CountingMessageException : Exception
    {
        public int Reads;

        public override string Message
        {
            get
            {
                Reads++;
                return "boom";
            }
        }
    }

    [Fact]
    public void ExceptionMessage_Text_reads_the_message_getter_exactly_once()
    {
        // LIVE today: ExceptionMessage.Of reads exception.Message exactly one time.
        var ex = new CountingMessageException();

        var text = ExceptionMessage.Text(ex);

        Assert.Equal("boom", text);
        Assert.Equal(1, ex.Reads);
    }

    public interface IReentrantCollaborator
    {
        string Touch();
    }

    private sealed class ReentrantCollaboratorImpl : IReentrantCollaborator
    {
        public string Touch() => "touched";
    }

    private sealed class ReentrantException : Exception
    {
        private readonly IReentrantCollaborator _collaborator;

        public ReentrantException(IReentrantCollaborator collaborator)
            : base("boom")
        {
            _collaborator = collaborator;
        }

        public override string Message
        {
            get
            {
                _collaborator.Touch();
                return "boom";
            }
        }
    }

    public interface IFailer
    {
        void Fail();
    }

    private sealed class FailerImpl : IFailer
    {
        private readonly IReentrantCollaborator _collaborator;

        public FailerImpl(IReentrantCollaborator collaborator)
        {
            _collaborator = collaborator;
        }

        public void Fail() => throw new ReentrantException(_collaborator);
    }

    [Fact]
    public void A_thrown_exceptions_overridden_Message_reached_on_the_durable_path_opens_no_phantom_span_for_a_traced_collaborator_it_calls()
    {
        // RED against today's code. Observed: two roots are captured, not one —
        // LoggingNarrativeContext.LogExitError reads ExceptionMessage.Text(error) entirely
        // outside RenderingGuard, so the Message override's call back into the collaborator
        // proxy is not recognized as rendering-in-progress and opens a genuine, real span for
        // it (a second root, since the failing call's own span was already closed by the time
        // LogExitError runs). This is the durable/synchronous path — the crash-safe half of the
        // dual-path pipeline — not just a best-effort export path.
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var collaboratorProxy = NarrativeTraceProxy.Create<IReentrantCollaborator>(
            new ReentrantCollaboratorImpl(), ctx);
        var logging = new LoggingNarrativeContext(ctx, NullLogger.Instance);
        var failer = NarrativeTraceProxy.Create<IFailer>(
            new FailerImpl(collaboratorProxy), logging);

        Assert.Throws<ReentrantException>(() => failer.Fail());

        var trace = ctx.CaptureTrace();
        Assert.Single(trace.Roots);
        Assert.Equal("Fail", trace.Roots[0].Signature.MethodName);
        Assert.Empty(trace.Roots[0].Children);
    }
}
