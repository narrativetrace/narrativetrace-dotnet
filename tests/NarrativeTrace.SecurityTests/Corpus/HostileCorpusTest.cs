// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using Xunit;

namespace NarrativeTrace.SecurityTests.Corpus;

/// <summary>
/// The corpus is data every NarrativeTrace runtime copies, so its shape is a contract in its own right.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: A fixture that silently stopped loading would turn every property below it green
/// without testing anything — the failure mode of every data-driven suite. These assertions are
/// what makes "the corpus ran" observable: the counts are non-trivial, the ids are unique, the
/// generated cases materialize to the size they claim, and every declared graph shape builds.
/// </para>
/// <para>
/// @llmNote Hostile code points are written here as <c>(char)0xNNNN</c> rather than as C# escapes
/// inside a verbatim literal, for the same reason the Java twin avoids <c>\uXXXX</c> literals: it
/// would put a raw bidi override or an unpaired surrogate into this source file — the exact thing
/// the corpus format exists to avoid.
/// </para>
/// </remarks>
public sealed class HostileCorpusTest
{
    private const int EmDash = 0x2014;

    [Fact]
    public void Every_fixture_loads_with_cases()
    {
        Assert.True(HostileCorpus.Strings().Count > 50);
        Assert.True(HostileCorpus.Injections().Count > 30);
        Assert.True(HostileCorpus.Traceparents().Count > 30);
        Assert.True(HostileCorpus.Tracestates().Count > 5);
        Assert.True(HostileCorpus.Templates().Count > 30);
        Assert.True(HostileCorpus.Graphs().Count > 40);
    }

    [Fact]
    public void Case_identifiers_are_unique_within_each_fixture()
    {
        AssertUniqueIds(HostileCorpus.Strings().Select(c => c.Id));
        AssertUniqueIds(HostileCorpus.Injections().Select(c => c.Id));
        AssertUniqueIds(HostileCorpus.Traceparents().Select(c => c.Id));
        AssertUniqueIds(HostileCorpus.Templates().Select(c => c.Id));
        AssertUniqueIds(HostileCorpus.Graphs().Select(c => c.Id));
    }

    [Fact]
    public void Every_case_carries_a_description_saying_what_breaks()
    {
        Assert.All(HostileCorpus.Strings(), c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
        Assert.All(HostileCorpus.Graphs(), c => Assert.False(string.IsNullOrWhiteSpace(c.Description)));
    }

    [Fact]
    public void The_generated_cases_materialize_to_the_size_they_claim()
    {
        Assert.Equal(1024 * 1024, ValueOf("long-1mib").Length);
        Assert.Equal(512, ValueOf("long-512").Length);
        Assert.Equal(200, ValueOf("long-truncation-boundary").Length);
        Assert.Equal(300, ValueOf("long-astral").Length);
    }

    [Fact]
    public void The_escaped_cases_carry_the_code_points_they_name()
    {
        Assert.Equal(CharOf(0x0000), ValueOf("nul"));
        Assert.StartsWith(CharOf(0x202e), ValueOf("rtl-override"), StringComparison.Ordinal);
        Assert.Equal(CharOf(0xd800), ValueOf("unpaired-high-surrogate"));
        Assert.Equal(CharOf(0xdc00), ValueOf("unpaired-low-surrogate"));
        Assert.Contains(CharOf(0x200b), ValueOf("zero-width"), StringComparison.Ordinal);
        Assert.Contains(CharOf(0x2060), ValueOf("zero-width"), StringComparison.Ordinal);
    }

    /// <summary>
    /// The fixture files are ASCII on disk. Enforced rather than agreed: an editor that helpfully
    /// normalises a raw bidi character would change what a case tests without changing what it reads.
    /// </summary>
    [Theory]
    [InlineData("strings.json")]
    [InlineData("headers.json")]
    [InlineData("templates.json")]
    [InlineData("graphs.json")]
    [InlineData("injection.json")]
    public void The_fixture_files_stay_ascii_on_disk(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "HostileCorpus", fileName);
        var text = File.ReadAllText(path, Encoding.UTF8);

        var offending = text.Where(c => c > 126 && c != EmDash).Distinct().ToList();

        Assert.True(offending.Count == 0, $"{fileName} must spell hostile characters as escapes, not raw bytes");
    }

    [Fact]
    public void Every_declared_graph_shape_builds()
    {
        foreach (var graphCase in HostileCorpus.Graphs())
        {
            // Not Record.Exception: several graph shapes legitimately build a Task (the "future"
            // kind), and Record.Exception's Func<object> overload treats any Task-shaped result as
            // async test code and refuses to run it synchronously.
            try
            {
                HostileGraphs.Build(graphCase, "sentinel-probe");
            }
            catch (Exception e)
            {
                Assert.Fail($"graph shape {graphCase.Id} must build: {e}");
            }
        }
    }

    [Fact]
    public void Every_template_fixture_name_resolves_to_a_graph()
    {
        foreach (var templateCase in HostileCorpus.Templates())
        {
            var exception = Record.Exception(() => HostileGraphs.TemplateValues(templateCase.Values, "sentinel-probe"));
            Assert.True(exception is null, $"template case {templateCase.Id} names an unknown fixture: {exception}");
        }
    }

    [Fact]
    public void The_header_fixture_marks_both_outcomes()
    {
        Assert.Contains(HostileCorpus.Traceparents(), h => h.Accepted);
        Assert.Contains(HostileCorpus.Traceparents(), h => !h.Accepted);
    }

    /// <summary>A single UTF-16 code unit as a string — includes lone surrogates, which are valid <see cref="char"/> values.</summary>
    private static string CharOf(int codeUnit) => ((char)codeUnit).ToString();

    private static string ValueOf(string id) =>
        HostileCorpus.Strings().First(c => c.Id == id).Value;

    private static void AssertUniqueIds(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        Assert.Equal(list.Count, list.Distinct().Count());
    }
}
