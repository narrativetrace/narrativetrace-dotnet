// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Turns a declarative <see cref="GraphCase"/> into a live object graph.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: The corpus stays data — this runtime copies <c>graphs.json</c> verbatim and writes its own
/// builder. Only this class knows what a boxed <see cref="Nullable{T}"/>, a
/// <see cref="System.Runtime.CompilerServices.StrongBox{T}"/> or a <see cref="Task{TResult}"/> is.
/// </para>
/// <para>
/// @llmNote <c>layers</c> is applied innermost-first, so <c>["optional","map"]</c> is a map holding
/// an optional. Every shape whose case says <c>payload: "secret-record"</c> carries a
/// <see cref="Secret"/> whose <c>[NotTraced]</c> component holds the caller's sentinel token, so the
/// redaction oracle can look for that token in every byte of every output.
/// </para>
/// <para>
/// @edgeCase Java's wrapper vocabulary does not map onto .NET one-to-one — see the per-layer notes
/// on <see cref="Wrap"/>. Where no .NET type plays the same role (<c>optional</c>: a boxed
/// <see cref="Nullable{T}"/> only exists for value types and is never a wrapper the renderer can
/// see — confirmed by <c>ValueRendererWrapperTests</c> — and the BCL has no reference-typed
/// <c>Option&lt;T&gt;</c>), the layer is identity: it changes nothing, which is itself the correct,
/// evidenced answer rather than a fabricated substitute.
/// </para>
/// </remarks>
public static class HostileGraphs
{
    /// <summary>
    /// Graph-case ids this runtime never renders in-process: rendering them recurses through
    /// <see cref="object.ToString"/> until the CLR raises an unmanaged, uncatchable
    /// <see cref="StackOverflowException"/> that tears down the whole test process rather than
    /// failing one test. See <see cref="HostileMembers.Recursing"/>.
    /// </summary>
    public static readonly IReadOnlyCollection<string> NeverRendered = new HashSet<string> { "recursive-to-string" };

    /// <summary>A record with one redacted component — the shape a leak was found in upstream.</summary>
    /// <param name="Label">Rendered normally, so an output with no label is empty rather than redacted.</param>
    /// <param name="SentinelValue">The sentinel; must reach no byte of any output.</param>
    public sealed record Secret(string Label, [property: NotTraced] string SentinelValue);

    /// <summary>A one-component record, for the <c>holder</c> layer.</summary>
    public sealed record Holder(object? Held);

    /// <summary>A two-component record, for the <c>record</c> layer.</summary>
    public sealed record Wrapped(string Label, object? Payload);

    /// <summary>Builds the graph a case describes, planting <paramref name="sentinel"/> wherever the case says.</summary>
    public static object Build(GraphCase graphCase, string sentinel)
    {
        object payload = graphCase.CarriesSecret ? BuildSecret(sentinel) : "no-payload";
        return graphCase.Kind is null
            ? Stack(payload, graphCase.Layers)
            : ByKind(graphCase, payload, sentinel);
    }

    /// <summary>The sentinel-bearing record, behind <c>[NotTraced]</c>.</summary>
    public static Secret BuildSecret(string sentinel) => new("visible-label", sentinel);

    private static object ByKind(GraphCase graphCase, object payload, string sentinel) => graphCase.Kind switch
    {
        "repeatLayer" => Stack(payload, Repeated(graphCase.Layer!, graphCase.N)),
        "width" => Wide(graphCase.Container!, graphCase.N, payload),
        "cycle" => Ring(graphCase.N, sentinel),
        "selfInCollection" => SelfReferencing(graphCase.Container!, payload),
        "diamond" => Diamond(payload),
        "hostileMember" => Hostile(graphCase.Member!, sentinel),
        "manyFields" => new HostileMembers.ManyFields(BuildSecret(sentinel)),
        "emptyContainers" => EmptyContainers(),
        "future" => Future(graphCase.State, sentinel),
        "throwable" => Throwable(graphCase.State, graphCase.N),
        "curatedToString" => new HostileMembers.CuratedToStringRecord(sentinel),
        "curatedToStringNested" => new HostileMembers.CuratedToStringRecordNested(
            "visible-label", new HostileMembers.CuratedToStringRecord(sentinel)),
        "mapKey" => new Dictionary<HostileMembers.CuratedToStringRecord, string>
        {
            [new HostileMembers.CuratedToStringRecord(sentinel)] = "value",
        },
        "throwingSummary" => new HostileMembers.ThrowingSummaryHolder(sentinel),
        _ => throw new ArgumentException($"unknown graph kind: {graphCase.Kind}"),
    };

