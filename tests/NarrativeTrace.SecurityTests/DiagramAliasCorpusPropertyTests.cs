// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using NarrativeTrace.SecurityTests.Corpus;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Mermaid and PlantUML alias mode, driven by a class name — the one route the shared hostile
/// corpus never reached before.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: <see cref="MermaidSequenceRenderer"/> and <see cref="PlantUmlSequenceRenderer"/> both
/// resolve every participant through <see cref="AliasGenerator"/> — this runtime's sequence
/// diagrams have no unaliased mode at all, so the shared hostile corpus's usual routes (a corpus
/// string as a captured <em>value</em> or as narration) never drove a corpus string through the one
/// field <see cref="AliasGenerator.Generate"/> ever turns into a bare, unquoted arrow token: the
/// class name itself. That is exactly why a class literally named <c>end</c> slipped through: the
/// corpus ran every commit and never once tried it as metadata.
/// </para>
/// <para>
/// The oracle here is stricter than the plain well-formedness regex (<c>Oracle/Formats.cs</c>):
/// well-formedness alone accepts a participant line whose alias happens to be a reserved keyword,
/// because the regex only checks the <em>shape</em> of a <c>participant</c> statement, not what
/// either grammar reserves — <c>end</c> matches <c>[A-Za-z0-9_]</c> just as well as <c>end_</c>
/// does. Three assertions pin the actual contract: (1) exactly one participant line per distinct
/// class — a collision silently merging two different classes into one participant would still be
/// well formed; (2) every alias token contains only <c>[A-Za-z0-9_]</c>; and (3) no alias token,
/// lowercased, equals a reserved keyword of the grammar it was rendered for.
/// <see cref="MermaidReservedAliases"/> and <see cref="PlantUmlReservedAliases"/> are kept
/// <em>independent</em> of <see cref="AliasGenerator"/>'s own reserved-word set rather than
/// importing it — a test that asks production code for the very list it is being checked against
/// cannot fail when that list is wrong.
/// </para>
/// </remarks>
public partial class DiagramAliasCorpusPropertyTests
{
    private const string CorpusPrefix = "diagram-alias-";

    [GeneratedRegex("^[A-Za-z0-9_]+$")]
    private static partial Regex BareAliasToken();

