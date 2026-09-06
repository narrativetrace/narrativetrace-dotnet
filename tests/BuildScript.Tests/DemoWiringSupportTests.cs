// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the scenario-title ↔ wiring-note parity behind the
/// <c>DemoWiringCheck</c> build target: every scenario the examples print
/// must have a note in <c>examples/demo/wiring.awk</c>, and no note may
/// outlive its scenario.
/// </summary>
public sealed class DemoWiringSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-demo-wiring").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private void WriteWiring(params string[] keys)
    {
        var entries = keys.Select(k => $"  wiring[\"{k}\"] = \\\n    \"Wiring: note.\"\n");
        Write("examples/demo/wiring.awk", "BEGIN {\n" + string.Concat(entries) + "}\n");
    }

    [Fact]
    public void Scenario_titles_come_from_BeginScenario_calls_and_inline_headers()
    {
        Write("examples/A/Demo.cs", "run.BeginScenario(\"Scenario 1: Out of Stock\");\nlogger.Info(\"=== Refactored: Player Joins World ===\\n\");");

        var titles = DemoWiringSupport.ScenarioTitles(_repo);

        Assert.Equal(["Refactored: Player Joins World", "Scenario 1: Out of Stock"], titles.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Every_scenario_with_a_note_and_every_note_with_a_scenario_is_clean()
    {
        Write("examples/A/Demo.cs", "run.BeginScenario(\"Scenario 1: Out of Stock\");");
        WriteWiring("Scenario 1: Out of Stock");

        Assert.Empty(DemoWiringSupport.Check(_repo));
    }

    [Fact]
    public void Scenario_without_a_note_is_reported_with_the_key_to_add()
    {
        Write("examples/A/Demo.cs", "run.BeginScenario(\"Scenario 1: Out of Stock\");");
        WriteWiring();

        var problem = Assert.Single(DemoWiringSupport.Check(_repo));

        Assert.Contains("\"Scenario 1: Out of Stock\" has no wiring note", problem, StringComparison.Ordinal);
        Assert.Contains("wiring[\"Scenario 1: Out of Stock\"]", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Note_whose_scenario_no_longer_exists_is_reported_as_orphaned()
    {
        Write("examples/A/Demo.cs", "run.BeginScenario(\"Scenario 1: Out of Stock\");");
        WriteWiring("Scenario 1: Out of Stock", "Scenario 9: Renamed Away");

        var problem = Assert.Single(DemoWiringSupport.Check(_repo));

        Assert.Contains("\"Scenario 9: Renamed Away\" matches no example scenario", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Notes_for_sections_without_a_header_count_as_present_when_the_text_appears_in_a_source()
    {
        Write("examples/A/Demo.cs", "run.Info(\"         CLARITY ANALYSIS REPORT\");\nrun.TraceTree(trace); // --- Trace tree ---");
        WriteWiring("CLARITY ANALYSIS REPORT", "--- Trace tree ---");

        Assert.Empty(DemoWiringSupport.Check(_repo));
    }

    [Fact]
    public void Missing_wiring_table_is_the_only_problem_reported()
    {
        Write("examples/A/Demo.cs", "run.BeginScenario(\"Scenario 1: Out of Stock\");");

        var problem = Assert.Single(DemoWiringSupport.Check(_repo));

        Assert.Contains("examples/demo/wiring.awk is missing", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/// The header line is <c>=== title ===</c>.")]
    [InlineData("// logger.Info(\"=== commented out ===\");")]
    [InlineData("var h = $\"=== {scenario} ===\";")]
    [InlineData("var h = \"=== \" + scenario + \" ===\";")]
    public void Comments_and_headers_built_from_variables_are_not_titles(string line)
    {
        Write("examples/A/Demo.cs", line + "\n");
        WriteWiring();

        Assert.Empty(DemoWiringSupport.ScenarioTitles(_repo));
        Assert.Empty(DemoWiringSupport.Check(_repo));
    }

    [Fact]
    public void FSharp_sources_are_scanned_and_build_output_is_not()
    {
        Write("examples/Library/Program.fs", "run.BeginScenario(\"Scenario 1: Successful Book Borrow\")");
        Write("examples/Library/obj/Generated.cs", "run.BeginScenario(\"Scenario 99: Stale Build Output\");");
        Write("examples/Library/bin/Debug/Copy.cs", "run.BeginScenario(\"Scenario 98: Stale Build Output\");");

        Assert.Equal(["Scenario 1: Successful Book Borrow"], DemoWiringSupport.ScenarioTitles(_repo));
    }

    [Fact]
    public void Problems_are_reported_sorted()
    {
        Write("examples/A/Demo.cs", "run.BeginScenario(\"Zulu\");\nrun.BeginScenario(\"Alpha\");");
        WriteWiring("Mike");

        var problems = DemoWiringSupport.Check(_repo);

        Assert.Equal(3, problems.Count);
        Assert.StartsWith("scenario \"Alpha\"", problems[0], StringComparison.Ordinal);
        Assert.StartsWith("scenario \"Zulu\"", problems[1], StringComparison.Ordinal);
        Assert.StartsWith("wiring note \"Mike\"", problems[2], StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_repository_has_only_the_missing_table_to_report()
    {
        Assert.Single(DemoWiringSupport.Check(_repo));
    }

    // The gate itself: the real launcher must be able to explain every real scenario.
    [Fact]
    public void The_repository_wiring_table_explains_every_example_scenario()
    {
        var root = RepositoryPath.Root();

        var problems = DemoWiringSupport.Check(root);

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

}
