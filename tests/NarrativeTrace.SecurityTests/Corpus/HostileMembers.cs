// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Collections;
using System.Collections.Specialized;
using NarrativeTrace.Core;
using NarrativeTrace.Core.Annotation;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// Objects whose <c>ToString</c>, <c>GetHashCode</c>, <c>Equals</c> or accessors misbehave — the
/// third-party DTOs a tracing library has no control over.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: These are the shapes the <c>graphs.json</c> <c>hostileMember</c> kind names. Each holds
/// a <see cref="HostileGraphs.Secret"/>, so the redaction oracle applies to all of them: a renderer
/// that falls back to <c>ToString</c> because introspection failed must not print past the
/// redaction, and an exception raised inside instrumentation must not carry the value out in its
/// message.
/// </para>
/// <para>
/// @edgeCase <see cref="Recursing"/> is deliberately never rendered by any property in this suite.
/// Java's Error hierarchy lets a test catch <c>StackOverflowError</c>; the CLR's
/// <see cref="StackOverflowException"/> cannot be caught by any managed handler and always
/// terminates the process (documented in <c>NarrationResolver</c>'s own comments as "a genuine
/// platform divergence, not an oversight"). Rendering this shape in-process would kill the whole
/// test run rather than fail one test, so it exists only so
/// <see cref="HostileCorpusTest.Every_declared_graph_shape_builds"/> can prove the corpus still
/// *builds* it — construction never calls <c>ToString()</c>. See
/// <c>ValueRendererRedactionPropertyTest</c>'s exclusion list.
/// </para>
/// </remarks>
public static class HostileMembers
{
    /// <summary>How long <see cref="Blocking.ToString"/> stalls the renderer.</summary>
    public const int BlockMillis = 250;

    /// <summary>U+200B, a format character no deny-list accepts.</summary>
    public const char ZeroWidthSpace = '​';

    /// <summary>U+0440, the Cyrillic letter that renders identically to a Latin <c>p</c>.</summary>
    public const char CyrillicEr = 'р';

    /// <summary>Size of the string <see cref="Huge.ToString"/> returns.</summary>
    public const int HugeLength = 1024 * 1024;

    /// <summary>A <c>ToString</c> that throws — the commonest hostile DTO.</summary>
    public sealed record Throwing(HostileGraphs.Secret Held)
    {
        /// <inheritdoc />
#pragma warning disable S3877 // the throw is the fixture: a DTO whose ToString refuses
        public override string ToString() => throw new InvalidOperationException("ToString refuses");
#pragma warning restore S3877
    }

    /// <summary>A <c>ToString</c> whose exception message carries the secret out with it.</summary>
    public sealed record ThrowingWithPayload(HostileGraphs.Secret Held)
    {
        /// <inheritdoc />
#pragma warning disable S3877 // the throw is the fixture: a DTO whose ToString refuses
        public override string ToString() =>
            throw new InvalidOperationException("cannot render " + Held.SentinelValue);
#pragma warning restore S3877
    }

    /// <summary>
    /// A <c>ToString</c> that recurses until the stack ends. Never rendered — see the type remarks.
    /// </summary>
    public sealed record Recursing(HostileGraphs.Secret Held)
    {
        /// <inheritdoc />
        public override string ToString() => "recursing " + this;
    }

    /// <summary>A <c>ToString</c> that blocks, bounded, so the time budget is what fails.</summary>
    public sealed record Blocking(HostileGraphs.Secret Held)
    {
        /// <inheritdoc />
        public override string ToString()
        {
            Thread.Sleep(BlockMillis);
            return "eventually";
        }
    }

    /// <summary>A <c>ToString</c> returning a mebibyte — the bounded-output case.</summary>
    public sealed record Huge(HostileGraphs.Secret Held)
    {
        /// <inheritdoc />
        public override string ToString() => new string('z', HugeLength);
    }

