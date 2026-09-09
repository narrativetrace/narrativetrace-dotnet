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
/// Target 3 of the shared fuzzing list: every output format, whatever the value
/// contained.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: The oracle is well-formedness read back by the consumer's own parser —
/// <see cref="System.Text.Json.JsonDocument"/> plus the canonical schema for JSON, the Mermaid
/// statement grammar for diagrams, a real YAML parser for frontmatter — because an escaper that is
/// merely plausible passes every eyeball test until the day it does not.
/// </para>
/// <para>
/// @llmNote Each hostile string enters twice, by the two routes production actually has. As a
/// <em>captured value</em> it passes through <see cref="ValueRenderer"/>, which sanitizes control
/// characters at capture time; as <em>narration</em> it does not, because that text is prose an
/// author wrote (and, through <c>[Narrated]</c>, prose an author wrote <em>around</em> user data).
/// Only checking the first route would leave the unsanitized half of the surface untested.
/// </para>
/// <para>
/// @edgeCase This runtime has no standalone frontmatter renderer or Mermaid alias variant — see
/// <see cref="Emitters"/>'s remarks. Frontmatter is read from the embedded block inside
/// <c>renderer:markdown-document</c> instead of a second key.
/// </para>
/// </remarks>
public class OutputFormatPropertyTests
{
    /// <summary>
    /// The frontmatter keys a clean capture produces. A hostile value must add none. This runtime
    /// carries one field the Java runtime's set does not (<c>result</c> — <see cref="MarkdownRenderer"/>'s
    /// frontmatter always states success/error, where Java folds that into <c>error_count</c> alone);
    /// <c>trace_id</c>/<c>trace_name</c> are conditional on a span context and absent from the plain
    /// trees these tests build, so they are not listed here.
    /// </summary>
    private static readonly HashSet<string> FrontmatterKeys =
        ["type", "scenario", "entry_point", "duration_ms", "method_count", "error_count", "result"];

    /// <summary>How much of a hostile value has to be absent before absence means anything.</summary>
    private const int DistinctiveProbeLength = 8;

    public static IEnumerable<object[]> Strings() =>
        HostileCorpus.Strings().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_leaves_every_format_well_formed(CorpusCase hostile)
    {
        AssertWellFormedIncludingArtifacts(AsCapturedValue(hostile));
        AssertWellFormed(Emitters.Renderers(AsNarration(hostile)));
        // The third route: the same hostile string as the scenario, which reaches the YAML
        // frontmatter, the Markdown body header, the structural header and the JSON scenario name.
        // Closes strings.json's long-astral-run-1024 case against its actual route: frontmatter's
        // read-back-with-a-real-parser oracle (FrontmatterKeysOf, via AssertWellFormed), which no
        // other route here reaches.
        AssertWellFormed(Emitters.Renderers(AsCapturedValue(hostile), hostile.Value));
    }

    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_keeps_every_format_bounded(CorpusCase hostile)
    {
        Oracles.BoundedSize(Emitters.Renderers(AsCapturedValue(hostile)));
        Oracles.BoundedSize(Emitters.Renderers(AsNarration(hostile)));
    }

    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_renders_identically_twice(CorpusCase hostile)
    {
        var tree = AsCapturedValue(hostile);
        Oracles.Idempotent("every renderer for " + hostile.Id, () => Describe(Emitters.Renderers(tree)));
    }

    /// <summary>
    /// The structural artifact's claim is stronger than well-formedness: it carries <em>no</em>
    /// runtime value at all, which is what makes it the AI-safe projection. A hostile value
    /// reaching it would be a hole in that claim, not a formatting bug.
    /// </summary>
    [Theory]
    [MemberData(nameof(Strings))]
    public void No_corpus_string_reaches_the_structural_projection(CorpusCase hostile)
    {
        // A probe shorter than this is not evidence: a lone newline is in every artifact already.
        if (hostile.Value.Length < DistinctiveProbeLength)
            return;

        var outputs = Emitters.EveryOutput(AsCapturedValue(hostile));
        var probe = hostile.Value[..DistinctiveProbeLength];

        Assert.DoesNotContain(probe, outputs["renderer:structural"], StringComparison.Ordinal);
    }

