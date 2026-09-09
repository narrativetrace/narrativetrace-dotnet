// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 7: the AI-consumer injection oracle.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: A narrative is read by a language model as often as by a person, and the values in it
/// came from somewhere the library does not control. The oracle is not that instruction-shaped text
/// is filtered — filtering prose is a losing game, and a redacted-looking narrative is a lie. It is
/// that the text stays <b>exactly one value</b>: parse or lex the output again and the payload comes
/// back as a single node, in the same document shape a harmless value produces. It can then say
/// whatever it likes and still be data, because nothing an LLM reads as structure came from it.
/// </para>
/// <para>
/// @llmNote The comparison is against a <em>benign baseline</em> rendered from the same tree shape.
/// That is what makes the assertion mean "one value": an escape that leaked would add a JSON field,
/// a Mermaid statement, or a Markdown/frontmatter fence, and every one of those changes the shape
/// while leaving the document well formed. A well-formedness check alone would pass a forged field.
/// </para>
/// <para>
/// @edgeCase Values enter by the three routes production has, and the contract differs. A
/// <em>captured value</em> passes through <see cref="ValueRenderer"/>, so its shape must match the
/// baseline exactly. An <em>exception message</em> and a <em>scenario</em> are text the application
/// wrote and renderers show them as prose, so the oracle there is the structural one only — the text
/// may add lines, but it may never add a field, a statement, a heading or a fence.
/// </para>
/// </remarks>
public class InjectionContainmentPropertyTests
{
    /// <summary>A value with nothing structural in it. Every shape comparison is against this.</summary>
    private const string Benign = "order-42";

    /// <summary>
    /// Rendered once. The shape of a document does not depend on the run, and recomputing the
    /// baseline per case made the baseline the slowest thing in the suite.
    /// </summary>
    private static readonly Lazy<Dictionary<string, string>> BenignBaseline =
        new(() => Emitters.Renderers(TreeOf(Benign)));

    public static IEnumerable<object[]> Injections() =>
        HostileCorpus.Injections().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Injections))]
    public void Every_injection_payload_comes_back_as_exactly_one_value(CorpusCase payload) =>
        AssertSameShape(payload.Value);

    /// <summary>
    /// The round trip, which is the claim in its strongest form: the JSON a consumer parses gives
    /// back the captured value byte for byte, as one string node.
    /// </summary>
    [Theory]
    [MemberData(nameof(Injections))]
    public void Every_injection_payload_round_trips_through_the_json_artifact(CorpusCase payload)
    {
        var rendered = ValueRenderer.Render(payload.Value);
        var outputs = Emitters.EveryOutput(Emitters.TreeOf(rendered, rendered));
        var document = Formats.ParseJson("renderer:json", outputs["renderer:json"]);

        Assert.Equal([rendered], Formats.StringsNamed(document.RootElement, "returnValue"));
    }

    /// <summary>
    /// The value-free structural artifact is the AI-safe projection: zero runtime values means zero
    /// prompt-injection surface. An injection payload reaching it would not be an escaping bug, it
    /// would be a hole in the claim.
    /// </summary>
    [Theory]
    [MemberData(nameof(Injections))]
    public void No_injection_payload_reaches_the_structural_projection(CorpusCase payload)
    {
        var rendered = ValueRenderer.Render(payload.Value);
        var outputs = Emitters.EveryOutput(Emitters.TreeOf(rendered, rendered));
        var probe = payload.Value[..Math.Min(24, payload.Value.Length)];

        Assert.DoesNotContain(probe, outputs["renderer:structural"], StringComparison.Ordinal);
        Assert.Contains(outputs.Keys, key => key.EndsWith(".nt", StringComparison.Ordinal));
    }

    /// <summary>An exception message is prose, but it still may not add structure to any format.</summary>
    [Theory]
    [MemberData(nameof(Injections))]
    public void No_injection_payload_in_an_exception_message_adds_structure(CorpusCase payload) =>
        AssertSameStructure(
            Emitters.Renderers(Emitters.TreeThrowing(new InvalidOperationException(Benign))),
            Emitters.Renderers(Emitters.TreeThrowing(new InvalidOperationException(payload.Value))));

    /// <summary>
    /// The scenario is the third route production has: caller-supplied text that reaches the YAML
    /// frontmatter, the Markdown body header, the structural header and the JSON scenario name. Like
    /// an exception message it is prose, so the oracle is the structural one — it may say anything
    /// and still add no field, statement, fence, heading or frontmatter key. This route once let the
    /// body header append the scenario raw while the frontmatter escaped it — the same defect class
    /// the java runtime's own fix here closed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Injections))]
    public void No_injection_payload_in_a_scenario_adds_structure(CorpusCase payload) =>
        AssertSameStructure(BenignBaseline.Value, Emitters.Renderers(TreeOf(Benign), payload.Value));

    [Property(MaxTest = 80, Arbitrary = [typeof(SecurityArbitraries.InjectionShapedArbitraries)])]
    public void Any_generated_injection_comes_back_as_exactly_one_value(string value) =>
        AssertSameShape(value);

    private static void AssertSameShape(string value) =>
        AssertSameStructure(BenignBaseline.Value, Emitters.Renderers(TreeOf(value)));

    private static TraceTree TreeOf(string value)
    {
        var rendered = ValueRenderer.Render(value);
        return Emitters.TreeOf(rendered, rendered);
    }

    /// <summary>
    /// Every format's structure, compared: the JSON document's shape, the Mermaid statement count,
    /// the frontmatter key set, and the Markdown fence counts.
    /// </summary>
    /// <remarks>
    /// Over the in-memory renderers, not the written artifacts: a writer copies what a renderer
    /// produced, so the shape is the same and writing it to disk per case would only cost time.
    /// Containment across the written artifacts is the redaction oracle's job, and it runs over all
    /// of them separately.
    /// </remarks>
    private static void AssertSameStructure(IReadOnlyDictionary<string, string> benign, IReadOnlyDictionary<string, string> hostile)
    {
        Assert.Equal(ShapeOfJson(benign), ShapeOfJson(hostile));
        Assert.Equal(
            Formats.StatementsOf(benign["renderer:mermaid"]).Count,
            Formats.StatementsOf(hostile["renderer:mermaid"]).Count);
        var benignKeys = Formats.FrontmatterKeysOf("benign", benign["renderer:markdown-document"]);
        var hostileKeys = Formats.FrontmatterKeysOf("hostile", hostile["renderer:markdown-document"]);
        Assert.True(benignKeys.SetEquals(hostileKeys), "a value must not forge a frontmatter key");
        AssertMarkdownStructureMatches(benign, hostile);
    }

    private static void AssertMarkdownStructureMatches(IReadOnlyDictionary<string, string> benign, IReadOnlyDictionary<string, string> hostile)
    {
        const string document = "renderer:markdown-document";
        Assert.Equal(Formats.FenceCount(benign[document]), Formats.FenceCount(hostile[document]));
        Assert.Equal(Formats.FrontmatterFenceCount(benign[document]), Formats.FrontmatterFenceCount(hostile[document]));
        Assert.True(
            Formats.HeadingCount(benign[document]) == Formats.HeadingCount(hostile[document]),
            "a value must not forge a Markdown heading");
    }

    private static string ShapeOfJson(IReadOnlyDictionary<string, string> outputs) =>
        Formats.JsonShape(Formats.ParseJson("renderer:json", outputs["renderer:json"]));
}