    /// <summary>A <c>ToString</c> returning <see langword="null"/> at runtime despite the signature.</summary>
    public sealed record NullReturning(HostileGraphs.Secret Held)
    {
        /// <inheritdoc />
#pragma warning disable CS8764 // deliberately violates the non-null contract to reproduce a hostile DTO
        public override string? ToString() => null;
#pragma warning restore CS8764
    }

    /// <summary>A <c>GetHashCode</c> that throws — breaks any identity or hash set the renderer keeps.</summary>
    public sealed class HashThrowing(HostileGraphs.Secret held)
    {
        /// <summary>The redacted payload.</summary>
        public HostileGraphs.Secret Held { get; } = held;

        /// <inheritdoc />
#pragma warning disable S3877 // the throw is the fixture: a DTO whose GetHashCode refuses
        public override int GetHashCode() => throw new InvalidOperationException("GetHashCode refuses");
#pragma warning restore S3877

        /// <inheritdoc />
        public override bool Equals(object? obj) => ReferenceEquals(this, obj);
    }

    /// <summary>An <c>Equals</c> that throws.</summary>
    public sealed class EqualsThrowing(HostileGraphs.Secret held)
    {
        /// <summary>The redacted payload.</summary>
        public HostileGraphs.Secret Held { get; } = held;

        /// <inheritdoc />
#pragma warning disable S3877 // the throw is the fixture: a DTO whose Equals refuses
        public override bool Equals(object? obj) => throw new InvalidOperationException("Equals refuses");
#pragma warning restore S3877

        /// <inheritdoc />
        public override int GetHashCode() => 1;
    }

    /// <summary>A record component accessor that throws while the renderer is reading members.</summary>
    public sealed class AccessorThrowing(HostileGraphs.Secret held, string label)
    {
        /// <summary>The redacted payload.</summary>
        public HostileGraphs.Secret Held { get; } = held;

        /// <summary>Never returns; the renderer must degrade rather than propagate.</summary>
        public string Label => throw new InvalidOperationException("accessor refuses: " + label);
    }

    /// <summary>
    /// A <c>ToString</c> that forges a narrative line — newline, Markdown fence, JSON structure and
    /// the two non-JSON floating-point spellings, all in one payload — with no public state at all,
    /// so <see cref="TypeShape"/>'s own state read finds nothing to walk and the member-less
    /// path, not a field walk, is what decides this value's rendering. This is the
    /// <c>number-hostile-to-string</c> row.
    /// </summary>
    /// <remarks>
    /// The Java corpus's <c>numberHostileToString</c> row targets an <c>ArrayList</c>-adjacent
    /// bug class specific to that runtime: a <c>java.lang.Number</c> subclass got a scalar fast
    /// path that trusted its own <c>toString()</c> unsanitized. .NET's <c>ValueRenderer</c> has no
    /// equivalent type-hierarchy fast path — no user type can pattern-match into the fixed
    /// <c>int</c>/<c>long</c>/<c>decimal</c>/... branches <c>RenderValue</c> switches on, since C#
    /// pattern matching there is exact-type, not inheritance from a common numeric base the BCL
    /// doesn't have. The honest .NET analogue of "trusted because the walk found no state to
    /// redact" is the member-less path, and this fixture pins where that path now goes: a type
    /// with nothing to walk is not thereby a stateless leaf
    /// (<c>PlatformTypes.IsStatelessLeaf</c>), so the forged line is never produced at all
    /// rather than produced and then escaped.
    /// </remarks>
    public sealed class NumberHostileToString
    {
        // Kept only to match every other hostileMember factory's signature; never read — same
        // shape as the Java fixture's unused `held` field.
#pragma warning disable S4487 // deliberately unread, matching the Java fixture's own note
        private readonly HostileGraphs.Secret _held;
#pragma warning restore S4487

        public NumberHostileToString(HostileGraphs.Secret held)
        {
            _held = held;
        }

