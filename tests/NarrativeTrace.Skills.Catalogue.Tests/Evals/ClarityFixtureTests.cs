// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Skills.Tests;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The <c>clarity-consumer</c> fixture, held to what the clarity case stands on: the CLI resolves from
/// the local feed at the version that feed is packed at, and the project really does carry the names
/// the gate flags (a fixture whose names had been tidied would let any agent pass without doing
/// anything). The live behaviour — flagged as it stands, clean once renamed — is rehearsed against the
/// grader itself in <c>ClarityGraderRehearsalTests</c>.
/// </summary>
public sealed partial class ClarityFixtureTests
{
    private static readonly string Fixture = Path.Combine(TestPaths.RepoRoot(), "skills-evals", "fixtures", "clarity-consumer");

    private static string Read(params string[] relative) => File.ReadAllText(Path.Combine([Fixture, .. relative]));

    private static string VersionPrefix() =>
        VersionPrefixPattern().Match(File.ReadAllText(Path.Combine(TestPaths.RepoRoot(), "Directory.Build.props"))).Groups[1].Value;

    [Fact]
    public void The_tool_manifest_pins_the_cli_at_the_version_the_feed_is_packed_at()
    {
        Assert.Matches(
            "\"narrativetrace.cli\"\\s*:\\s*\\{\\s*\"version\"\\s*:\\s*\"" + Regex.Escape(VersionPrefix()) + "\"",
            Read(".config", "dotnet-tools.json"));
    }

    [Fact]
    public void Narrativetrace_packages_resolve_from_the_local_feed_and_nowhere_else()
    {
        var config = Read("nuget.config");

        Assert.Contains("value=\"%NARRATIVETRACE_TEST_FEED%\"", config, StringComparison.Ordinal);
        Assert.Matches("<packageSource key=\"narrativetrace\">\\s*<package pattern=\"NarrativeTrace\\.\\*\" />\\s*</packageSource>", config);
        Assert.Single(Regex.Matches(config, "pattern=\"NarrativeTrace\\.\\*\""));
    }

    [Fact]
    public void The_project_references_no_narrativetrace_package_because_the_scan_reads_the_built_assembly()
    {
        Assert.DoesNotContain("NarrativeTrace", Read("Orders.csproj"), StringComparison.Ordinal);
    }

    [Fact]
    public void The_public_boundary_carries_the_generic_verb_and_the_vague_parameter_the_gate_flags()
    {
        var source = Read("OrderHandler.cs");

        Assert.Contains("public interface IOrderHandler", source, StringComparison.Ordinal);
        Assert.Contains("string Handle(string data)", source, StringComparison.Ordinal);
        Assert.Contains("public sealed class OrderHandler", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_program_calls_the_boundary_so_a_rename_must_reach_its_caller()
    {
        Assert.Contains("handler.Handle(\"customer-42\")", Read("Program.cs"), StringComparison.Ordinal);
    }

    [GeneratedRegex("<VersionPrefix>([^<]+)</VersionPrefix>")]
    private static partial Regex VersionPrefixPattern();
}