    private static List<string> Repeated(string layer, int count) => Enumerable.Repeat(layer, count).ToList();

    private static object Stack(object payload, IReadOnlyList<string> layers)
    {
        object current = payload;
        foreach (var layer in layers)
            current = Wrap(layer, current);
        return current;
    }

    /// <summary>
    /// One wrapper level, mapped from Java's vocabulary onto the nearest .NET equivalent:
    /// <c>atomicReference</c> → <see cref="System.Runtime.CompilerServices.StrongBox{T}"/> (the
    /// BCL's only general-purpose mutable reference cell); <c>atomicReferenceArray</c> → an array of
    /// those boxes (no <c>AtomicReferenceArray</c> type exists in .NET); <c>entryValue</c>/
    /// <c>entryKey</c> → <see cref="KeyValuePair{TKey,TValue}"/> (the BCL's <c>Map.Entry</c>);
    /// <c>future</c> → <see cref="Task{TResult}"/>; <c>optional</c> → identity, see the type remarks.
    /// </summary>
    private static object Wrap(string layer, object inner) => layer switch
    {
        "optional" => inner,
        "atomicReference" => new System.Runtime.CompilerServices.StrongBox<object>(inner),
        "atomicReferenceArray" => new[] { new System.Runtime.CompilerServices.StrongBox<object>(inner) },
        "entryValue" => new KeyValuePair<object, object>("key", inner),
        "entryKey" => new KeyValuePair<object, object>(inner, "value"),
        "future" => Task.FromResult(inner),
        "list" => new List<object> { inner },
        "array" => new object[] { inner },
        "map" => SingleEntryMap(inner),
        "record" => new Wrapped("wrapper", inner),
        "holder" => new Holder(inner),
        _ => throw new ArgumentException($"unknown layer: {layer}"),
    };

    private static Dictionary<string, object?> SingleEntryMap(object inner) => new() { ["key"] = inner };

    /// <summary>One container of <paramref name="count"/> elements, the payload last so truncation cannot hide it.</summary>
    private static object Wide(string container, int count, object payload)
    {
        var elements = new List<object?>(count + 1);
        for (var i = 0; i < count; i++)
            elements.Add(container == "listWithNulls" ? null : $"filler-{i}");
        elements.Add(payload);

        return container switch
        {
            "list" or "listWithNulls" => elements,
            "array" => elements.ToArray(),
            "map" => IndexedMap(elements),
            _ => throw new ArgumentException($"unknown container: {container}"),
        };
    }

    private static Dictionary<string, object?> IndexedMap(List<object?> elements)
    {
        var map = new Dictionary<string, object?>();
        for (var i = 0; i < elements.Count; i++)
            map[$"key-{i}"] = elements[i];
        return map;
    }

    /// <summary>A ring of <paramref name="length"/> nodes; length 1 is an object that holds itself.</summary>
    private static object Ring(int length, string sentinel)
    {
        var nodes = new List<HostileMembers.Ring>(length);
        for (var i = 0; i < length; i++)
            nodes.Add(new HostileMembers.Ring(BuildSecret(sentinel)));
        for (var i = 0; i < length; i++)
            nodes[i].LinkTo(nodes[(i + 1) % length]);
        return nodes[0];
    }

    private static object SelfReferencing(string container, object payload) => container switch
    {
        "list" => SelfHoldingList(payload),
        "map" => SelfHoldingMap(payload, asKey: false),
        "mapKey" => SelfHoldingMap(payload, asKey: true),
        "array" => SelfHoldingArray(payload),
        "atomicReferenceArray" => SelfHoldingAtomicArray(payload),
        "entry" => SelfHoldingEntry(payload),
        _ => throw new ArgumentException($"unknown container: {container}"),
    };

    private static object SelfHoldingList(object payload)
    {
        var list = new List<object> { payload };
        list.Add(list);
        return list;
    }

    private static object SelfHoldingMap(object payload, bool asKey)
    {
        var map = new Dictionary<object, object> { ["payload"] = payload };
        if (asKey)
        {
            map[map] = "self-as-key";
        }
        else
        {
            map["self"] = map;
        }

        return map;
    }

    private static object SelfHoldingArray(object payload)
    {
        var array = new object[2];
        array[0] = payload;
        array[1] = array;
        return array;
    }

    private static object SelfHoldingAtomicArray(object payload)
    {
        var array = new System.Runtime.CompilerServices.StrongBox<object>?[2];
        array[0] = new System.Runtime.CompilerServices.StrongBox<object>(payload);
        var loopback = new System.Runtime.CompilerServices.StrongBox<object>();
        array[1] = loopback;
        loopback.Value = array;
        return array;
    }

