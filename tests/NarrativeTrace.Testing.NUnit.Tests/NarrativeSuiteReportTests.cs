// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.TestingNUnit;
using Xunit;

namespace NarrativeTrace.Testing.NUnit.Tests;

public sealed class NarrativeSuiteReportTests
{
    private static TraceTree Tree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);
    }

    /// <summary>
    /// Glossary harvesting pinned "off" so these synthetic suites can never
    /// pollute a real glossary.json found above the test bin folder.
    /// </summary>
    private static NarrativeSuiteReport Report(string dir, string? glossary = null)
    {
        return new NarrativeSuiteReport(
            dir,
            key => key == NarrativeTrace.Glossary.GlossarySettings.EnvKey
                ? glossary ?? NarrativeTrace.Glossary.GlossarySettings.OffValue
                : null);
    }

    [Fact]
    public void Flushes_results_and_footer_for_the_recorded_scenarios()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-suite-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var report = Report(dir);
            report.Record("first", Tree());
            report.Record("first", Tree());

            report.Flush(console);

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
    public void An_active_scope_captures_base_teardowns()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-scope-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        NarrativeSuiteScope.Begin(Report(dir));
        try
        {
            var subject = new RecordingSubject();
            subject.SetUpTrace();
            subject.Context.EnterMethod("Svc", "Do", []);
            subject.Context.ExitMethodWithReturn(null);
            subject.TearDownTrace();
        }
        finally
        {
            NarrativeSuiteScope.End(console);
        }

        try
        {
            Assert.True(File.Exists(
                Path.Combine(dir, "clarity-results.json")));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    private sealed class RecordingSubject : NarrativeTestBase
    {
    }

    [Fact]
    public void Flush_harvests_into_a_configured_glossary_and_prints_vocabulary_line()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-suite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(
            glossaryFile,
            "{\n  \"schemaVersion\": 1,\n  \"contexts\": {},\n  \"terms\": []\n}\n");
        var console = new StringWriter();
        try
        {
            var report = Report(dir, glossaryFile);
            report.Record("first", Tree());

            report.Flush(console);

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
    public void Empty_report_flushes_nothing()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-suite-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();

        Report(dir).Flush(console);

        Assert.False(Directory.Exists(dir));
        Assert.Equal(string.Empty, console.ToString());
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

    private static string TempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-suite-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
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

    [Fact]
    public void The_committed_glossary_raises_the_suite_clarity_score()
    {
        var plain = TempDir();
        var glossed = TempDir();
        var glossaryFile = Path.Combine(glossed, "glossary.json");
        File.WriteAllText(glossaryFile, TradingGlossary);
        try
        {
            var without = Report(plain);
            without.Record("folds a tranche", TrancheTree());
            without.Flush(TextWriter.Null);

            var with = Report(glossed, glossaryFile);
            with.Record("folds a tranche", TrancheTree());
            with.Flush(TextWriter.Null);

            Assert.True(OverallScore(glossed) > OverallScore(plain));
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
            Directory.Delete(glossed, recursive: true);
        }
    }

    [Fact]
    public void A_malformed_glossary_degrades_instead_of_failing_the_suite()
    {
        var dir = TempDir();
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(glossaryFile, "{ not json");
        var console = new StringWriter();
        try
        {
            var report = Report(dir, glossaryFile);
            report.Record("folds a tranche", TrancheTree());

            report.Flush(console);

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

    [Fact]
    public void A_registered_loss_source_is_named_once_in_the_footer()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "nt-nunit-suite-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var report = Report(dir);
            var lossy = new StubLossSource(new TraceLoss(0, 2, 30));
            report.Record("first", Tree());

            // Same rule as the xUnit fixture: one capture, counted once.
            report.ReportLoss(lossy);
            report.ReportLoss(lossy);
            report.Flush(console);

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
            Path.GetTempPath(), "nt-nunit-suite-" + Guid.NewGuid().ToString("N"));
        var console = new StringWriter();
        try
        {
            var report = Report(dir);
            report.Record("first", Tree());

            report.Flush(console);

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
        var report = Report(Path.GetTempPath());

        Assert.Throws<ArgumentNullException>(() => report.ReportLoss(null!));
    }

    private sealed class StubLossSource(TraceLoss loss) : ITraceLossSource
    {
        public TraceLoss TraceLoss { get; } = loss;
    }
}
