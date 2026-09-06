// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

public sealed class TraceOutputWriterTests : IDisposable
{
    private readonly string _outputDir;

    public TraceOutputWriterTests()
    {
        _outputDir = Path.Combine(
            Path.GetTempPath(),
            "nt-test-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
        {
            Directory.Delete(_outputDir, true);
        }
    }

    [Fact]
    public void Writes_markdown_file()
    {
        var tree = BuildSimpleTree();

        TraceOutputWriter.Write(
            tree, "PlaceOrder", _outputDir,
            TraceFormat.Markdown);

        var file = Path.Combine(
            _outputDir, "PlaceOrder.md");
        Assert.True(File.Exists(file));
        var content = File.ReadAllText(file);
        Assert.Contains("placeOrder", content);
    }

    [Fact]
    public void Writes_mermaid_file()
    {
        var tree = BuildSimpleTree();

        TraceOutputWriter.Write(
            tree, "PlaceOrder", _outputDir,
            TraceFormat.Mermaid);

        var file = Path.Combine(
            _outputDir, "PlaceOrder.mmd");
        Assert.True(File.Exists(file));
        Assert.Contains("sequenceDiagram",
            File.ReadAllText(file));
    }

    [Fact]
    public void Writes_json_file()
    {
        var tree = BuildSimpleTree();

        TraceOutputWriter.Write(
            tree, "PlaceOrder", _outputDir,
            TraceFormat.Json);

        var file = Path.Combine(
            _outputDir, "PlaceOrder.json");
        Assert.True(File.Exists(file));
        Assert.Contains("placeOrder",
            File.ReadAllText(file));
    }

    [Fact]
    public void Sanitizes_file_name()
    {
        var tree = BuildSimpleTree();

        TraceOutputWriter.Write(
            tree, "test/with:special<chars>",
            _outputDir, TraceFormat.Markdown);

        var files = Directory.GetFiles(_outputDir);
        Assert.Single(files);
        Assert.DoesNotContain("/", Path.GetFileName(files[0]));
        Assert.DoesNotContain(":",
            Path.GetFileName(files[0]));
    }

    [Fact]
    public void Creates_output_directory_if_missing()
    {
        var tree = BuildSimpleTree();
        var nested = Path.Combine(
            _outputDir, "sub", "dir");

        TraceOutputWriter.Write(
            tree, "test", nested,
            TraceFormat.Markdown);

        Assert.True(Directory.Exists(nested));
        Assert.True(File.Exists(
            Path.Combine(nested, "test.md")));
    }

    [Fact]
    public void Empty_tree_produces_output()
    {
        var tree = new TraceTree([]);

        TraceOutputWriter.Write(
            tree, "empty", _outputDir,
            TraceFormat.Markdown);

        var file = Path.Combine(
            _outputDir, "empty.md");
        Assert.True(File.Exists(file));
    }

    private static TraceTree BuildSimpleTree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "OrderService", "placeOrder", []),
                new Returned(null), [], 0),
        ]);
    }
}
