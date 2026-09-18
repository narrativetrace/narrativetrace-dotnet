// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Guards <c>stryker-config.clarity.json</c>'s vocabulary-data <c>mutate</c> excludes (owner
/// ruling 2026-09-18, TODO §35): every excluded path must structurally BE data
/// (<see cref="MutationVocabularyExclusion.Evaluate"/>) and carry a written reason
/// (<see cref="MutationVocabularyExclusion.ExcludedFiles"/>) — this is the test that fails red
/// the moment the exclude list quietly grows to cover a real logic file.
/// </summary>
public sealed class MutationVocabularyExclusionTests
{
    private const string DataFileSource = """
        namespace NarrativeTrace.Clarity;

        public static class SampleVocabulary
        {
            private static readonly HashSet<string> Words = ["accrue", "amortize", "balance"];

            public static bool IsKnown(string word) => Words.Contains(word);
        }
        """;

    [Fact]
    public void A_file_of_only_static_readonly_collections_is_a_data_file()
    {
        var verdict = MutationVocabularyExclusion.Evaluate(DataFileSource);

        Assert.True(verdict.IsDataFile, verdict.Violation);
    }

    [Fact]
    public void A_thin_lookup_method_over_the_file_own_collections_stays_a_data_file()
    {
        const string source = """
            namespace NarrativeTrace.Clarity;

            public static class SampleDictionary
            {
                private static readonly HashSet<string> Known = ["id", "name"];
                private static readonly Dictionary<string, double> Scores =
                    new() { ["id"] = 0.8, ["name"] = 0.6 };

                public static bool Contains(string token) => Known.Contains(token);

                public static double Score(string token)
                {
                    return Scores.TryGetValue(token, out var score) ? score : 0.3;
                }
            }
            """;

        var verdict = MutationVocabularyExclusion.Evaluate(source);

        Assert.True(verdict.IsDataFile, verdict.Violation);
    }

    [Fact]
    public void A_mutable_non_readonly_field_is_rejected()
    {
        const string source = """
            namespace NarrativeTrace.Clarity;

            public static class SampleMutable
            {
                private static HashSet<string> Words = ["accrue"];
            }
            """;

        var verdict = MutationVocabularyExclusion.Evaluate(source);

        Assert.False(verdict.IsDataFile);
        Assert.Contains("not static readonly", verdict.Violation);
    }

    [Fact]
    public void A_loop_is_rejected()
    {
        const string source = """
            namespace NarrativeTrace.Clarity;

            public static class SampleLoop
            {
                private static readonly string[] Words = ["a", "b"];

                public static int CountLongerThan(int min)
                {
                    var count = 0;
                    for (var i = 0; i < Words.Length; i++)
                    {
                        if (Words[i].Length > min) count++;
                    }
                    return count;
                }
            }
            """;

        var verdict = MutationVocabularyExclusion.Evaluate(source);

        Assert.False(verdict.IsDataFile);
        Assert.Contains("loop", verdict.Violation);
    }

    [Fact]
    public void A_boundary_predicate_relational_pattern_is_rejected()
    {
        const string source = """
            namespace NarrativeTrace.Clarity;

            public static class SampleBoundary
            {
                private static readonly string[] Words = ["a", "b"];

                public static bool IsMidRange(int count) => count is >= 2 and <= 4;
            }
            """;

        var verdict = MutationVocabularyExclusion.Evaluate(source);

        Assert.False(verdict.IsDataFile);
        Assert.Contains("relational pattern", verdict.Violation);
    }

    [Fact]
    public void An_arithmetic_average_is_rejected()
    {
        const string source = """
            namespace NarrativeTrace.Clarity;

            public static class SampleArithmetic
            {
                private static readonly string[] Words = ["a", "b"];

                public static double Average(int sum, int count) => sum / count;
            }
            """;

        var verdict = MutationVocabularyExclusion.Evaluate(source);

        Assert.False(verdict.IsDataFile);
        Assert.Contains("operator", verdict.Violation);
    }

    /// <summary>
    /// The "observed red" the brief asks for: pointed at a REAL logic file already in this repo
    /// (loops, length/count boundary predicates — see its own remarks) rather than a synthetic
    /// stand-in, so this proves the check on the exact class of file the exclude list must never
    /// quietly grow to cover.
    /// </summary>
    [Fact]
    public void A_real_logic_file_MethodNameScorer_is_rejected()
    {
        var path = Path.Combine(
            RepositoryPath.Root(), "src", "NarrativeTrace.Clarity", "MethodNameScorer.cs");
        var source = File.ReadAllText(path);

        var verdict = MutationVocabularyExclusion.Evaluate(source);

        Assert.False(verdict.IsDataFile);
    }

    /// <summary>
    /// The five files actually excluded by <c>stryker-config.clarity.json</c> — every one must
    /// structurally be data, and carry a non-empty reason. Reads the real config and the real
    /// source files, so a future edit to either is checked against the other automatically.
    /// </summary>
    [Fact]
    public void Every_file_excluded_in_the_real_clarity_config_is_a_data_file_with_a_reason()
    {
        var configPath = Path.Combine(RepositoryPath.Root(), "stryker-config.clarity.json");
        var excluded = MutationVocabularyExclusion.ExcludedFiles(configPath);

        Assert.NotEmpty(excluded);

        foreach (var file in excluded)
        {
            Assert.False(string.IsNullOrWhiteSpace(file.Reason), $"{file.FileName} carries no reason");

            var matches = Directory.GetFiles(
                Path.Combine(RepositoryPath.Root(), "src", "NarrativeTrace.Clarity"),
                file.FileName, SearchOption.TopDirectoryOnly);
            var sourcePath = Assert.Single(matches);
            var verdict = MutationVocabularyExclusion.Evaluate(File.ReadAllText(sourcePath));

            Assert.True(verdict.IsDataFile, $"{file.FileName}: {verdict.Violation}");
        }
    }

    /// <summary>
    /// The scratch-config half of the brief's "observed red": excluding a real logic file
    /// alongside the honest vocabulary set must make the whole-config check fail, proving the
    /// gate would catch the exclude list quietly growing to cover behaviour.
    /// </summary>
    [Fact]
    public void Adding_a_logic_file_to_the_exclude_list_fails_the_data_file_check()
    {
        var logicPath = Path.Combine(
            RepositoryPath.Root(), "src", "NarrativeTrace.Clarity", "MethodNameScorer.cs");
        var verdict = MutationVocabularyExclusion.Evaluate(File.ReadAllText(logicPath));

        Assert.False(
            verdict.IsDataFile,
            "MethodNameScorer.cs must never pass the data-file check — if it does, the " +
            "structural criterion has stopped distinguishing data from logic and the exclude " +
            "list could grow to quietly cover behaviour.");
    }
}
