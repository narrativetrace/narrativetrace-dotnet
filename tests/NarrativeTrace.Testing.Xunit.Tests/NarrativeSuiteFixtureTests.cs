// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Testing.Xunit.Tests;

public sealed class NarrativeSuiteFixtureTests
{
    private static TraceTree Tree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);
    }

    private static Func<string, string?> Dir(string dir, string? glossary = null)
    {
        // Glossary harvesting defaults to "off" so these synthetic suites can
        // never pollute a real glossary.json found above the test bin folder.
        return key => key switch
        {
            _ when key == ConfigResolver.OutputDirKey => dir,
            NarrativeTrace.Glossary.GlossarySettings.EnvKey =>
                glossary ?? NarrativeTrace.Glossary.GlossarySettings.OffValue,
            _ => null,
        };
    }

    [Fact]
    public void Flushes_a_clarity_results_file_and_footer_on_dispose()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir), console);
            fixture.Record("first", Tree());
            fixture.Record("first", Tree());

            fixture.Dispose();

            Assert.True(File.Exists(
                Path.Combine(dir, "clarity-results.json")));
            Assert.Contains(
                "2 scenarios recorded", console.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }


    [Fact]
    public void A_registered_loss_source_is_named_once_in_the_footer()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir), console);
            var lossy = new StubLossSource(new TraceLoss(0, 2, 30));
            fixture.Record("first", Tree());

            // Registering the same capture per test must not multiply its
            // cumulative counters by the number of tests that saw it.
            fixture.ReportLoss(lossy);
            fixture.ReportLoss(lossy);
            fixture.Dispose();

            Assert.Contains(
                "Incomplete: 2 async scopes not adopted (cap, 30 spans)",
                console.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void A_suite_with_no_loss_prints_no_loss_line()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir), console);
            fixture.Record("first", Tree());
            fixture.ReportLoss(new StubLossSource(TraceLoss.None));

            fixture.Dispose();

            Assert.DoesNotContain(
                "Incomplete", console.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public void A_null_loss_source_is_rejected()
    {
        var fixture = new NarrativeSuiteFixture(
            Dir(Path.GetTempPath()), TextWriter.Null);

        Assert.Throws<ArgumentNullException>(() => fixture.ReportLoss(null!));
    }

    private sealed class StubLossSource(TraceLoss loss) : ITraceLossSource
    {
        public TraceLoss TraceLoss { get; } = loss;
    }

    [Fact]
    public void Empty_suite_flushes_nothing()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();

        var fixture = new NarrativeSuiteFixture(Dir(dir), console);
        fixture.Dispose();

        Assert.False(Directory.Exists(dir));
        Assert.Equal(string.Empty, console.ToString());
    }

    [Fact]
    public void Flush_harvests_into_a_configured_glossary_and_prints_vocabulary_line()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(
            glossaryFile,
            "{\n  \"schemaVersion\": 1,\n  \"contexts\": {},\n  \"terms\": []\n}\n");
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir, glossaryFile), console);
            fixture.Record("first", Tree());

            fixture.Dispose();

            Assert.Contains(
                "Vocabulary:", console.ToString(), StringComparison.Ordinal);
            Assert.Contains(
                "place order",
                File.ReadAllText(glossaryFile),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void A_malformed_glossary_is_reported_but_does_not_fail_the_flush()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(glossaryFile, "{ not json");
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir, glossaryFile), console);
            fixture.Record("first", Tree());

            fixture.Dispose();

            Assert.Contains(
                "Glossary harvest skipped:", console.ToString(), StringComparison.Ordinal);
            Assert.Contains(
                "recorded", console.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir), console);
            fixture.Record("only", Tree());

            fixture.Dispose();
            var afterFirst = console.ToString();
            fixture.Dispose();

            Assert.Equal(afterFirst, console.ToString());
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
    private const string TradingGlossary = """
        {
          "schemaVersion": 1,
          "contexts": {"trading": {"packages": ["Acme.Trading"]}},
          "terms": [
            {
              "term": "fold tranche",
              "context": "trading",
              "kind": "verb-phrase",
              "status": "curated",
              "firstSeen": "2020-01-01"
            }
          ]
        }
        """;

    private static TraceTree TrancheTree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("TrancheService", "FoldTranche", []),
                new Returned(null), [], 0),
        ]);
    }

    private static double OverallScore(string dir)
    {
        using var document = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(dir, "clarity-results.json")));
        return document.RootElement
            .GetProperty("scenarios")[0]
            .GetProperty("overallScore")
            .GetDouble();
    }

    private static string TempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-suitefx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void The_committed_glossary_raises_the_suite_clarity_score()
    {
        var plain = TempDir();
        var glossed = TempDir();
        var glossaryFile = Path.Combine(glossed, "glossary.json");
        File.WriteAllText(glossaryFile, TradingGlossary);
        try
        {
            var without = new NarrativeSuiteFixture(Dir(plain), TextWriter.Null);
            without.Record("folds a tranche", TrancheTree());
            without.Dispose();

            var with = new NarrativeSuiteFixture(
                Dir(glossed, glossaryFile), TextWriter.Null);
            with.Record("folds a tranche", TrancheTree());
            with.Dispose();

            Assert.True(OverallScore(glossed) > OverallScore(plain));
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
            Directory.Delete(glossed, recursive: true);
        }
    }

    [Fact]
    public void A_malformed_glossary_degrades_scoring_instead_of_failing_the_suite()
    {
        var dir = TempDir();
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(glossaryFile, "{ not json");
        var console = new StringWriter();
        try
        {
            var fixture = new NarrativeSuiteFixture(Dir(dir, glossaryFile), console);
            fixture.Record("folds a tranche", TrancheTree());

            fixture.Dispose();

            Assert.True(File.Exists(Path.Combine(dir, "clarity-results.json")));
            Assert.Contains(
                "built-in dictionaries only",
                console.ToString(),
                StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
