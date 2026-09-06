// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

public sealed class GlossaryCommandTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"glossary-cli-{Guid.NewGuid():N}");

    private readonly string glossaryFile;
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public GlossaryCommandTests()
    {
        Directory.CreateDirectory(root);
        glossaryFile = Path.Combine(root, "glossary.json");
    }

    public void Dispose()
    {
        output.Dispose();
        error.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Validates_canonicalizes_and_regenerates_markdown()
    {
        // Hand-edited form: unsorted keys survive parsing but not canonical rewrite.
        File.WriteAllText(glossaryFile, """
            {
              "schemaVersion": 1,
              "contexts": { "billing": { "packages": ["Acme.Billing"] } },
              "terms": [
                { "term": "overdraft account", "context": "billing",
                  "kind": "noun-phrase", "status": "curated", "firstSeen": "2026-08-11" }
              ]
            }
            """);

        var exit = CliRunner.Run(
            ["glossary", "--file", glossaryFile], output, error);

        Assert.Equal(0, exit);
        Assert.Contains(
            "Glossary valid: 1 contexts, 1 terms", output.ToString(), StringComparison.Ordinal);
        Assert.StartsWith(
            "{\n  \"schemaVersion\": 1,",
            File.ReadAllText(glossaryFile),
            StringComparison.Ordinal);
        Assert.Contains(
            "overdraft account",
            File.ReadAllText(Path.Combine(root, "glossary.md")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_file_is_not_rewritten()
    {
        File.WriteAllText(glossaryFile, """
            {
              "schemaVersion": 1,
              "contexts": { "billing": { "packages": ["Acme.Billing"] } },
              "terms": []
            }
            """);
        CliRunner.Run(["glossary", "--file", glossaryFile], output, error);
        var stamp = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(glossaryFile, stamp);

        var exit = CliRunner.Run(["glossary", "--file", glossaryFile], output, error);

        Assert.Equal(0, exit);
        Assert.Equal(stamp, File.GetLastWriteTimeUtc(glossaryFile));
    }

    [Fact]
    public void Missing_file_fails_with_exit_2()
    {
        var exit = CliRunner.Run(
            ["glossary", "--file", Path.Combine(root, "absent.json")], output, error);

        Assert.Equal(2, exit);
        Assert.Contains("no glossary at", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_glossary_fails_with_exit_2_and_reason()
    {
        File.WriteAllText(glossaryFile, """{"schemaVersion": 1, "oops": true}""");

        var exit = CliRunner.Run(["glossary", "--file", glossaryFile], output, error);

        Assert.Equal(2, exit);
        Assert.Contains("invalid glossary:", error.ToString(), StringComparison.Ordinal);
        Assert.Contains("'oops'", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Usage_mentions_the_glossary_verb()
    {
        CliRunner.Run([], output, error);

        Assert.Contains("glossary", error.ToString(), StringComparison.Ordinal);
    }
}