    private static object SelfHoldingEntry(object payload)
    {
        var entry = new HostileMembers.MutableEntry { Key = payload, Value = "placeholder" };
        entry.Value = entry;
        return entry;
    }

    /// <summary>The same object twice by different paths: shared, not cyclic — a cycle guard must allow it.</summary>
    private static object Diamond(object payload)
    {
        var shared = new Holder(payload);
        return new List<object> { new Wrapped("left", shared), new Wrapped("right", shared) };
    }

    private static object Hostile(string member, string sentinel)
    {
        var held = BuildSecret(sentinel);
        return member switch
        {
            "toStringThrows" => new HostileMembers.Throwing(held),
            "toStringThrowsWithPayload" => new HostileMembers.ThrowingWithPayload(held),
            "toStringRecurses" => new HostileMembers.Recursing(held),
            "toStringBlocks" => new HostileMembers.Blocking(held),
            "toStringHuge" => new HostileMembers.Huge(held),
            "toStringNull" => new HostileMembers.NullReturning(held),
            "hashCodeThrows" => new HostileMembers.HashThrowing(held),
            "equalsThrows" => new HostileMembers.EqualsThrowing(held),
            "getterThrows" => new HostileMembers.GetterThrowing(held),
            "accessorThrows" => new HostileMembers.AccessorThrowing(held, "label"),
            "hostileKeyNames" => HostileMembers.HostileKeyNames(held),
            _ => throw new ArgumentException($"unknown hostile member: {member}"),
        };
    }

    private static object EmptyContainers() => new Dictionary<string, object?>
    {
        ["list"] = new List<object>(),
        ["array"] = Array.Empty<object>(),
        ["map"] = new Dictionary<string, object?>(),
        ["nullable"] = null,
        ["atomicReferenceArray"] = Array.Empty<System.Runtime.CompilerServices.StrongBox<object>>(),
        ["string"] = "",
    };

    private static object Future(string? state, string sentinel) => state switch
    {
        "pending" => new TaskCompletionSource<object>().Task,
        "cancelled" => CancelledTask(),
        _ => Task.FromException<object>(new InvalidOperationException($"lookup failed for {sentinel}")),
    };

    private static Task<object> CancelledTask()
    {
        var source = new TaskCompletionSource<object>();
        source.SetCanceled();
        return source.Task;
    }

    /// <summary>
    /// An exception carrying prose. Deliberately not the sentinel: an exception message is text the
    /// application wrote, and showing it is the renderer's job, so a containment assertion here
    /// would pin the opposite of the contract.
    /// </summary>
    private static object Throwable(string? state, int depth)
    {
        Exception current = new InvalidOperationException("payment declined for card 4111");
        if (state == "suppressed")
        {
            // .NET has no AggregateException.addSuppressed() sibling that mutates in place the way
            // Java's Throwable.addSuppressed does; the closest shape is an inner exception that no
            // ordinary cause-chain walk (InnerException) reaches — modelled with AggregateException,
            // whose InnerExceptions collection is exactly that: additional exceptions no single
            // InnerException walk would find via .InnerException alone.
            return new AggregateException("payment declined", current);
        }

        for (var i = 0; i < depth; i++)
            current = new InvalidOperationException($"layer {i}", current);
        return current;
    }

    /// <summary>
    /// The fixture graphs <c>templates.json</c> resolves against, by the name its <c>values</c>
    /// field carries. Every one plants the caller's sentinel behind either <c>[NotTraced]</c> or a
    /// deny-listed property name, so a template that names the path must render the marker instead.
    /// </summary>
    /// <remarks>
    /// The last three are <em>scalars</em>, not graphs (mirrors java's <c>HostileGraphs</c>):
    /// every fixture above them is an object, which makes a template name a
    /// property path — the placeholder production that was already correct. The production that
    /// leaked is a bare key naming a value directly, which had no fixture to be
    /// exercised with at all — how <c>{password}</c> printing a password survived a suite pointed
    /// straight at it. <c>newline-scalar</c> is the one carrying no sentinel: its value is not
    /// secret and is meant to be shown, and what must not survive is its raw line break, which a
    /// containment oracle cannot assert on its own — see <c>ScalarPlaceholderRedactionTests</c> for
    /// the unit-level escaping pin.
    /// </remarks>
    public static Dictionary<string, object> TemplateValues(string? name, string sentinel) => (name ?? "card") switch
    {
        "card" => new Dictionary<string, object> { ["card"] = new Card("4111", sentinel) },
        "user" => new Dictionary<string, object> { ["user"] = new Credentials("ada", sentinel) },
        "order" => new Dictionary<string, object> { ["order"] = new Order("order-42", new Card("4000", sentinel)) },
        "deep" => new Dictionary<string, object> { ["a"] = new DepthOne(new DepthTwo(new DepthThree(new DepthFour(sentinel)))) },
        "unicode" => new Dictionary<string, object> { ["café"] = new UnicodeFixture("plain", sentinel) },
        "wide" => new Dictionary<string, object> { ["wide"] = new WideFields("1", "2", "3", "4", "5", sentinel) },
        "chain" => new Dictionary<string, object> { ["chain"] = Chain(sentinel) },
        "password-scalar" => new Dictionary<string, object> { ["password"] = sentinel },
        "jwt-scalar" => new Dictionary<string, object> { ["value"] = Jwt(sentinel) },
        "newline-scalar" => new Dictionary<string, object> { ["comment"] = "note" + (char)0x000a + "## forged" },
        _ => throw new ArgumentException($"unknown template fixture: {name}"),
    };

