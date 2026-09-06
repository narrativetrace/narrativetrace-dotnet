// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
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
    /// this port needs because <see cref="KeyValuePair{TKey,TValue}"/> is an immutable struct and
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