    /// <summary>
    /// Mermaid sequence-diagram keywords, independently sourced from <c>sequenceDiagram.jison</c>
    /// (mermaid-js/mermaid, <c>develop</c> branch, verified 2026-09-13): every single-word literal
    /// lexer rule — <c>loop</c>, <c>box</c>, <c>participant</c>, <c>actor</c>, <c>create</c>,
    /// <c>destroy</c>, <c>rect</c>, <c>opt</c>, <c>alt</c>, <c>else</c>, <c>par</c>,
    /// <c>par_over</c>, <c>and</c>, <c>critical</c>, <c>option</c>, <c>break</c>, <c>end</c>,
    /// <c>links</c>, <c>link</c>, <c>properties</c>, <c>details</c>, <c>over</c>, <c>note</c>,
    /// <c>activate</c>, <c>deactivate</c>, <c>autonumber</c>, <c>off</c>, and the diagram-opening
    /// <c>sequenceDiagram</c> itself. <c>title</c> is included defensively: its lexer rule only
    /// fires when followed by same-line text today, but a future revision could drop that
    /// requirement. Matched case-insensitively — the grammar declares
    /// <c>%options case-insensitive</c>.
    /// </summary>
    private static readonly HashSet<string> MermaidReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "sequencediagram", "participant", "actor", "create", "destroy", "box", "loop", "rect",
        "opt", "alt", "else", "par", "par_over", "and", "critical", "option", "break", "end",
        "links", "link", "properties", "details", "over", "note", "activate", "deactivate",
        "autonumber", "off", "title",
    };

    /// <summary>
    /// PlantUML sequence-diagram keywords, sourced from its own documentation
    /// (plantuml.com/sequence-diagram, verified 2026-09-13): participant-declaration types
    /// (<c>participant</c>, <c>actor</c>, <c>boundary</c>, <c>control</c>, <c>entity</c>,
    /// <c>database</c>, <c>collections</c>, <c>queue</c>), flow-control blocks (<c>alt</c>,
    /// <c>else</c>, <c>opt</c>, <c>loop</c>, <c>par</c>, <c>break</c>, <c>critical</c>,
    /// <c>group</c>, <c>end</c>), annotations (<c>note</c>, <c>ref</c>, <c>create</c>,
    /// <c>activate</c>, <c>deactivate</c>, <c>destroy</c>, <c>return</c>), structural keywords
    /// (<c>box</c>, <c>title</c>, <c>header</c>, <c>footer</c>, <c>newpage</c>, <c>mainframe</c>,
    /// <c>partition</c>), and formatting/declaration keywords (<c>hide</c>, <c>show</c>,
    /// <c>autonumber</c>, <c>skinparam</c>, <c>as</c>, <c>order</c>) — the last two load-bearing in
    /// a participant declaration line itself.
    /// </summary>
    private static readonly HashSet<string> PlantUmlReservedAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "participant", "actor", "boundary", "control", "entity", "database", "collections",
        "queue", "alt", "else", "opt", "loop", "par", "break", "critical", "group", "end",
        "note", "ref", "create", "activate", "deactivate", "destroy", "return", "box", "title",
        "header", "footer", "newpage", "mainframe", "partition", "hide", "show", "autonumber",
        "skinparam", "as", "order",
    };

    public static IEnumerable<object[]> AliasCorpusCases() =>
        HostileCorpus.Strings()
            .Where(c => c.Id.StartsWith(CorpusPrefix, StringComparison.Ordinal))
            .Select(c => new object[] { c });

    [Theory]
    [MemberData(nameof(AliasCorpusCases))]
    public void Corpus_case_as_a_class_name_produces_a_safe_mermaid_alias(CorpusCase hostile) =>
        AssertMermaidParticipantCountAndCleanAliases(
            MermaidSequenceRenderer.Render(OneClassTree(hostile.Value)), expectedParticipants: 1);

    [Theory]
    [MemberData(nameof(AliasCorpusCases))]
    public void Corpus_case_as_a_class_name_produces_a_safe_plantuml_alias(CorpusCase hostile) =>
        AssertPlantUmlParticipantCountAndCleanAliases(
            PlantUmlSequenceRenderer.Render(OneClassTree(hostile.Value)), expectedParticipants: 1);

    private static readonly string[] ExpectedAliasCorpusIds =
    [
        "diagram-alias-arrow",
        "diagram-alias-quote-collision",
        "diagram-alias-reserved-word",
        "diagram-alias-empty",
    ];

    /// <summary>
    /// The property above silently tests nothing if a rename ever drops these ids from the corpus.
    /// </summary>
    [Fact]
    public void Every_diagram_alias_corpus_case_is_actually_present()
    {
        var ids = HostileCorpus.Strings()
            .Select(c => c.Id)
            .Where(id => id.StartsWith(CorpusPrefix, StringComparison.Ordinal))
            .ToList();

        Assert.Equal(ExpectedAliasCorpusIds, ids);
    }

    /// <summary>
    /// The collision the corpus row <c>diagram-alias-quote-collision</c> names but cannot exercise
    /// alone: two <em>different</em> classes (<c>a"b</c> and <c>a'b</c>) whose sanitized aliases
    /// land on the same bare token. Disambiguation must still keep them as two participants, never
    /// silently merge them into one.
    /// </summary>
    [Fact]
    public void Quote_and_apostrophe_class_names_that_sanitize_to_the_same_alias_stay_distinct_participants_in_mermaid() =>
        AssertMermaidParticipantCountAndCleanAliases(
            MermaidSequenceRenderer.Render(TwoClassTree("a\"b", "a'b")), expectedParticipants: 2);

    /// <summary>The same collision, through the PlantUML renderer.</summary>
    [Fact]
    public void Quote_and_apostrophe_class_names_that_sanitize_to_the_same_alias_stay_distinct_participants_in_plantuml() =>
        AssertPlantUmlParticipantCountAndCleanAliases(
            PlantUmlSequenceRenderer.Render(TwoClassTree("a\"b", "a'b")), expectedParticipants: 2);

    /// <summary>
    /// Mermaid declares <c>participant &lt;alias&gt; as &lt;display&gt;</c> — the alias is the
    /// first token after the <c>participant</c> keyword, whatever the (possibly quoted,
    /// possibly space-containing) display name after <c>as</c> looks like.
    /// </summary>
    private static void AssertMermaidParticipantCountAndCleanAliases(string diagram, int expectedParticipants) =>
        AssertParticipantCountAndCleanAliases(
            diagram, MermaidReservedAliases, expectedParticipants, line => line["participant ".Length..].Split(' ', 2)[0]);

    /// <summary>
    /// PlantUML declares <c>participant &lt;display&gt; as &lt;alias&gt;</c> — the alias is the
    /// last whitespace-separated token on the line, whatever the (possibly quoted, possibly
    /// space-containing) display name before <c>as</c> looks like.
    /// </summary>
    private static void AssertPlantUmlParticipantCountAndCleanAliases(string diagram, int expectedParticipants) =>
        AssertParticipantCountAndCleanAliases(
            diagram, PlantUmlReservedAliases, expectedParticipants,
            line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[^1]);

    private static void AssertParticipantCountAndCleanAliases(
        string diagram, HashSet<string> reservedAliases, int expectedParticipants, Func<string, string> extractAlias)
    {
        var participantLines = diagram
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("participant ", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            participantLines.Count == expectedParticipants,
            $"exactly one participant line per distinct class, got {participantLines.Count}: {diagram}");

        foreach (var line in participantLines)
        {
            var alias = extractAlias(line);
            Assert.True(
                BareAliasToken().IsMatch(alias),
                $"a participant alias containing anything but [A-Za-z0-9_]: {line}");
            Assert.False(
                reservedAliases.Contains(alias),
                $"a participant alias that is a bare reserved keyword: {line}");
        }
    }

    private static TraceTree OneClassTree(string className) =>
        new([new TraceNode(new MethodSignature(className, "Run", []), new Returned("true"), [], 1_000_000)]);

    private static TraceTree TwoClassTree(string callerClassName, string targetClassName)
    {
        var child = new TraceNode(
            new MethodSignature(targetClassName, "Run", []), new Returned("true"), [], 1_000_000);
        var root = new TraceNode(
            new MethodSignature(callerClassName, "Call", []), new Returned("true"), [child], 2_000_000);
        return new TraceTree([root]);
    }
}
