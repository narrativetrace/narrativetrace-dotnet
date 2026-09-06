// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Clarity;
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using NarrativeTrace.SecurityTests.Corpus;
using NarrativeTrace.SecurityTests.Oracle;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 5 of the parity document's fuzzing list: the clarity and glossary scanners over
/// arbitrary identifier text.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: These read names, not values, which is why they look safe — but a name reaches them
/// from metadata the library did not write: a compiler-generated name, a source-generator
/// synthetic, an obfuscated assembly and a dynamically emitted proxy all produce identifiers no
/// hand-written C# name resembles: <c>&lt;</c>, <c>&gt;</c>, digits, non-ASCII, and the empty
/// string. Scoring must survive all of them, and the reports built from them must stay parseable.
/// </para>
/// <para>
/// @llmNote The scorers return a number, so the oracle is a <em>range</em> as well as the absence
/// of a crash. A score outside 0..1 silently corrupts every average computed above it, which a
/// "does not throw" assertion alone would miss.
/// </para>
/// <para>
/// @edgeCase This port's guard contract is not uniform the way Java's is: <see cref="TermNormalizer.Phrase"/>
/// and <see cref="TermNormalizer.MethodCandidates"/> throw <see cref="ArgumentException"/> on a
/// blank or tokenless identifier (the .NET analogue of Java's declared <c>IllegalArgumentException</c>),
/// while <see cref="TermNormalizer.ParameterCandidate"/>, <see cref="TermNormalizer.ClassCandidate"/>
/// and <see cref="TermNormalizer.ExceptionCandidate"/> answer with a nullable return and never throw
/// at all, blank or not. Both are declared outcomes, never a crash — the oracle checks each shape on
/// its own terms rather than assuming one uniform guard.
/// </para>
/// </remarks>
public class ScannerPropertyTests
{
    public static IEnumerable<object[]> Strings() =>
        HostileCorpus.Strings().Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_survives_every_scorer(CorpusCase hostile) =>
        AssertScoresAreUsable(hostile.Value, hostile.Id);

    [Theory]
    [MemberData(nameof(Strings))]
    public void Every_corpus_string_leaves_the_term_normalizer_with_a_declared_outcome(CorpusCase hostile) =>
        AssertNormalizesOrRejects(hostile.Value, hostile.Id + ": " + hostile.Description);