        /// <inheritdoc />
        public override string ToString() =>
            "1\n```\n{\"outcome\": \"success\"}\nNaN Infinity -Infinity\n```";
    }

    /// <summary>
    /// A deny-listed field name whose value is a platform type the renderer would otherwise trust
    /// to stringify itself — the <c>platform-type-name-redacted</c> row.
    /// </summary>
    /// <remarks>
    /// The name axis and the platform-type carve-out are two different questions: this record
    /// proves the first is asked, and answered, before the second is ever consulted.
    /// <see cref="ValueRenderer"/>'s per-member guard (<c>IsRedacted</c>) runs before
    /// <c>member.Read</c> is ever called, so a renderer that reached the carve-out first would
    /// print the <see cref="Uri"/>'s own text — sentinel included — because <see cref="Uri"/> is
    /// exactly the kind of type this runtime otherwise renders through its own short native text
    /// (see <see cref="HostileGraphs.PlatformValues"/>).
    /// </remarks>
    public sealed record NamedPlatformValue(Uri Password);

    /// <summary>
    /// A user class named after a platform type (<see cref="System.DateTime"/>) but defined by
    /// application code — the <c>platform-lookalike-walked</c> row.
    /// </summary>
    /// <remarks>
    /// Deliberately not <see cref="System.DateTime"/> itself — this is a distinct, nested type
    /// (<c>HostileMembers.DateTime</c>), unrelated by inheritance. Trust is decided by
    /// <see cref="PlatformTypes.IsPlatformDefined"/>'s declaring-assembly check, never by a class's
    /// own simple name; a name-based test would have trusted this class's <c>ToString()</c> and
    /// printed the field it hides.
    /// </remarks>
#pragma warning disable CA1724 // the name collision with System.DateTime is the fixture
    public sealed class DateTime
#pragma warning restore CA1724
    {
        /// <summary>An ordinary, non-sensitive field.</summary>
        public string Username { get; } = "ada";

        /// <summary>The deny-listed field, matched by name.</summary>
        public string Password { get; }

        public DateTime(string password)
        {
            Password = password;
        }

        /// <inheritdoc />
        public override string ToString() => $"DateTime{{Username={Username}, Password={Password}}}";
    }

    /// <summary>
    /// A user subclass of a platform type (<see cref="Uri"/>, not sealed) — the
    /// <c>platform-subclass-walked</c> row.
    /// </summary>
    /// <remarks>
    /// The most-derived type's own declaring assembly decides trust, never an ancestor's: this
    /// class is declared in the test assembly even though its base is platform-defined, so it must
    /// be walked exactly like <see cref="DateTime"/> above, never trusted merely because
    /// <c>: Uri</c> reaches a platform type one step up. Unlike the collection-origin dispatch (see
    /// <see cref="PlatformTypes"/>), <see cref="ValueRenderer"/> has no scalar carve-out that
    /// pattern-matches <see cref="Uri"/> at all, so this shape reaches the ordinary reflective
    /// object walk the same way any other type with public members does — the override below must
    /// never win over it.
    /// </remarks>
    public sealed class ApplicationUri : Uri
    {
        /// <summary>The deny-listed field, matched by name.</summary>
        public string Password { get; }

        public ApplicationUri(string password)
            : base("https://example.test/resource")
        {
            Password = password;
        }

        /// <inheritdoc />
        public override string ToString() => $"ApplicationUri{{Password={Password}}}";
    }

    /// <summary>A property whose getter throws while the renderer is introspecting it.</summary>
    public sealed class GetterThrowing(HostileGraphs.Secret held)
    {
        /// <summary>
        /// Never returns; the renderer must degrade rather than propagate. Deliberately an instance
        /// property reachable via <c>BindingFlags.Instance</c> reflection, even though it reads no
        /// instance state — the hostile shape being tested is "a getter that throws," not "a getter
        /// that could have been static."
        /// </summary>
#pragma warning disable S2325 // must stay an instance member to be reachable the way ValueRenderer introspects properties
        public string Detail => throw new InvalidOperationException("getter refuses");
#pragma warning restore S2325

