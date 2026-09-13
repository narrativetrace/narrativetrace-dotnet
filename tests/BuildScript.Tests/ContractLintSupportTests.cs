// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the machinery behind the <c>ContractLint</c> build target — docs-vs-published-gate §2:
/// <c>documentation/contract.yaml</c>'s schema, anchors and since-marker coverage, all offline.
/// </summary>
public sealed class ContractLintSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-contract-lint").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private string Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    // ---- Slugify / HeadingAnchors ----------------------------------------------------------

    [Fact]
    public void Slugify_matches_a_known_github_anchor()
    {
        Assert.Equal("7-slf4j-configuration", ContractLintSupport.Slugify("7. SLF4J Configuration"));
    }

    [Fact]
    public void Slugify_strips_punctuation_but_keeps_hyphens_and_underscores()
    {
        Assert.Equal(
            "nottraced",
            ContractLintSupport.Slugify("`[NotTraced]`"));
    }

    [Fact]
    public void Slugify_does_not_collapse_adjacent_separators()
    {
        // An em dash between two words removes to nothing, leaving both surrounding spaces —
        // which become a double hyphen. Deliberate: this is what GitHub's own renderer produces.
        Assert.Equal(
            "the-buffered-path--capture-that-never-blocks",
            ContractLintSupport.Slugify("The buffered path — capture that never blocks"));
    }

    [Fact]
    public void HeadingAnchors_disambiguates_repeated_slugs()
    {
        var file = Write("page.md", "# Title\n## Properties\nsome text\n## Properties\n");

        Assert.Equal(
            new HashSet<string> { "title", "properties", "properties-1" },
            ContractLintSupport.HeadingAnchors(file));
    }

    // ---- ParsePageRef -----------------------------------------------------------------------

    [Fact]
    public void ParsePageRef_splits_path_and_anchor()
    {
        var reference = ContractLintSupport.ParsePageRef("documentation/foo.md#some-anchor");

        Assert.Equal("documentation/foo.md", reference.RelativePath);
        Assert.Equal("some-anchor", reference.Anchor);
    }

    [Fact]
    public void ParsePageRef_requires_an_anchor()
    {
        Assert.Throws<ArgumentException>(() => ContractLintSupport.ParsePageRef("documentation/foo.md"));
    }

    // ---- Parse ------------------------------------------------------------------------------

    private static string ContractYaml(params string[] entryBlocks) =>
        "version_source: \"Directory.Build.props#VersionPrefix\"\nentries:\n" + string.Join("\n", entryBlocks);

    private const string ValidEntryPointEntry = """
          - id: entry-point-core
            kind: entry-point
            registry: nuget-org
            coordinate: "NarrativeTrace.Core"
            page: "documentation/foo.md#some-anchor"
            claim: "NarrativeTrace.Core resolves on nuget.org"
            since: "0.1.0"
            documented_default: "PRESENT"
            probe: "contract-probe/Probe.cs"
        """;

    [Fact]
    public void Parse_reads_an_entry_point_entry()
    {
        var file = Write("contract.yaml", ContractYaml(ValidEntryPointEntry));

        var document = ContractLintSupport.Parse(file);

        Assert.Equal("Directory.Build.props#VersionPrefix", document.VersionSource);
        var entry = Assert.Single(document.Entries);
        Assert.Equal("entry-point-core", entry.Id);
        Assert.Equal(ContractKind.EntryPoint, entry.Kind);
        Assert.Equal("NarrativeTrace.Core", entry.Coordinate);
        Assert.Equal("PRESENT", entry.Expect);
    }

    private const string ValidConfigShapeEntry = """
          - id: config-shape-example
            kind: config-shape
            page: "documentation/foo.md#some-anchor"
            claim: "a documented shape produces an effect"
            since: "0.1.0"
            expected_effect: "true"
            probe: "contract-probe/Probe.cs"
        """;

    [Fact]
    public void Parse_reads_expected_effect_for_a_config_shape_entry()
    {
        var file = Write("contract.yaml", ContractYaml(ValidConfigShapeEntry));

        var entry = Assert.Single(ContractLintSupport.Parse(file).Entries);

        Assert.Equal(ContractKind.ConfigShape, entry.Kind);
        Assert.Equal("true", entry.Expect);
    }

    [Fact]
    public void Parse_throws_on_an_unknown_kind()
    {
        var file = Write(
            "contract.yaml",
            ContractYaml("""
              - id: bad
                kind: not-a-real-kind
                page: "documentation/foo.md#anchor"
                claim: "x"
                since: "0.1.0"
                documented_default: "x"
                probe: "contract-probe/Probe.cs"
            """));

        var ex = Assert.Throws<ArgumentException>(() => ContractLintSupport.Parse(file));
        Assert.Contains("unknown kind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_throws_when_neither_documented_default_nor_expected_effect_is_present()
    {
        var file = Write(
            "contract.yaml",
            ContractYaml("""
              - id: bad
                kind: reflectable-default
                page: "documentation/foo.md#anchor"
                claim: "x"
                since: "0.1.0"
                probe: "contract-probe/Probe.cs"
            """));

        var ex = Assert.Throws<ArgumentException>(() => ContractLintSupport.Parse(file));
        Assert.Contains("needs \"documented_default\" or \"expected_effect\"", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_throws_when_entry_point_has_no_coordinate()
    {
        var file = Write(
            "contract.yaml",
            ContractYaml("""
              - id: bad
                kind: entry-point
                page: "documentation/foo.md#anchor"
                claim: "x"
                since: "0.1.0"
                documented_default: "PRESENT"
                probe: "contract-probe/Probe.cs"
            """));

        Assert.Throws<ArgumentException>(() => ContractLintSupport.Parse(file));
    }

    // ---- Lint ------------------------------------------------------------------------------

    private void GivenProbeAndPage(string probeRelative, string pageRelative, string heading)
    {
        Write(probeRelative, "// probe\n");
        Write(pageRelative, $"# Title\n\n{heading}\n\nSome text.\n");
    }

    [Fact]
    public void Lint_reports_nothing_for_an_internally_consistent_contract()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Some Anchor");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Empty(problems);
    }

    [Fact]
    public void Lint_reports_a_duplicate_id()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Some Anchor");
        var file = Write(
            "documentation/contract.yaml",
            ContractYaml(
                ValidEntryPointEntry,
                ValidEntryPointEntry.Replace(
                    "claim: \"NarrativeTrace.Core resolves on nuget.org\"",
                    "claim: \"a different claim\"")));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Contains(problems, p => p.Contains("duplicate entry id", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_reports_two_entries_making_the_same_claim()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Some Anchor");
        var second = ValidEntryPointEntry
            .Replace("id: entry-point-core", "id: entry-point-second")
            .Replace("coordinate: \"NarrativeTrace.Core\"", "coordinate: \"NarrativeTrace.Runtime\"");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry, second));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Contains(problems, p => p.Contains("make the same claim", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_reports_a_malformed_since_version()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Some Anchor");
        var file = Write(
            "documentation/contract.yaml",
            ContractYaml(ValidEntryPointEntry.Replace("since: \"0.1.0\"", "since: \"latest\"")));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Contains(problems, p => p.Contains("not a real version string", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_reports_a_missing_probe_file()
    {
        Write("documentation/foo.md", "## Some Anchor\n");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Contains(problems, p => p.Contains("does not exist", StringComparison.Ordinal) && p.Contains("probe", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_reports_a_missing_page_file()
    {
        Write("contract-probe/Probe.cs", "// probe\n");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Contains(problems, p => p.Contains("page", StringComparison.Ordinal) && p.Contains("does not exist", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_reports_a_missing_anchor()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Something Else");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string>());

        Assert.Contains(problems, p => p.Contains("anchor \"#some-anchor\" not found", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_reports_an_unreleased_marker_version_with_no_covering_entry()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Some Anchor");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string> { "0.9.9" });

        Assert.Contains(problems, p => p.Contains("since: \"0.9.9\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Lint_is_satisfied_when_any_entry_covers_a_marker_version()
    {
        GivenProbeAndPage("contract-probe/Probe.cs", "documentation/foo.md", "## Some Anchor");
        var file = Write("documentation/contract.yaml", ContractYaml(ValidEntryPointEntry));
        var document = ContractLintSupport.Parse(file);

        var problems = ContractLintSupport.Lint(_repo, document, new HashSet<string> { "0.1.0" });

        Assert.Empty(problems);
    }

    // ---- ContractDecisionSupport --------------------------------------------------------------

    private static ContractEntry Entry(
        string id = "id", ContractKind kind = ContractKind.ProbedDefault, string since = "0.1.0",
        string expect = "true", string? coordinate = null) =>
        new(id, kind, "documentation/foo.md#anchor", "a claim", since, expect, "contract-probe/Probe.cs", coordinate);

    [Fact]
    public void IsApplicable_is_true_when_since_equals_installed()
    {
        Assert.True(ContractDecisionSupport.IsApplicable("0.1.4", "0.1.4"));
    }

    [Fact]
    public void IsApplicable_is_true_when_since_is_earlier_than_installed()
    {
        Assert.True(ContractDecisionSupport.IsApplicable("0.1.0", "0.1.4"));
    }

    [Fact]
    public void IsApplicable_is_false_when_since_is_later_than_installed()
    {
        Assert.False(ContractDecisionSupport.IsApplicable("0.2.0", "0.1.4"));
    }

    [Fact]
    public void Decide_reports_not_applicable_before_since_and_never_compares_observed()
    {
        var entry = Entry(since: "0.2.0", expect: "true");

        var outcome = ContractDecisionSupport.Decide(entry, "0.1.4", observed: null);

        Assert.Equal(ContractVerdict.NotApplicableBeforeSince, outcome.Verdict);
    }

    [Fact]
    public void Decide_holds_when_observed_matches_expect()
    {
        var entry = Entry(expect: "true");

        var outcome = ContractDecisionSupport.Decide(entry, "0.1.4", observed: "true");

        Assert.Equal(ContractVerdict.Holds, outcome.Verdict);
    }

    [Fact]
    public void Decide_fails_when_observed_differs_from_expect_and_names_all_four_facts()
    {
        var entry = Entry(id: "narrativetrace-output", since: "0.2.2", expect: "true", coordinate: "NarrativeTrace.Core");

        var outcome = ContractDecisionSupport.Decide(entry, "0.2.2", observed: "false");

        Assert.Equal(ContractVerdict.Fails, outcome.Verdict);
        Assert.Contains("narrativetrace-output", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("\"true\"", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("0.2.2", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("\"false\"", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Decide_fails_with_no_answer_when_the_probe_could_not_run()
    {
        var entry = Entry(expect: "true");

        var outcome = ContractDecisionSupport.Decide(entry, "0.1.4", observed: null);

        Assert.Equal(ContractVerdict.Fails, outcome.Verdict);
        Assert.Contains("<no answer>", outcome.Message, StringComparison.Ordinal);
    }

    // ---- The four historical instances (docs-vs-published-gate §3) --------------------------
    // Each fixture proves the DECISION LOGIC would have fired: a contract entry shaped like the
    // real defect, paired with the probe result the real defect would have produced, run through
    // the exact Decide() ContractCheck uses. These are not re-runs of history (the defects
    // described in the design note are hypothetical for this port); they pin the class of bug so a
    // regression of the same shape is caught by this logic again, offline, without a release.

    [Fact]
    public void Row2_a_doc_cited_coordinate_that_does_not_resolve_would_have_failed()
    {
        // "docs cite a coordinate, the registry serves something else": an entry-point whose
        // coordinate does not actually resolve at the version under test reads as MISSING, never
        // silently PRESENT.
        var row2 = Entry(id: "entry-point-proxy", kind: ContractKind.EntryPoint, since: "0.1.0", expect: "PRESENT", coordinate: "NarrativeTrace.Proxy");

        var outcome = ContractDecisionSupport.Decide(row2, "0.2.0", observed: "MISSING");

        Assert.Equal(ContractVerdict.Fails, outcome.Verdict);
    }

    [Fact]
    public void Row3_a_documented_default_the_published_artifact_does_not_honour_would_have_failed()
    {
        var row3 = Entry(id: "output-default", kind: ContractKind.ReflectableDefault, since: "0.1.1", expect: "true");

        var outcome = ContractDecisionSupport.Decide(row3, "0.1.1", observed: "false");

        Assert.Equal(ContractVerdict.Fails, outcome.Verdict);
        Assert.Contains("documented default \"true\"", outcome.Message, StringComparison.Ordinal);
        Assert.Contains("reads \"false\"", outcome.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Row4_a_doc_committed_after_publish_believing_a_default_already_changed_would_have_failed()
    {
        // The doc author believed the config was already on; probing the PUBLISHED package
        // directly (never what the commit believed) is what catches it regardless of intent.
        var row4 = Entry(id: "proxy-options-redaction", kind: ContractKind.ProbedDefault, since: "0.1.3", expect: "true");

        var outcome = ContractDecisionSupport.Decide(row4, "0.1.3", observed: "false");

        Assert.Equal(ContractVerdict.Fails, outcome.Verdict);
    }

    [Fact]
    public void Row5_a_documented_config_shape_with_no_observable_effect_would_have_failed()
    {
        // The documented shape, applied to the published package, produces no such effect.
        var row5 = Entry(id: "trace-object-methods", kind: ContractKind.ConfigShape, since: "0.1.1", expect: "return value redacted");

        var outcome = ContractDecisionSupport.Decide(row5, "0.1.1", observed: "no effect");

        Assert.Equal(ContractVerdict.Fails, outcome.Verdict);
    }
}
