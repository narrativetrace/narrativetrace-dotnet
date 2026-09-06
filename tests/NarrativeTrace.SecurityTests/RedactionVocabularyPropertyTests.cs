// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// The redaction oracle over the shared sensitive-vocabulary corpus, in every emitter the product
/// ships. The .NET mirror of java's <c>RedactionVocabularyPropertyTest</c>.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <see cref="RedactionPolicy"/> answering "yes" is not protection — protection is the
/// byte never reaching an output. This drives every row of <c>redaction.json</c> through the value
/// renderer and every downstream emitter, and asserts the direction the row declares: a canary
/// behind a sensitive name, or a national-id value, appears in no byte of any output; a canary
/// behind a near-miss name stays readable.
/// </para>
/// <para>
/// @llmNote The visible half carries the same weight as the hidden half, on purpose. A matcher
/// that redacted everything would satisfy a suite that only ever asserted absence, and the result
/// is a default that teams switch off — which leaks every field rather than one.
/// </para>
/// <para>
/// @edgeCase Absence is asserted across every emitter; presence only across the two value
/// renderers. Downstream emitters legitimately escape and re-encode what they are given, so a
/// substring check there would be asserting the escaping rule rather than the redaction rule.
/// Absence has no such asymmetry: no escaping can make a hidden value reappear.
/// </para>
/// </remarks>
public class RedactionVocabularyPropertyTests
{
    public static IEnumerable<object[]> Corpus() =>
        HostileCorpus.Redactions().Select(c => new object[] { c });

    public static IEnumerable<object[]> RedactedNames() =>
        HostileCorpus.Redactions()
            .Where(c => c.IsName && c.ExpectsRedaction)
            .Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Corpus))]
    public void Every_corpus_row_goes_the_way_it_declares(RedactionCase corpusCase)
    {
        var outputs = Oracles.WithinBudget(
            "every output for " + corpusCase.Id, () => EveryOutput(corpusCase.Payload));

        if (corpusCase.ExpectsRedaction)
        {
            AssertHiddenEverywhere(corpusCase, outputs);
        }
        else
        {
            AssertReadableInTheValueRenderers(corpusCase, outputs);
        }

        Oracles.BoundedSize(outputs);
    }

    /// <summary>
    /// A redacted field must leave the marker behind, not silence. Silence satisfies containment
    /// too, and a reader cannot tell "hidden" from "never captured".
    /// </summary>
    [Theory]
    [MemberData(nameof(RedactedNames))]
    public void A_redacted_field_shows_the_marker_rather_than_nothing(RedactionCase corpusCase)
    {
        var rendered = ValueRenderer.Render(corpusCase.Payload);

        Assert.Contains(RedactionPolicy.Marker, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(corpusCase.Secret, rendered, StringComparison.Ordinal);
    }

    /// <summary>
    /// The bug class rather than the instance: every leak found so far lived in a wrapper. A
    /// sensitive name a container deep is the same secret.
    /// </summary>
    [Theory]
    [MemberData(nameof(RedactedNames))]
    public void A_sensitive_name_is_still_hidden_inside_a_container(RedactionCase corpusCase)
    {
        var nested = new Dictionary<string, object>
        {
            ["outer"] = new List<object?> { corpusCase.Payload },
        };

        AssertHiddenEverywhere(corpusCase, EveryOutput(nested));
    }

    [Fact]
    public void The_corpus_covers_both_directions_and_both_axes()
    {
        var rows = HostileCorpus.Redactions();

        Assert.True(rows.Count > 60, "expected more than 60 rows");
        Assert.Contains(rows, r => r.IsName);
        Assert.Contains(rows, r => !r.IsName);
        Assert.Contains(rows, r => r.ExpectsRedaction);
        Assert.Contains(rows, r => !r.ExpectsRedaction);
        Assert.True(
            rows.Count(r => !r.ExpectsRedaction) > 25,
            "the false-positive half is what keeps the default switched on");
    }

    private static void AssertHiddenEverywhere(
        RedactionCase corpusCase, IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var (emitter, output) in outputs)
        {
            Assert.False(
                output.Contains(corpusCase.Secret, StringComparison.Ordinal),
                $"{corpusCase.Id} ({corpusCase.Description}) must not reach {emitter}");
        }
    }

    private static void AssertReadableInTheValueRenderers(
        RedactionCase corpusCase, IReadOnlyDictionary<string, string> outputs)
    {
        foreach (var emitter in new[] { "renderer:value-flat", "renderer:value-structured" })
        {
            Assert.Contains(corpusCase.Secret, outputs[emitter], StringComparison.Ordinal);
        }
    }

    private static Dictionary<string, string> EveryOutput(object graph)
    {
        var flat = ValueRenderer.Render(graph);
        var outputs = new Dictionary<string, string>
        {
            ["renderer:value-flat"] = flat,
            ["renderer:value-structured"] = Dump(ValueRenderer.RenderStructured(graph)),
        };
        foreach (var (key, value) in Emitters.EveryOutput(Emitters.TreeOf(flat, flat)))
        {
            outputs[key] = value;
        }

        return outputs;
    }

    // RenderedValue's ObjectVal/ListVal carry an IReadOnlyDictionary/IReadOnlyList whose default
    // ToString() prints only the .NET type name, unlike a Java record's collection fields -- so a
    // naive ToString() on the structured tree would never actually surface a nested field's
    // content, making both halves of this oracle vacuous. This walks the whole tree instead.
    private static string Dump(RenderedValue value)
    {
        return value switch
        {
            RenderedValue.StringVal s => s.Value,
            RenderedValue.LongVal l => l.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderedValue.DoubleVal d => d.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderedValue.BooleanVal b => b.Value.ToString(),
            RenderedValue.InstantVal i => i.EpochMillis.ToString(System.Globalization.CultureInfo.InvariantCulture),
            RenderedValue.NullVal => "null",
            RenderedValue.ListVal list => string.Join(", ", list.Elements.Select(Dump)),
            RenderedValue.ObjectVal obj => obj.TypeName + "("
                + string.Join(", ", obj.Fields.Select(f => $"{f.Key}={Dump(f.Value)}")) + ")",
            _ => value.ToString() ?? "",
        };
    }
}