    /// <summary>
    /// A JWT whose payload segment is the sentinel, so the value axis has a shape to recognize and
    /// the containment oracle still knows which bytes must not appear.
    /// </summary>
    /// <remarks>
    /// The key holding it is <c>value</c>, deliberately a name no deny-list knows. Under
    /// <c>token</c> the name axis would answer first and the case would prove nothing about the
    /// shape — which is the entire reason the value axis exists.
    /// </remarks>
    private static string Jwt(string sentinel) => "eyJhbGciOiJIUzI1NiJ9." + sentinel + ".c2lnbmF0dXJl";

    /// <summary>The dogfood shape: a payment card whose verification code is annotated out of every output.</summary>
    public sealed record Card(string Number, [property: NotTraced] string Cvv);

    /// <summary>A card one level down, so a template can name a redacted segment mid-path.</summary>
    public sealed record Order(string Id, Card Card);

    /// <summary>A bean whose property name the deny-list knows, with no annotation involved.</summary>
    public sealed record Credentials(string Name, string Password)
    {
        /// <summary>The deny-list matches <c>Secret</c> by name, exactly as it matches <c>Password</c>.</summary>
        public string Secret => Password;
    }

    /// <summary>Four records deep, so <c>{a.b.c.d.secret}</c> names a redacted leaf and nothing shorter.</summary>
    public sealed record DepthOne(DepthTwo B);

    /// <summary>The second level of the <c>deep</c> template fixture.</summary>
    public sealed record DepthTwo(DepthThree C);

    /// <summary>The third level of the <c>deep</c> template fixture.</summary>
    public sealed record DepthThree(DepthFour D);

    /// <summary>The redacted leaf of the <c>deep</c> template fixture.</summary>
    public sealed record DepthFour([property: NotTraced] string Secret);

    /// <summary>Identifier segments outside ASCII, so a path grammar cannot assume <c>[A-Za-z_]</c>.</summary>
    public sealed record UnicodeFixture(string Naive, [property: NotTraced] string Secret);

    /// <summary>
    /// A record whose redacted component sits past <c>ValueRenderer</c>'s five-field cap
    /// (<c>RenderOptions.MaxObjectKeys</c>), so the safe rendering truncates the component away and
    /// carries no redaction marker at all.
    /// </summary>
    /// <remarks>
    /// This is the shape the underlying defect found: a resolver that reads "no marker in the safe form" as
    /// "nothing is hidden, the value's own <c>ToString()</c> may stand" prints every component here,
    /// including this one. Absence of the marker means "nothing is hidden" and "the renderer did not
    /// look" alike.
    /// </remarks>
    public sealed record WideFields(
        string One, string Two, string Three, string Four, string Five,
        [property: NotTraced] string Six);

    /// <summary>One link of a chain longer than <c>ValueRenderer</c>'s depth cap (<c>RenderOptions.MaxDepth</c>).</summary>
    public sealed record Link(object Next);

    /// <summary>
    /// A chain nested deeper than <c>ValueRenderer</c>'s depth cap, with a redacted leaf at the
    /// bottom: the second entrance to the same leak class as <see cref="WideFields"/>. The walk stops at
    /// the cap and never reaches the <c>[NotTraced]</c> component, so the safe rendering carries no
    /// marker — while the chain's own <c>ToString()</c> prints the whole thing.
    /// </summary>
    private static Link Chain(string sentinel)
    {
        object link = new Card("4111", sentinel);
        for (var i = 0; i < 6; i++)
        {
            link = new Link(link);
        }

        return (Link)link;
    }
}
