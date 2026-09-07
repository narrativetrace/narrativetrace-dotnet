// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 4 of the shared fuzzing list: template parsing and rendering.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: A narration is prose the author wrote, so it travels into every artifact without passing
/// through <see cref="ValueRenderer"/> at all — which is what made "a template naming a redacted
/// path prints it in full" a leak the renderer's own tests could not see upstream. This target
/// holds that ruling against generated paths.
/// </para>
/// <para>
/// @edgeCase Two structural facts about this runtime's <c>NarrationResolver</c> (see
/// <see cref="TemplateResolution"/>'s remarks) mean the corpus's own <c>expect: "redacted"</c> cases
/// do not exercise real resolution here: the placeholder grammar is <b>one level only</b>
/// (<c>{root.prop}</c>, never <c>{root.a.b}</c>) and property lookup is <b>case-sensitive</b>, while
/// every corpus path is written in Java's javaBean casing (<c>card.cvv</c>) against this runtime's
/// PascalCase fixtures (<c>Card.Cvv</c>). Every corpus template case is still replayed for the
/// invariants that hold regardless of resolution — never throws, bounded time, idempotent, never
/// leaks — but "must show the marker" is asserted only where this runtime's actual grammar resolves
/// the path: the FsCheck-generated properties below, built from
/// <see cref="SecurityArbitraries.RedactedPathArbitraries"/>.
/// </para>
/// </remarks>
public class TemplateRedactionPropertyTests
{
    public static IEnumerable<object[]> Templates() =>
        HostileCorpus.Templates().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Templates))]
    public void Every_corpus_template_resolves_without_leaking_or_throwing(TemplateCase templateCase)
    {
        var sentinel = Oracles.FreshSentinel();

        var resolved = Oracles.WithinBudget(
            "template " + templateCase.Id, () => TemplateResolution.Resolve(templateCase.Template, templateCase.Values, sentinel));

        Assert.NotNull(resolved);
        Assert.DoesNotContain(sentinel, resolved, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Every_corpus_template_resolves_identically_twice(TemplateCase templateCase)
    {
        var sentinel = Oracles.FreshSentinel();
        Oracles.Idempotent(
            "template " + templateCase.Id,
            () => TemplateResolution.Resolve(templateCase.Template, templateCase.Values, sentinel) ?? "");
    }

    /// <summary>
    /// The bug class: a redacted leaf stays redacted, wherever a path this runtime can actually resolve
    /// names it — regardless of what surrounds the placeholder.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.RedactedPathArbitraries), typeof(SecurityArbitraries.ProseArbitraries)])]
    public void A_path_naming_a_redacted_member_always_renders_the_marker(RedactedPathCase pathCase, string surrounding)
    {
        var sentinel = Oracles.FreshSentinel();

        var resolved = TemplateResolution.Resolve(
            surrounding + "{" + pathCase.Path + "}" + surrounding, pathCase.Fixture, sentinel);

        Assert.NotNull(resolved);
        Assert.DoesNotContain(sentinel, resolved, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, resolved, StringComparison.Ordinal);
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.BraceSoupArbitraries)])]
    public void Resolving_never_throws_whatever_the_template_contains(string template)
    {
        var sentinel = Oracles.FreshSentinel();
        var exception = Record.Exception(() => TemplateResolution.Resolve(template, "card", sentinel));
        Assert.Null(exception);
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.BraceSoupArbitraries)])]
    public void No_generated_template_leaks_the_redacted_component(string template)
    {
        var sentinel = Oracles.FreshSentinel();
        var resolved = TemplateResolution.Resolve(template, "card", sentinel);
        Assert.DoesNotContain(sentinel, resolved, StringComparison.Ordinal);
    }

    /// <summary>
    /// A single-level path is refused exactly the same way whatever text follows it — depth beyond
    /// one segment is a documented, already-N/A grammar limit (see the class remarks), not a way
    /// around the guard.
    /// </summary>
    [Property(MaxTest = 50)]
    public void A_redacted_segment_is_refused_at_any_depth_of_path(PositiveInt depth)
    {
        var bounded = 1 + (depth.Get % 50);
        var sentinel = Oracles.FreshSentinel();
        var path = "card" + string.Concat(Enumerable.Repeat(".Number", bounded - 1)) + ".Cvv";

        var resolved = TemplateResolution.Resolve("{" + path + "}", "card", sentinel);

        Assert.DoesNotContain(sentinel, resolved, StringComparison.Ordinal);
    }

    /// <summary>A resolved narration travels into every artifact, so containment has to hold there too.</summary>
    [Theory]
    [MemberData(nameof(Templates))]
    public void A_redacted_path_reaches_no_written_artifact(TemplateCase templateCase)
    {
        if (!templateCase.ExpectsRedaction)
            return;

        var sentinel = Oracles.FreshSentinel();
        var narration = TemplateResolution.Resolve(templateCase.Template, templateCase.Values, sentinel) ?? "";

        Oracles.ContainsNoSentinel(Emitters.EveryOutput(Emitters.TreeNarrating(narration, narration)), sentinel);
    }

    /// <summary>
    /// The more common placeholder spelling than <c>{card.cvv}</c>: naming the whole object rather
    /// than a path into it. The object's own <c>ToString()</c> knows nothing about
    /// <c>[NotTraced]</c>, so this leaked strictly more than the path case — every hidden component
    /// at once, bypassing truncation, cycle detection and <c>[NarrativeSummary]</c> along with them.
    /// </summary>
    [Fact]
    public void A_placeholder_naming_the_whole_object_redacts_its_hidden_members()
    {
        var sentinel = Oracles.FreshSentinel();

        var resolved = TemplateResolution.Resolve("charging {card}", "card", sentinel);

        Assert.NotNull(resolved);
        Assert.DoesNotContain(sentinel, resolved, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, resolved, StringComparison.Ordinal);
    }

    /// <summary>The same, one level deeper: a redacted component nested inside the named object.</summary>
    [Fact]
    public void A_placeholder_naming_an_object_redacts_a_nested_hidden_member()
    {
        var sentinel = Oracles.FreshSentinel();

        var resolved = TemplateResolution.Resolve("{order}", "order", sentinel);

        Assert.NotNull(resolved);
        Assert.DoesNotContain(sentinel, resolved, StringComparison.Ordinal);
        Assert.Contains(RedactionPolicy.Marker, resolved, StringComparison.Ordinal);
    }

    /// <summary>
    /// A non-scalar placeholder always renders through <see cref="ValueRenderer"/> now,
    /// even when nothing is hidden — "safe to skip the renderer" was exactly the unsound shortcut
    /// this fix closed (see <c>RedactedObjectPlaceholderTests</c> for the full rationale). A scalar
    /// still never reaches <see cref="ValueRenderer"/> at all.
    /// </summary>
    [Fact]
    public void A_placeholder_naming_an_object_with_nothing_hidden_renders_structurally()
    {
        var resolved = TemplateResolution.ResolveValue(new Money("EUR", "10.00"));

        Assert.Equal("Money(Currency: \"EUR\", Amount: \"10.00\")", resolved);
    }

    [Theory]
    [InlineData("order-42")]
    [InlineData("42")]
    public void A_scalar_placeholder_is_never_rendered_through_the_value_renderer(string value)
    {
        var resolved = TemplateResolution.ResolveValue(value);

        Assert.Equal(value, resolved);
    }

    /// <summary>A plain, unquoted <c>ToString()</c> form — money narrates as an author wrote it, not as JSON.</summary>
    public sealed record Money(string Currency, string Amount)
    {
        /// <inheritdoc />
        public override string ToString() => $"{Currency} {Amount}";
    }
}