    /// <summary>A whole trace of hostile identifiers, scored and then reported on, end to end.</summary>
    [Theory]
    [MemberData(nameof(Strings))]
    public void A_trace_of_hostile_identifiers_produces_a_parseable_clarity_report(CorpusCase hostile)
    {
        var tree = TreeNamed(hostile.Value);

        var result = Oracles.WithinBudget("clarity " + hostile.Id, () => ClarityAnalyzer.Analyze(tree));

        Assert.InRange(result.Overall, 0.0, 1.0);
        var json = ClarityJsonExporter.Export(result, "scenario");
        Formats.ParseJson("clarity-json for " + hostile.Id, json);
        Assert.NotNull(ClarityReportRenderer.Render([new ScenarioClarity("scenario", result)]));
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.IdentifierArbitraries)])]
    public void A_trace_of_generated_identifiers_survives_glossary_harvest(string identifier)
    {
        var trees = new List<TraceTree> { TreeNamed(identifier) };
        var exception = Record.Exception(() => Harvester().Harvest(trees));
        Assert.Null(exception);
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.IdentifierArbitraries)])]
    public void A_trace_of_generated_identifiers_produces_a_clarity_score(string identifier)
    {
        var result = ClarityAnalyzer.Analyze(TreeNamed(identifier));

        Assert.InRange(result.Overall, 0.0, 1.0);
    }

    [Fact]
    public void A_trace_of_hostile_identifiers_survives_glossary_harvest()
    {
        var harvester = Harvester();
        foreach (var hostile in HostileCorpus.Strings())
        {
            var trees = new List<TraceTree> { TreeNamed(hostile.Value) };
            var harvest = Record.Exception(() => harvester.Harvest(trees));
            var harvestStatic = Record.Exception(() => harvester.HarvestStatic(trees));
            Assert.True(harvest is null, $"{hostile.Id}: {hostile.Description} — Harvest threw {harvest}");
            Assert.True(harvestStatic is null, $"{hostile.Id}: {hostile.Description} — HarvestStatic threw {harvestStatic}");
        }
    }

    /// <summary>
    /// A harvester over an empty glossary: every class lands in the unassigned context, which is
    /// the state a project is in before anyone writes a glossary — and the one a hostile identifier
    /// is most likely to be scanned in.
    /// </summary>
    private static GlossaryHarvester Harvester() =>
        new(
            new ContextResolver(new NarrativeTrace.Glossary.Glossary(1, new Dictionary<string, BoundedContext>(), [])),
            _ => "com.example");

    [Property(MaxTest = 250, Arbitrary = [typeof(SecurityArbitraries.IdentifierArbitraries)])]
    public void Any_identifier_scores_inside_the_unit_range(string identifier) =>
        AssertScoresAreUsable(identifier, "generated");

    [Property(MaxTest = 250, Arbitrary = [typeof(SecurityArbitraries.IdentifierArbitraries)])]
    public void Tokenizing_never_throws_and_never_returns_null(string identifier) =>
        Assert.DoesNotContain(null, IdentifierTokenizer.Tokenize(identifier));

    [Property(MaxTest = 150, Arbitrary = [typeof(SecurityArbitraries.IdentifierArbitraries)])]
    public void Scoring_is_deterministic(string identifier)
    {
        Assert.Equal(MethodNameScorer.Score(identifier), MethodNameScorer.Score(identifier));
        Assert.Equal(ClassNameScorer.Score(identifier), ClassNameScorer.Score(identifier));
        Assert.Equal(ParameterNameScorer.Score(identifier), ParameterNameScorer.Score(identifier));
    }

    [Property(MaxTest = 100, Arbitrary = [typeof(SecurityArbitraries.IdentifierArbitraries)])]
    public void Normalizing_only_ever_throws_its_declared_guard(string identifier) =>
        AssertNormalizesOrRejects(identifier, "generated identifier");

    /// <summary>
    /// <see cref="TermNormalizer.Phrase"/> and <see cref="TermNormalizer.MethodCandidates"/> guard
    /// two cases — blank, and carrying no readable word (<c>__</c>) — and are the only two members
    /// tested in that branch. <see cref="TermNormalizer.ParameterCandidate"/>,
    /// <see cref="TermNormalizer.ClassCandidate"/> and <see cref="TermNormalizer.ExceptionCandidate"/>
    /// model "nothing to harvest" with a null return for a tokenless (but non-blank) identifier
    /// instead of throwing, so they are exercised only in the never-throws branch, exactly as the
    /// Java suite's own test does.
    /// </summary>
    private static void AssertNormalizesOrRejects(string identifier, string label)
    {
        if (string.IsNullOrWhiteSpace(identifier) || IdentifierTokenizer.Tokenize(identifier).Count == 0)
        {
            Assert.Throws<ArgumentException>(() => TermNormalizer.Phrase(identifier));
            Assert.Throws<ArgumentException>(() => TermNormalizer.MethodCandidates(identifier));
            return;
        }

        var exception = Record.Exception(() =>
        {
            TermNormalizer.Phrase(identifier);
            TermNormalizer.MethodCandidates(identifier);
            TermNormalizer.ParameterCandidate(identifier);
            TermNormalizer.ClassCandidate(identifier);
            TermNormalizer.ExceptionCandidate(identifier);
        });
        Assert.True(exception is null, $"{label} — {exception}");
    }

    private static void AssertScoresAreUsable(string identifier, string label)
    {
        var methodScore = MethodNameScorer.Score(identifier);
        Assert.True(methodScore is >= 0.0 and <= 1.0, $"method score for {label}: {methodScore}");
        var classScore = ClassNameScorer.Score(identifier);
        Assert.True(classScore is >= 0.0 and <= 1.0, $"class score for {label}: {classScore}");
        var parameterScore = ParameterNameScorer.Score(identifier);
        Assert.True(parameterScore is >= 0.0 and <= 1.0, $"parameter score for {label}: {parameterScore}");
        Assert.NotNull(IdentifierTokenizer.Tokenize(identifier));
    }

    private static TraceTree TreeNamed(string identifier)
    {
        var node = new TraceNode(
            new MethodSignature(identifier, identifier, [new ParameterCapture(identifier, "\"v\"", false)]),
            new Returned("\"ok\""),
            Children: [],
            DurationTicks: 42_000_000L);
        return new TraceTree([node]);
    }
}
