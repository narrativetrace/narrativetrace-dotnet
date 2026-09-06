// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Build;
using Xunit;

namespace Build.Tests;

/// <summary>
/// Covers the staleness-header machinery behind the <c>TranslationCheck</c>
/// build target: translated public docs record the git blob hash of their
/// English source, and the gate fails once that source moves on.
/// </summary>
public sealed class TranslationCheckSupportTests : IDisposable
{
    private readonly string _repo = Directory.CreateTempSubdirectory("nt-translation-check").FullName;

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    /// <summary>Writes an English source file; returns its blob-hash prefix.</summary>
    private string WriteSource(string relativePath, string content)
    {
        Write(relativePath, content);
        return TranslationCheckSupport.GitBlobHash(Encoding.UTF8.GetBytes(content))[..12];
    }

    private void WriteTranslation(string relativePath, string sourcePath, string hashPrefix) =>
        Write(relativePath, $"<!-- source: {sourcePath} blob {hashPrefix} | translated: 2026-08-13 -->\ncuerpo\n");

    private void Write(string relativePath, string content)
    {
        var file = Path.Combine(_repo, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    [Fact]
    public void Header_line_yields_source_path_and_hash_prefix()
    {
        var header = TranslationCheckSupport.ParseHeader(
            "<!-- source: documentation/guides/installation.md blob a359ff222229 | translated: 2026-08-13 -->");

        Assert.NotNull(header);
        Assert.Equal("documentation/guides/installation.md", header.SourcePath);
        Assert.Equal("a359ff222229", header.BlobHashPrefix);
        Assert.Null(header.Reviewed);
    }

    [Fact]
    public void Header_line_with_a_reviewed_date_yields_it()
    {
        var header = TranslationCheckSupport.ParseHeader(
            "<!-- source: x.md blob a359ff222229 | translated: 2026-08-13 | reviewed: 2026-09-01 -->");

        Assert.NotNull(header);
        Assert.Equal("2026-09-01", header.Reviewed);
    }

    [Theory]
    [InlineData("<!-- source: x.md blob a359ff222229 | translated: 2026-08-13 | reviewed: - -->")]
    public void Header_line_with_a_dash_reviewed_marker_is_unreviewed(string line)
    {
        var header = TranslationCheckSupport.ParseHeader(line);

        Assert.NotNull(header);
        Assert.Null(header.Reviewed);
    }

    [Theory]
    [InlineData("")]
    [InlineData("# A plain title")]
    [InlineData("<!-- source: x.md blob TOOSHORT | translated: 2026-08-13 -->")]
    [InlineData("<!-- source: x.md blob ABCDEF123456 | translated: 2026-08-13 -->")]
    [InlineData("<!-- source: x.md blob a359ff2222 | translated: 2026-08-13 -->")]
    [InlineData("<!-- source: x.md blob a359ff222229 | translated: yesterday -->")]
    [InlineData("<!-- source: two words.md blob a359ff222229 | translated: 2026-08-13 -->")]
    [InlineData("prefix <!-- source: x.md blob a359ff222229 | translated: 2026-08-13 -->")]
    [InlineData("<!-- source: x.md blob a359ff222229 | translated: 2026-08-13 | reviewed: yesterday -->")]
    public void Malformed_header_lines_are_rejected(string line)
    {
        Assert.Null(TranslationCheckSupport.ParseHeader(line));
    }

    // Known `git hash-object` output: an empty file, "hello\n", and a byte
    // sequence that is not valid UTF-8 — the hash is over raw bytes, so an
    // encoding round-trip would corrupt it.
    [Theory]
    [InlineData(new byte[0], "e69de29bb2d1d6434b8b29ae775ad8c2e48c5391")]
    [InlineData(new byte[] { 0x68, 0x65, 0x6C, 0x6C, 0x6F, 0x0A }, "ce013625030ba8dba906f756967f9e9ca394464a")]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00 }, "6e00d25c6cd705d172279b791d49c6e378416d86")]
    public void Blob_hash_matches_git_hash_object(byte[] content, string expected)
    {
        Assert.Equal(expected, TranslationCheckSupport.GitBlobHash(content));
    }

    [Fact]
    public void Translations_matching_their_sources_report_no_problems()
    {
        var readme = WriteSource("README.md", "# NarrativeTrace .NET\n");
        var guide = WriteSource("documentation/guides/installation.md", "# Installation\n");
        WriteTranslation("LEAME.md", "README.md", readme);
        WriteTranslation("documentation/guides/es/guia-de-instalacion.md", "documentation/guides/installation.md", guide);
        WriteTranslation("documentation/guides/zh-CN/安装指南.md", "documentation/guides/installation.md", guide);

        Assert.Empty(TranslationCheckSupport.Check(_repo));
    }

    [Fact]
    public void Edited_source_makes_its_translation_stale_and_names_both_hashes()
    {
        var current = WriteSource("documentation/guides/installation.md", "# Installation v2\n");
        WriteTranslation("documentation/guides/es/guia-de-instalacion.md", "documentation/guides/installation.md", "aaaaaaaaaaaa");

        var problem = Assert.Single(TranslationCheckSupport.Check(_repo));

        Assert.Contains("documentation/guides/es/guia-de-instalacion.md", problem, StringComparison.Ordinal);
        Assert.Contains("stale", problem, StringComparison.Ordinal);
        Assert.Contains("aaaaaaaaaaaa", problem, StringComparison.Ordinal);
        Assert.Contains(current, problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Translation_pointing_at_a_deleted_source_is_reported()
    {
        WriteTranslation("documentation/es/guia.md", "documentation/gone.md", "aaaaaaaaaaaa");

        var problem = Assert.Single(TranslationCheckSupport.Check(_repo));

        Assert.Contains("documentation/gone.md", problem, StringComparison.Ordinal);
        Assert.Contains("missing", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void File_in_a_language_directory_without_a_header_is_reported()
    {
        WriteSource("documentation/guides/clarity.md", "# Clarity\n");
        Write("documentation/guides/es/guia-de-claridad.md", "# Sin encabezado\n");

        var problem = Assert.Single(TranslationCheckSupport.Check(_repo));

        Assert.Contains("documentation/guides/es/guia-de-claridad.md", problem, StringComparison.Ordinal);
        Assert.Contains("header", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../outside.md")]
    [InlineData("documentation/../../outside.md")]
    public void Source_path_escaping_the_repository_root_is_reported(string sourcePath)
    {
        WriteTranslation("documentation/es/guia.md", sourcePath, "aaaaaaaaaaaa");

        var problem = Assert.Single(TranslationCheckSupport.Check(_repo));

        Assert.Contains("escapes", problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Problems_are_reported_sorted_by_file_path()
    {
        WriteSource("documentation/guides/installation.md", "# Installation\n");
        WriteTranslation("documentation/guides/es/guia-de-instalacion.md", "documentation/guides/installation.md", "aaaaaaaaaaaa");
        Write("documentation/guides/zh-CN/安装指南.md", "没有头\n");

        var problems = TranslationCheckSupport.Check(_repo);

        Assert.Equal(2, problems.Count);
        Assert.Contains("documentation/guides/es/", problems[0], StringComparison.Ordinal);
        Assert.Contains("documentation/guides/zh-CN/", problems[1], StringComparison.Ordinal);
    }

    [Fact]
    public void English_originals_and_the_i18n_convention_directory_are_left_alone()
    {
        Write("README.md", "# NarrativeTrace .NET\n");
        Write("documentation/i18n/manifest.json", "{}\n");
        Write("documentation/guides/installation.md", "# Installation\n");
        Write("documentation/architecture-decisions.md", "# ADRs\n");

        Assert.Empty(TranslationCheckSupport.Check(_repo));
    }

    // `es` is a language directory; `esx` and `i18n` share its shape without
    // being one. A near-miss must not drag English content into the gate.
    [Theory]
    [InlineData("esx")]
    [InlineData("i18n")]
    [InlineData("guides")]
    [InlineData("ES")]
    public void Directories_that_only_look_like_language_tags_are_not_scanned(string directory)
    {
        Write($"documentation/{directory}/notes.md", "# No header here\n");

        Assert.Empty(TranslationCheckSupport.Check(_repo));
    }

    [Fact]
    public void Header_is_read_through_a_utf8_bom_and_crlf_line_endings()
    {
        var hash = WriteSource("README.md", "# NarrativeTrace .NET\n");
        var header = $"<!-- source: README.md blob {hash} | translated: 2026-08-13 -->\r\n# NarrativeTrace\r\n";
        File.WriteAllText(Path.Combine(_repo, "LEAME.md"), header, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        Assert.Empty(TranslationCheckSupport.Check(_repo));
    }

    [Fact]
    public void An_empty_repository_has_nothing_to_check()
    {
        Assert.Empty(TranslationCheckSupport.Check(_repo));
    }
}
