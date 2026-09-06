// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Examples.Common.Tests;

/// <summary>Mirrors Java's <c>DemoTracesTest</c>.</summary>
public sealed class DemoTracesTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"demo-traces-{Guid.NewGuid():N}");

    private readonly string translationDir;
    private readonly string glossaryFile;
    private readonly StringWriter diagnostics = new();

    public DemoTracesTests()
    {
        Directory.CreateDirectory(root);
        translationDir = Path.Combine(root, "traces-es");
        glossaryFile = Path.Combine(root, "glossary.json");
        DemoTraces.ResetSequence();
    }

    public void Dispose()
    {
        diagnostics.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Writes_a_translated_file_named_after_the_scenario_when_translation_is_configured()
    {
        Capture("Scenario 1: Successful Order + Async Notification", Tree(), Translating());

        var files = Directory.GetFiles(translationDir);
        Assert.Single(files);
        Assert.Matches(
            @"^\d\d_scenario_1_successful_order_async_notification\.md$",
            Path.GetFileName(files[0]));
        var content = File.ReadAllText(files[0]);
        Assert.StartsWith(
            "=== Scenario 1: Successful Order + Async Notification ===\n",
            content,
            StringComparison.Ordinal);
        Assert.Contains(
            "OrderService.realizar pedido (placeOrder) ()", content, StringComparison.Ordinal);
        Assert.Contains("\"order-1\"", content, StringComparison.Ordinal);
    }

    [Fact]
    public void Capture_order_survives_alphabetical_file_sorting()
    {
        var env = Translating();

        Capture("Unrefactored: Player Joins World", Tree(), env);
        Capture("Refactored: Player Joins World", Tree(), env);

        var names = Directory.GetFiles(translationDir)
            .Select(Path.GetFileName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.Contains("unrefactored", names[0]!, StringComparison.Ordinal);
        Assert.Contains("_refactored", names[1]!, StringComparison.Ordinal);
    }

    [Fact]
    public void Does_nothing_when_translation_is_not_configured()
    {
        Capture("Scenario 1", Tree(), _ => null);

        Assert.False(Directory.Exists(translationDir));
    }

    [Fact]
    public void Does_nothing_when_only_the_directory_but_no_locale_is_configured()
    {
        Capture(
            "Scenario 1", Tree(),
            key => key == DemoTraces.DirectoryKey ? translationDir : null);

        Assert.False(Directory.Exists(translationDir));
    }

    [Fact]
    public void An_empty_trace_writes_nothing()
    {
        Capture("Scenario 1", new TraceTree([]), Translating());

        Assert.False(Directory.Exists(translationDir));
    }

    [Fact]
    public void A_missing_glossary_never_breaks_the_demo_run_and_writes_nothing()
    {
        Capture("Scenario 1", Tree(), key => key switch
        {
            DemoTraces.DirectoryKey => translationDir,
            DemoTraces.LocaleKey => "es",
            _ => null,
        });

        Assert.False(Directory.Exists(translationDir));
        Assert.Contains("no glossary found", diagnostics.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unwritable_directory_never_breaks_the_demo_run()
    {
        WriteGlossary();
        var blocked = Path.Combine(root, "occupied");
        File.WriteAllText(blocked, "a plain file, not a directory");

        Capture("Scenario 1", Tree(), key => key switch
        {
            DemoTraces.DirectoryKey => blocked,
            DemoTraces.LocaleKey => "es",
            GlossaryLoader.PathKey => glossaryFile,
            _ => null,
        });

        Assert.Contains(
            "could not write translated trace", diagnostics.ToString(), StringComparison.Ordinal);
    }

    private void Capture(string scenario, TraceTree trace, Func<string, string?> readEnv)
    {
        DemoTraces.Capture(scenario, trace, readEnv, diagnostics);
    }

    /// <summary>The environment the demo launcher sets for a Spanish run.</summary>
    private Func<string, string?> Translating()
    {
        WriteGlossary();
        return key => key switch
        {
            DemoTraces.DirectoryKey => translationDir,
            DemoTraces.LocaleKey => "es",
            GlossaryLoader.PathKey => glossaryFile,
            _ => null,
        };
    }

    private void WriteGlossary()
    {
        File.WriteAllText(glossaryFile, """
            {
              "schemaVersion": 1,
              "contexts": {
                "shop": {
                  "packages": ["NarrativeTrace.Examples"],
                  "description": "demo"
                }
              },
              "terms": [
                {
                  "term": "place order",
                  "context": "shop",
                  "kind": "verb-phrase",
                  "status": "curated",
                  "translations": { "es": "realizar pedido" },
                  "sources": [],
                  "firstSeen": "2026-09-06"
                }
              ]
            }
            """);
    }

    private static TraceTree Tree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "placeOrder", []),
                new Returned("\"order-1\""),
                [],
                0),
        ]);
    }
}
