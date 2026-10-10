// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Skills.Tests;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The two <c>narrativetrace-feedback</c> fixtures, held to what their cases stand on: they resolve
/// NarrativeTrace from the local feed at the version that feed is packed at, the doctor's finding
/// about them is a GENUINE false positive, and the value-free fixture is the false-positive project
/// plus exactly one planted file. Each of these silently breaking would cost a real trial to find.
/// </summary>
public sealed partial class FeedbackFixtureTests
{
    private static readonly string Root = TestPaths.RepoRoot();

    private static readonly string[] Fixtures = ["feedback-false-positive", "feedback-value-free"];

    private static string Fixture(string name) => Path.Combine(Root, "skills-evals", "fixtures", name);

    private static string VersionPrefix() =>
        VersionPrefixPattern().Match(File.ReadAllText(Path.Combine(Root, "Directory.Build.props"))).Groups[1].Value;

    /// <summary>
    /// <c>./build.sh SkillsEvalFeed</c> packs at the current version, so a bump that left these pins
    /// behind would fail restore inside a trial — after the request was already spent.
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Every_narrativetrace_pin_is_the_version_the_feed_is_packed_at(string fixture)
    {
        var project = File.ReadAllText(Path.Combine(Fixture(fixture), "Payments.csproj"));
        var tools = File.ReadAllText(Path.Combine(Fixture(fixture), ".config", "dotnet-tools.json"));

        var pins = NarrativeTracePin().Matches(project).Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(pins);
        Assert.All(pins, pin => Assert.Equal(VersionPrefix(), pin));
        Assert.Matches("\"narrativetrace.cli\"\\s*:\\s*\\{\\s*\"version\"\\s*:\\s*\"" + Regex.Escape(VersionPrefix()) + "\"", tools);
    }

    /// <summary>
    /// The feed is the ONLY source for <c>NarrativeTrace.*</c>: with nuget.org also allowed, which
    /// build a trial measured would depend on which source answered first.
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void Narrativetrace_packages_resolve_from_the_local_feed_and_nowhere_else(string fixture)
    {
        var config = File.ReadAllText(Path.Combine(Fixture(fixture), "nuget.config"));

        Assert.Contains("value=\"%NARRATIVETRACE_TEST_FEED%\"", config, StringComparison.Ordinal);
        Assert.Matches("<packageSource key=\"narrativetrace\">\\s*<package pattern=\"NarrativeTrace\\.\\*\" />\\s*</packageSource>", config);
        Assert.Single(Regex.Matches(config, "pattern=\"NarrativeTrace\\.\\*\""));
    }

    /// <summary>
    /// The case's premise is TRUE only while no test-named source types the marker literal (the
    /// doctor's own check would then pass) and the redaction test still asserts the marker through
    /// the library's constant (the project would then really be unproven).
    /// </summary>
    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void The_doctors_finding_about_the_fixture_is_a_genuine_false_positive(string fixture)
    {
        var testSources = Directory.EnumerateFiles(Fixture(fixture), "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(Fixture(fixture), path).Contains("Test", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(testSources);
        Assert.DoesNotContain(testSources, path => File.ReadAllText(path).Contains("[REDACTED]", StringComparison.Ordinal));
        Assert.Contains(testSources, path => File.ReadAllText(path).Contains("Assert.Contains(RedactionPolicy.Marker", StringComparison.Ordinal));
    }

    [Fact]
    public void The_value_free_fixture_is_the_false_positive_project_plus_one_planted_trace()
    {
        var falsePositive = Files(Fixture("feedback-false-positive"));
        var valueFree = Files(Fixture("feedback-value-free"));

        Assert.Equal(["traces/payment-charge-before-redaction.md"], valueFree.Keys.Except(falsePositive.Keys));
        Assert.Empty(falsePositive.Keys.Except(valueFree.Keys));
        Assert.All(
            falsePositive.Keys.Where(path => path != "README.md"),
            path => Assert.Equal(falsePositive[path], valueFree[path]));
    }

    [Fact]
    public void The_canary_is_planted_in_exactly_one_file()
    {
        var carriers = Files(Fixture("feedback-value-free"))
            .Where(file => file.Value.Contains("ghp_NTCANARY0001", StringComparison.Ordinal) && file.Key != "README.md")
            .Select(file => file.Key);

        Assert.Equal(["traces/payment-charge-before-redaction.md"], carriers);
    }

    public static TheoryData<string> FixtureNames() => [.. Fixtures];

    private static Dictionary<string, string> Files(string dir) =>
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(Path.GetRelativePath(dir, path)))
            .ToDictionary(path => Path.GetRelativePath(dir, path).Replace('\\', '/'), File.ReadAllText, StringComparer.Ordinal);

    private static bool IsBuildOutput(string relative) =>
        relative.Split('/', '\\').Any(segment => segment is "bin" or "obj" or "TestResults");

    [GeneratedRegex("<VersionPrefix>([^<]+)</VersionPrefix>")]
    private static partial Regex VersionPrefixPattern();

    [GeneratedRegex("<PackageReference Include=\"NarrativeTrace\\.[^\"]+\" Version=\"([^\"]+)\"")]
    private static partial Regex NarrativeTracePin();
}
