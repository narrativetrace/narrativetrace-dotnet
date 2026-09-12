// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Pins the family invariant a five-runtime investigation (2026-09-11) found only this runtime
/// already upholds: a captured value's own hand-written <c>ToString</c> is never trusted once its
/// type has public state — the reflective walk, which redacts a member by name or
/// <c>[NotTraced]</c> before ever reading it, always wins over a curated <c>ToString</c>.
/// <c>[NarrativeSummary]</c> is the only opt-in past that walk.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: Characterization tests, not new behavior — this is the shape java/ts/python/swift got
/// wrong (a hand-written <c>toString</c>/<c>__str__</c>/<c>description</c> trusted once declared,
/// bypassing the deny-list). Every assertion here is red-observable by swapping
/// <c>RenderComplex</c>'s switch arms so <c>SafeToString</c> is tried before <c>IsRecord</c>/
/// <c>HasPublicMembers</c> — verified during authoring by making exactly that edit, watching every
/// test below fail with the sentinel present, and reverting.
/// </para>
/// <para>
/// @llmNote Each fixture's own <c>ToString</c> really does interpolate the sentinel (asserted first,
/// as a sanity check on the fixture itself) — a passing suite here means the renderer chose not to
/// call it, not that the fixture forgot to leak.
/// </para>
/// </remarks>
public class NeverTrustToStringPinningTests
{
    [Fact]
    public void Record_with_its_own_deny_listed_field_is_redacted_not_summarized_by_its_own_ToString() =>
        AssertNeverTrusted(sentinel => new HostileMembers.CuratedToStringRecord(sentinel));

    [Fact]
    public void Record_whose_ToString_interpolates_a_nested_deny_listed_record_is_redacted() =>
        AssertNeverTrusted(sentinel => new HostileMembers.CuratedToStringRecordNested(
            "visible-label", new HostileMembers.CuratedToStringRecord(sentinel)));

    [Fact]
    public void Class_with_its_own_deny_listed_field_is_redacted_not_summarized_by_its_own_ToString() =>
        AssertNeverTrusted(sentinel => new HostileMembers.CuratedToStringClass(sentinel));

    [Fact]
    public void Class_whose_ToString_interpolates_a_nested_deny_listed_record_is_redacted() =>
        AssertNeverTrusted(sentinel => new HostileMembers.CuratedToStringClassNested(
            "visible-label", new HostileMembers.CuratedToStringRecord(sentinel)));

    [Fact]
    public void Dictionary_key_carrying_a_deny_listed_field_renders_with_no_secret_in_the_key()
    {
        var sentinel = Oracles.FreshSentinel();
        var dict = new Dictionary<HostileMembers.CuratedToStringRecord, string>
        {
            [new HostileMembers.CuratedToStringRecord(sentinel)] = "value",
        };

        var flat = ValueRenderer.Render(dict);

        Assert.Contains(RedactionPolicy.Marker, flat, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, flat, StringComparison.Ordinal);
        AssertEveryFormatRedacts(flat, ValueRenderer.RenderStructured(dict), sentinel);
    }

    /// <summary>
    /// Builds the fixture, confirms it really would leak if its own <c>ToString</c> were trusted,
    /// then asserts the renderer never reaches for it — flat, structured, and through every output
    /// format the corpus drives (indented text, Markdown, JSON).
    /// </summary>
    private static void AssertNeverTrusted(Func<string, object> build)
    {
        var sentinel = Oracles.FreshSentinel();
        var value = build(sentinel);
        var rawToString = value.ToString();
        Assert.Contains(sentinel, rawToString, StringComparison.Ordinal);

        var flat = ValueRenderer.Render(value);

        Assert.Contains(RedactionPolicy.Marker, flat, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, flat, StringComparison.Ordinal);
        Assert.NotEqual(rawToString, flat);
        AssertEveryFormatRedacts(flat, ValueRenderer.RenderStructured(value), sentinel);
    }

    private static void AssertEveryFormatRedacts(string flat, RenderedValue structured, string sentinel)
    {
        var described = Formats.Describe(structured);
        Assert.Contains(RedactionPolicy.Marker, described, StringComparison.Ordinal);
        Assert.DoesNotContain(sentinel, described, StringComparison.Ordinal);

        var renderers = Emitters.Renderers(Emitters.TreeOf(flat, flat));
        foreach (var name in new[] { "renderer:indented", "renderer:markdown", "renderer:json" })
        {
            Assert.Contains(RedactionPolicy.Marker, renderers[name], StringComparison.Ordinal);
            Assert.DoesNotContain(sentinel, renderers[name], StringComparison.Ordinal);
        }
    }
}