        /// <summary>Reachable, and behind <c>[NotTraced]</c> at the next level down.</summary>
        public HostileGraphs.Secret Held { get; } = held;
    }

    /// <summary>
    /// A mutable entry-shaped class — the analogue of Java's <c>AbstractMap.SimpleEntry</c>, which
    /// this runtime needs because <see cref="KeyValuePair{TKey,TValue}"/> is an immutable struct and
    /// cannot be made to hold itself.
    /// </summary>
    public sealed class MutableEntry
    {
        /// <summary>The entry's key, which may be reassigned after construction.</summary>
        public object? Key { get; set; }

        /// <summary>The entry's value, which may be reassigned after construction.</summary>
        public object? Value { get; set; }
    }

    /// <summary>
    /// Hostile *names*, where names actually come from data: dictionary keys.
    /// </summary>
    /// <remarks>
    /// The deny-list matches on the rendered key text, so a key carrying an invisible code point
    /// (U+200B inside the word "password") reads as an ordinary key to a human and matches nothing.
    /// The secret here is protected by <c>[NotTraced]</c> one level down, not by its name — which is
    /// the point.
    /// </remarks>
    public static Dictionary<string, object?> HostileKeyNames(HostileGraphs.Secret held)
    {
        return new Dictionary<string, object?>
        {
            ["pass" + ZeroWidthSpace + "word"] = "visible-but-unmatched",
            ["PASSWORD"] = "upper-case",
            ["pass_word"] = "separated",
            [CyrillicEr + "assword"] = "cyrillic-lookalike",
            ["held"] = held,
        };
    }

    /// <summary>More fields than the renderer's five-field cap, with the secret beyond it.</summary>
    public sealed class ManyFields(HostileGraphs.Secret held)
    {
#pragma warning disable CA1051, S1104 // deliberately public fields — every one exists to be introspected
        public string A = "1";
        public string B = "2";
        public string C = "3";
        public string D = "4";
        public string E = "5";
        public string F = "6";
        public string G = "7";
        public string H = "8";
        public string I = "9";
        public string J = "10";
        public string K = "11";
        public HostileGraphs.Secret Secret = held;
#pragma warning restore CA1051, S1104
    }

    /// <summary>
    /// A record with a deny-listed field (matched by name, not <c>[NotTraced]</c>) whose own
    /// hand-written <c>ToString</c> interpolates it directly.
    /// </summary>
    /// <remarks>
    /// This is the shape the never-trust-ToString invariant pins the absence of: the reflective
    /// walk (which redacts <see cref="Password"/> by name before ever reading it) must win over
    /// this type's own <c>ToString</c> whenever the type has public state — the renderer must
    /// never fall back to this string.
    /// </remarks>
    public sealed record CuratedToStringRecord(string Password)
    {
        /// <inheritdoc />
        public override string ToString() => $"CuratedToStringRecord[password={Password}]";
    }

    /// <summary>
    /// A composite with no sensitive field of its own, whose hand-written <c>ToString</c>
    /// interpolates a nested <see cref="CuratedToStringRecord"/> that does carry one.
    /// </summary>
    public sealed record CuratedToStringRecordNested(string Label, CuratedToStringRecord Nested)
    {
        /// <inheritdoc />
        public override string ToString() => $"CuratedToStringRecordNested[{Label}: {Nested}]";
    }

    /// <summary>The plain-class analogue of <see cref="CuratedToStringRecord"/> — records get the
    /// walk via <c>IsRecord</c>, a plain class via <c>HasPublicMembers</c>; both paths must redact.</summary>
    public sealed class CuratedToStringClass(string password)
    {
        /// <summary>The deny-listed member, matched by name.</summary>
        public string Password { get; } = password;

        /// <inheritdoc />
        public override string ToString() => $"CuratedToStringClass[password={Password}]";
    }

