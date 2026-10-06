// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Skills.Render;
using Xunit;

namespace NarrativeTrace.Skills.Tests.Render;

public sealed class SnippetResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "nt-snippet-" + Guid.NewGuid().ToString("N"));

    public SnippetResolverTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Whole_file_resolution_returns_trimmed_content()
    {
        var path = "File.cs";
        File.WriteAllText(Path.Combine(_root, path), "line one\nline two\n\n");

        var content = SnippetResolver.Resolve(new SnippetStep(path, "csharp"), _root);

        Assert.Equal("line one\nline two", content);
    }

    [Fact]
    public void Region_resolution_extracts_and_dedents_the_marked_lines()
    {
        var path = "File.cs";
        File.WriteAllText(
            Path.Combine(_root, path),
            "before\n    // snippet:begin sample\n    const int X = 1;\n    const int Y = 2;\n    // snippet:end sample\nafter\n");

        var content = SnippetResolver.Resolve(new SnippetStep(path, "csharp", Region: "sample"), _root);

        Assert.Equal("const int X = 1;\nconst int Y = 2;", content);
    }

    [Fact]
    public void Missing_region_throws()
    {
        var path = "File.cs";
        File.WriteAllText(Path.Combine(_root, path), "no regions here\n");

        Assert.Throws<InvalidOperationException>(
            () => SnippetResolver.Resolve(new SnippetStep(path, "csharp", Region: "missing"), _root));
    }
}
