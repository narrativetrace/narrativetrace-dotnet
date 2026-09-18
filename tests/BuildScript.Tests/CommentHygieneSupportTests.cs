// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the per-commit audit-history/port-framing comment lint behind the
/// <c>CommentHygieneCheck</c> build target.
/// </summary>
public sealed class CommentHygieneSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-comment-hygiene").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    public sealed class HistoryPatternTests
    {
        [Theory]
        [InlineData("kept for redaction (owner ruling, 2026-09-11).")]
        [InlineData("fixed the gap (ruled 2026-09-12).")]
        [InlineData("recomputed here (2026-09-02).")]
        [InlineData("disable buffering here, by design. (since 0.1.3, unreleased)")]
        public void Matches_a_history_shaped_phrase(string text) =>
            Assert.Matches(CommentHygieneSupport.HistoryPattern, text);

        [Theory]
        [InlineData("Escapes control characters before rendering.")]
        [InlineData("Ported from Java's NationalIdShapes.")]
        [InlineData("Landed 2026-09-10 across every port.")]
        public void Does_not_match_an_ordinary_sentence(string text) =>
            Assert.DoesNotMatch(CommentHygieneSupport.HistoryPattern, text);
    }

    public sealed class PortFramingPatternTests
    {
        [Theory]
        [InlineData("this is the golden source for the algorithm.")]
        [InlineData("see the Java repo for the original.")]
        [InlineData("see the Java runtime for the original.")]
        [InlineData("see the Java implementation for the original.")]
        [InlineData("mirrors Java's Escape.Code().")]
        [InlineData("an explicit null counts as missing, as in Java.")]
        [InlineData("this is the JVM edition of the same idea.")]
        [InlineData("kept for parity, matching the JVM.")]
        public void Matches_each_named_phrase(string text) =>
            Assert.Matches(CommentHygieneSupport.PortFramingPattern, text);

        [Theory]
        [InlineData("same as every NarrativeTrace runtime.")]
        [InlineData("Escapes control characters before rendering.")]
        public void Family_wide_framing_is_not_flagged(string text) =>
            Assert.DoesNotMatch(CommentHygieneSupport.PortFramingPattern, text);
    }

    [Fact]
    public void SourceFiles_finds_every_cs_file_under_src_and_never_a_sibling_tests_directory()
    {
        Write("src/Core/A.cs", "");
        Write("src/Core/Nested/B.cs", "");
        Write("tests/Core.Tests/A.cs", "");
        Write("src/Other/C.cs", "");

        var found = CommentHygieneSupport.SourceFiles(_repo);

        Assert.Equal(
            new[]
            {
                Path.Combine(_repo, "src", "Core", "A.cs"),
                Path.Combine(_repo, "src", "Core", "Nested", "B.cs"),
                Path.Combine(_repo, "src", "Other", "C.cs"),
            }.OrderBy(p => p, StringComparer.Ordinal),
            found);
    }

    [Fact]
    public void SourceFiles_skips_generated_bin_and_obj_output()
    {
        Write("src/Core/obj/Generated.cs", "");
        Write("src/Core/bin/Debug/Compiled.cs", "");

        Assert.Empty(CommentHygieneSupport.SourceFiles(_repo));
    }

    [Fact]
    public void SourceFiles_is_empty_when_there_is_no_src_directory_at_all() =>
        Assert.Empty(CommentHygieneSupport.SourceFiles(_repo));

    [Fact]
    public void FindHits_reports_the_repo_relative_file_1_indexed_line_reason_and_trimmed_text()
    {
        Write(
            "src/Core/Foo.cs",
            "var x = 1;\n// fixed the leak (owner ruling, 2026-09-11).\nvar y = 2;\n");

        var hit = Assert.Single(CommentHygieneSupport.FindHits(_repo));

        Assert.Equal("src/Core/Foo.cs", hit.File);
        Assert.Equal(2, hit.Line);
        Assert.Equal("// fixed the leak (owner ruling, 2026-09-11).", hit.Text);
        Assert.Equal("history", hit.Reason);
    }

    [Fact]
    public void FindHits_reports_a_port_framing_hit_with_its_own_reason()
    {
        Write("src/Core/Bar.cs", "/// See how this mirrors Java's RenderWalk.\n");

        var hit = Assert.Single(CommentHygieneSupport.FindHits(_repo));

        Assert.Equal("port-framing", hit.Reason);
    }

    [Fact]
    public void FindHits_finds_nothing_in_a_file_with_no_matching_comment()
    {
        Write(
            "src/Core/Clean.cs",
            "// Redacts a value whose shape matches a known secret pattern.\nvar x = 1;\n");

        Assert.Empty(CommentHygieneSupport.FindHits(_repo));
    }

    [Fact]
    public void Lint_reports_an_unlisted_hit_as_a_violation_and_never_flags_an_allowlisted_one()
    {
        Write("src/Core/Leftover.cs", "// (owner ruling, 2026-09-11)\n");
        Write("src/Pending/NotYet.cs", "// (owner ruling, 2026-09-11)\n");
        var allowlist = new Dictionary<string, string> { ["src/Pending/NotYet.cs"] = "wave pending" };

        var result = CommentHygieneSupport.Lint(_repo, allowlist);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("src/Core/Leftover.cs", violation.File);
        Assert.Empty(result.StaleAllowlistEntries);
    }

    [Fact]
    public void Lint_flags_an_allowlist_entry_whose_file_no_longer_has_any_hit_as_stale()
    {
        Write("src/Core/Clean.cs", "var x = 1;\n");
        var allowlist = new Dictionary<string, string> { ["src/Core/Clean.cs"] = "no longer needed" };

        var result = CommentHygieneSupport.Lint(_repo, allowlist);

        Assert.Empty(result.Violations);
        Assert.Equal(["src/Core/Clean.cs"], result.StaleAllowlistEntries);
    }

    [Fact]
    public void Lint_is_clean_when_nothing_matches_and_nothing_is_allowlisted()
    {
        Write("src/Core/Clean.cs", "var x = 1;\n");

        var result = CommentHygieneSupport.Lint(_repo, new Dictionary<string, string>());

        Assert.Empty(result.Violations);
        Assert.Empty(result.StaleAllowlistEntries);
    }

    [Fact]
    public void LoadAllowlist_returns_an_empty_map_when_the_file_does_not_exist()
    {
        var allowlist = CommentHygieneSupport.LoadAllowlist(Path.Combine(_repo, "missing.json"));

        Assert.Empty(allowlist);
    }

    [Fact]
    public void LoadAllowlist_parses_a_file_to_reason_map()
    {
        Write("allowlist.json", """{"src/Core/A.cs": "wave pending"}""");

        var allowlist = CommentHygieneSupport.LoadAllowlist(Path.Combine(_repo, "allowlist.json"));

        Assert.Equal("wave pending", allowlist["src/Core/A.cs"]);
    }
}