    /// <summary>The plain-class analogue of <see cref="CuratedToStringRecordNested"/>.</summary>
    public sealed class CuratedToStringClassNested(string label, CuratedToStringRecord nested)
    {
        /// <summary>An ordinary, non-sensitive field.</summary>
        public string Label { get; } = label;

        /// <summary>The nested composite carrying the deny-listed field.</summary>
        public CuratedToStringRecord Nested { get; } = nested;

        /// <inheritdoc />
        public override string ToString() => $"CuratedToStringClassNested[{Label}: {Nested}]";
    }

    /// <summary>
    /// A composite whose <c>[NarrativeSummary]</c> member throws, with the sentinel folded into
    /// the exception message so a leak through the message text — not just a captured value —
    /// would be caught by the same containment oracle.
    /// </summary>
    public sealed class ThrowingSummaryHolder(string sentinel)
    {
        /// <summary>An ordinary, non-sensitive field the summary was curated to replace.</summary>
        public string Label { get; } = "visible-label";

        /// <summary>Never returns; the renderer must degrade to a typed placeholder, not this type's fields.</summary>
        [NarrativeSummary]
#pragma warning disable S3877 // the throw is the fixture: a summary that refuses
        public string Summary => throw new InvalidOperationException("summary refused for " + sentinel);
#pragma warning restore S3877
    }

    /// <summary>
    /// The rendering rule (the rendering rule in the repository's agent guide): a record whose accessor counts
    /// its own calls instead of throwing — <c>record-accessor-with-counter</c>. Expected outcome
    /// is rendered without executing: <see cref="Reads"/> stays 0, the backing field
    /// (<c>_held</c>, the sanctioned <c>_camelCase</c> convention <c>TypeShape.MemberSlot</c>
    /// reads directly) supplies the rendered value instead.
    /// </summary>
    public sealed record CountingAccessorRecord
    {
        private readonly HostileGraphs.Secret _held;

        public CountingAccessorRecord(HostileGraphs.Secret held)
        {
            _held = held;
        }

        /// <summary>How many times the hand-written accessor below actually ran.</summary>
        public int Reads;

        /// <summary>
        /// A hand-written accessor standing in for a record component whose generated getter a
        /// hostile author replaced — counts instead of throwing, so a defect here shows up as a
        /// nonzero counter rather than a caught exception a degrading renderer could mask.
        /// </summary>
        public HostileGraphs.Secret Held
        {
            get
            {
                Reads++;
                return _held;
            }
        }
    }

    /// <summary>
    /// The rendering rule: a platform-collection (<see cref="List{T}"/>, the .NET analogue of
    /// Java's <c>ArrayList</c>) subclass whose overridden enumerator both counts its own calls
    /// and refuses — <c>platform-collection-side-effecting-iterator</c>. Expected outcome is
    /// rendered without executing: <see cref="GetEnumeratorCalls"/> stays 0, the elements come
    /// from <see cref="List{T}"/>'s own backing state through the platform ancestor, never this
    /// override — the same shape <c>NarrativeTrace.Core.Tests</c> pins inline
    /// (<c>RenderReadsStateTests.HostileListSubclass</c>), here as a named corpus fixture.
    /// </summary>
    public sealed class SideEffectingIteratorList : List<object?>, IEnumerable<object?>
    {
        /// <summary>How many times the overridden generic enumerator below actually ran.</summary>
        public int GetEnumeratorCalls;

        public SideEffectingIteratorList(HostileGraphs.Secret held)
        {
            Add(held);
        }

        /// <inheritdoc />
        IEnumerator<object?> IEnumerable<object?>.GetEnumerator()
        {
            GetEnumeratorCalls++;
            throw new InvalidOperationException("iterator() must never be called by rendering");
        }
    }