    [Fact]
    public void Every_renderer_is_present_so_a_rename_cannot_silently_skip_one()
    {
        var outputs = Emitters.EveryOutput(Emitters.TreeOf("\"a\"", "\"b\""));

        foreach (var key in new[]
                 {
                     "renderer:prose", "renderer:indented", "renderer:markdown", "renderer:markdown-document",
                     "renderer:structural", "renderer:json", "renderer:mermaid", "renderer:plantuml",
                     "renderer:canonical-entries", "renderer:structural-entries", "artifact:console",
                 })
            Assert.Contains(key, outputs.Keys);
        Assert.Contains(outputs.Keys, key => key.EndsWith(".json", StringComparison.Ordinal));
        Assert.Contains(outputs.Keys, key => key.EndsWith(".mmd", StringComparison.Ordinal));
        Assert.Contains(outputs.Keys, key => key.EndsWith(".nt", StringComparison.Ordinal));
    }

    [Property(MaxTest = 80, Arbitrary = [typeof(SecurityArbitraries.HostileTextArbitraries)])]
    public void Any_generated_value_leaves_every_format_well_formed(string value)
    {
        var rendered = ValueRenderer.Render(value);
        AssertWellFormed(Emitters.Renderers(Emitters.TreeOf(rendered, rendered)));
        AssertWellFormed(Emitters.Renderers(Emitters.TreeNarrating(value, value)));
    }

    /// <summary>
    /// The metadata counterpart of <see cref="Any_generated_value_leaves_every_format_well_formed"/>:
    /// class name, method name and parameter name are the hostile text this time, not the value —
    /// the surface only <see cref="Emitters.TreeWithHostileMetadata"/> drives.
    /// </summary>
    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_as_metadata_leaves_every_format_well_formed(CorpusCase hostile) =>
        AssertWellFormed(Emitters.Renderers(Emitters.TreeWithHostileMetadata(hostile.Value)));

    [Property(MaxTest = 80, Arbitrary = [typeof(SecurityArbitraries.HostileTextArbitraries)])]
    public void Any_generated_metadata_leaves_every_format_well_formed(string metadata) =>
        AssertWellFormed(Emitters.Renderers(Emitters.TreeWithHostileMetadata(metadata)));

    [Property(MaxTest = 50, Arbitrary = [typeof(SecurityArbitraries.HostileTextArbitraries)])]
    public void An_exception_message_leaves_every_format_well_formed(string message) =>
        AssertWellFormed(Emitters.Renderers(Emitters.TreeThrowing(new InvalidOperationException(message))));

    private static TraceTree AsCapturedValue(CorpusCase hostile)
    {
        var rendered = ValueRenderer.Render(hostile.Value);
        return Emitters.TreeOf(rendered, rendered);
    }

    private static TraceTree AsNarration(CorpusCase hostile) =>
        Emitters.TreeNarrating(hostile.Value, hostile.Value);

    /// <summary>
    /// The full path, artifacts included: the production writer's JSON is what the canonical schema
    /// governs, and a unit test of the exporter cannot see it. Reserved for the captured-value route
    /// over the corpus, because a temp directory per generated input would make the filesystem the
    /// thing being measured.
    /// </summary>
    private static void AssertWellFormedIncludingArtifacts(TraceTree tree)
    {
        var outputs = Emitters.EveryOutput(tree);

        AssertWellFormed(outputs);
        Formats.EveryJsonArtifactParses(outputs);
    }

    private static void AssertWellFormed(IReadOnlyDictionary<string, string> outputs)
    {
        Formats.ValidatesAgainstChapterTreeSchema("renderer:json", outputs["renderer:json"]);
        Formats.IsWellFormedMermaid("renderer:mermaid", outputs["renderer:mermaid"]);
        Formats.IsWellFormedPlantUml("renderer:plantuml", outputs["renderer:plantuml"]);
        var keys = Formats.FrontmatterKeysOf("renderer:markdown-document", outputs["renderer:markdown-document"]);
        Assert.True(FrontmatterKeys.SetEquals(keys), $"frontmatter carried unexpected keys: {string.Join(", ", keys)}");
    }

    private static string Describe(IReadOnlyDictionary<string, string> outputs) =>
        string.Join("", outputs.OrderBy(o => o.Key, StringComparer.Ordinal).Select(o => o.Key + "=" + o.Value));
}