    /// <summary>
    /// The rendering rule: a user collection implemented from scratch — not a platform-collection
    /// subclass, so it carries no platform ancestor state to fall back on —
    /// <c>lookalike-collection-not-platform-defined</c>. Expected outcome is rendered without
    /// executing its own iterator: the target must never enumerate it by calling
    /// <see cref="GetEnumerator"/>; it renders as an object (its type name and, where free, its
    /// size) instead.
    /// </summary>
    public sealed class LookalikeCollection : IEnumerable<object?>
    {
        private readonly List<object?> _backing;

        /// <summary>How many times the hand-rolled enumerator below actually ran.</summary>
        public int GetEnumeratorCalls;

        public LookalikeCollection(HostileGraphs.Secret held)
        {
            _backing = new List<object?> { held };
        }

        /// <inheritdoc />
        public IEnumerator<object?> GetEnumerator()
        {
            GetEnumeratorCalls++;
            return _backing.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>
    /// The rendering rule's 2026-09-18 refinement (owner ruling, the repository's agent guide): a subclass of an
    /// ABSTRACT platform collection base whose overridden enumeration counts its calls and refuses
    /// — <c>abstract-collection-subclass-override</c>. Unlike <see cref="SideEffectingIteratorList"/>
    /// (a concrete <see cref="List{T}"/> subclass, where the platform ancestor's own
    /// <c>_items</c>/<c>_size</c> fields are an honest, non-overridable state read), an abstract
    /// base has no such fallback — so this fixture's honest .NET twin is not a <see cref="List{T}"/>
    /// subclass at all.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Java's <c>AbstractCollection</c> declares <c>iterator()</c> <c>abstract</c> and holds no
    /// instance fields of its own — the override is the *only* way to reach elements, which is
    /// exactly why the ruling forbids calling it: no honest ancestor state exists to prefer instead.
    /// .NET's nearest collection-skeleton family (<see cref="CollectionBase"/>,
    /// <see cref="DictionaryBase"/>, <see cref="System.Collections.ObjectModel.Collection{T}"/>,
    /// <see cref="System.Collections.ObjectModel.KeyedCollection{TKey,TItem}"/>,
    /// <see cref="System.Collections.ObjectModel.ReadOnlyCollection{T}"/>) all instead <c>seal</c>
    /// their enumeration entry point — verified by reflection (<c>MethodInfo.IsFinal</c>) and by the
    /// compiler itself: <c>class D : DictionaryBase {{ public override IDictionaryEnumerator
    /// GetEnumerator() ... }}</c> fails with CS0506, "cannot override ... because it is not marked
    /// virtual, abstract, or override." That sealing is deliberate BCL design, precisely to prevent
    /// the interference this rendering rule now has to guard against for Java.
    /// </para>
    /// <para>
    /// The one platform-collection skeleton in <c>System.Collections</c> whose <c>GetEnumerator()</c>
    /// stays <c>virtual</c> and un-sealed is <see cref="ReadOnlyCollectionBase"/> — an
    /// <c>abstract</c> class, and, confirmed by reflecting on a fresh instance before its
    /// <c>InnerList</c> property is ever touched, one with no eagerly created backing state: the
    /// property lazily allocates an empty <see cref="ArrayList"/> only on first access, so a
    /// subclass that never reads <c>InnerList</c> and keeps its element in a private field of its
    /// own — exactly as this fixture does — leaves the platform ancestor with nothing behind it,
    /// the same "no honest state" shape as Java's <c>AbstractCollection</c>. This is the honest .NET
    /// twin.
    /// </para>
    /// <para>
    /// @llmNote Verified empirically, not just by inspection: <c>ValueRenderer.Render</c> and
    /// <c>ValueRenderer.RenderStructured</c> already leave <see cref="GetEnumeratorCalls"/> at 0 for
    /// this fixture today — <c>PlatformTypes.IsPlatformDefined</c> decides origin from the
    /// *most-derived* type's own declaring assembly (this fixture's, not
    /// <see cref="ReadOnlyCollectionBase"/>'s), and <c>PlatformTypes.ListAncestor</c> only recognizes
    /// a closed <see cref="List{T}"/> ancestor, not <see cref="ReadOnlyCollectionBase"/> — so this
    /// shape falls straight to plain object introspection already, by construction, ahead of the
    /// Java fix. See <c>RenderingReadsStateCorpusReplayTests</c> for the LIVE assertion.
    /// </para>
    /// </remarks>
#pragma warning disable S4052 // the outdated base is the fixture: it is the one BCL collection skeleton with an un-sealed GetEnumerator
    public sealed class AbstractCollectionSubclassOverride : ReadOnlyCollectionBase
#pragma warning restore S4052
    {
        private readonly HostileGraphs.Secret _held;

        /// <summary>How many times the overridden enumerator below actually ran.</summary>
        public int GetEnumeratorCalls;

        public AbstractCollectionSubclassOverride(HostileGraphs.Secret held)
        {
            _held = held;
        }

        /// <inheritdoc />
        public override IEnumerator GetEnumerator()
        {
            GetEnumeratorCalls++;
            throw new InvalidOperationException("iterator() must never be called by rendering");
        }
    }

    /// <summary>
    /// The rendering rule's 2026-09-18 refinement, the map-shaped twin of
    /// <see cref="AbstractCollectionSubclassOverride"/> — <c>abstract-map-subclass-override</c>.
    /// Java's <c>AbstractMap</c> declares <c>entrySet()</c> <c>abstract</c> with no backing state of
    /// its own; the honest .NET analogue needs an <c>abstract</c>, map-shaped platform base whose
    /// enumeration entry point a subclass can still actually override.
    /// </summary>
    /// <remarks>
    /// <para>
    /// .NET ships no <c>IDictionary</c>-shaped abstract base with that property:
    /// <see cref="DictionaryBase"/> — the historical, pre-generics analogue of <c>AbstractMap</c> —
    /// seals its <c>GetEnumerator()</c> the same way <see cref="CollectionBase"/> does (see
    /// <see cref="AbstractCollectionSubclassOverride"/>'s remarks for the CS0506 proof). The nearest
    /// honest match is instead <see cref="NameObjectCollectionBase"/> — <c>abstract</c>, the base of
    /// <see cref="NameValueCollection"/> (.NET's idiomatic string-keyed, map-shaped collection type),
    /// and, confirmed by reflection, the rare case in this family whose <c>GetEnumerator()</c> stays
    /// <c>virtual</c>/un-sealed. Its backing fields (<c>_entriesArray</c>, <c>_entriesTable</c>) are
    /// real, but this fixture never calls the protected <c>BaseAdd</c> that would populate them, so
    /// they stay empty — the secret lives only in a private field the override alone would reach,
    /// reproducing "no honest ancestor state carries the payload" even though the field itself is
    /// not literally absent the way <see cref="ReadOnlyCollectionBase"/>'s is.
    /// </para>
    /// <para>
    /// @llmNote Also verified empirically LIVE today, for the same reason as the collection twin:
    /// <c>PlatformTypes.IsPlatformDefined</c> checks only the most-derived type's declaring assembly,
    /// and <c>PlatformTypes.ListAncestor</c> recognizes only <see cref="List{T}"/> — neither fires for
    /// a <see cref="NameObjectCollectionBase"/> subclass, so <see cref="GetEnumeratorCalls"/> stays 0
    /// and the fixture renders as a plain object already.
    /// </para>
    /// </remarks>
    public sealed class AbstractMapSubclassOverride : NameObjectCollectionBase
    {
        private readonly HostileGraphs.Secret _held;

        /// <summary>How many times the overridden enumerator below actually ran.</summary>
        public int GetEnumeratorCalls;

        public AbstractMapSubclassOverride(HostileGraphs.Secret held)
        {
            _held = held;
        }

        /// <inheritdoc />
        public override IEnumerator GetEnumerator()
        {
            GetEnumeratorCalls++;
            throw new InvalidOperationException("entrySet()/iterator() must never be called by rendering");
        }
    }

    /// <summary>
    /// The user-authored abstract base <c>fieldless-abstract-subclass-tostring-door</c> needs — see
    /// <see cref="FieldlessAbstractSubclassToStringDoor"/> for why. Java's <c>AbstractCollection</c>
    /// overrides <c>toString()</c> to enumerate its own elements; verified by reflection that none of
    /// .NET's collection skeletons do the same (<c>ToString()</c> on <see cref="ReadOnlyCollectionBase"/>,
    /// <see cref="CollectionBase"/>, <see cref="DictionaryBase"/>, <see cref="NameObjectCollectionBase"/>,
    /// <see cref="List{T}"/>, <see cref="ArrayList"/> and <see cref="Hashtable"/> all resolve to the
    /// declaring type <see cref="object"/> — none of them stringify elements at all), so there is no
    /// platform base this fixture can subclass to reproduce the door; this hand-rolled base is the
    /// honest .NET twin, not a platform-defined stand-in.
    /// </summary>
    public abstract class NarratingCollectionBase
    {
        /// <summary>The elements to narrate — a hostile subclass overrides this and refuses.</summary>
        protected abstract IEnumerable<object?> Enumerate();

        /// <inheritdoc />
        public override string ToString() => "[" + string.Join(", ", Enumerate()) + "]";
    }

    /// <summary>
    /// The rendering rule's "toString door" —
    /// <c>fieldless-abstract-subclass-tostring-door</c>: a FIELDLESS user subclass of an ABSTRACT
    /// collection base. <c>rendersItsOwnString</c> trusts any type with no instance fields of its own
    /// to stand behind its own stringification — and this class declares none — but the text it then
    /// trusts is <see cref="NarratingCollectionBase"/>'s OWN inherited <see cref="object.ToString"/>,
    /// which walks <see cref="Enumerate"/> internally. <see cref="AbstractCollectionSubclassOverride"/>
    /// above pins the field-bearing sibling of this same shape, which the abstract-base refinement
    /// already made safe by routing it to an object dump instead of <c>SafeToString</c>; this one has
    /// no field to make that routing distrust it.
    /// </summary>
    /// <remarks>
    /// No constructor argument, unlike every other <c>hostileMember</c> shape: accepting one only to
    /// discard it would still read as a stored field to a careless refactor, and the whole point of
    /// this fixture is to carry zero. The spy counter is <c>static</c>, not instance, for the same
    /// reason — an instance-field spy would defeat the very fieldless precondition this fixture exists
    /// to hold. Callers reset it before use.
    /// </remarks>
    public sealed class FieldlessAbstractSubclassToStringDoor : NarratingCollectionBase
    {
        private static int _iteratorCalls;

        /// <summary>How many times the overridden enumerator below actually ran.</summary>
        public static int IteratorCalls => Volatile.Read(ref _iteratorCalls);

        /// <summary>Resets the spy counter — the counter is static, so callers must reset it before use.</summary>
        public static void ResetIteratorCalls() => Volatile.Write(ref _iteratorCalls, 0);

        /// <inheritdoc />
        protected override IEnumerable<object?> Enumerate()
        {
            Interlocked.Increment(ref _iteratorCalls);
            throw new InvalidOperationException("iterator() must never be called by rendering");
        }
    }

    /// <summary>A ring: every node holds the next, and the last holds the first.</summary>
    public sealed class Ring(HostileGraphs.Secret held)
    {
        /// <summary>The redacted payload.</summary>
        public HostileGraphs.Secret Held { get; } = held;

        /// <summary>The next node in the ring, wired up after every node exists.</summary>
        public Ring? Next { get; private set; }

        /// <summary>Links this node to <paramref name="node"/>.</summary>
        public void LinkTo(Ring node) => Next = node;
    }
}
