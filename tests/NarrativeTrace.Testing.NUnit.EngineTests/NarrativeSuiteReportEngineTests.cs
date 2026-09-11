// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using NarrativeTrace.TestingNUnit;
using NUnit.Framework;

namespace NarrativeTrace.Testing.NUnit.EngineTests;

/// <summary>
/// Behavioural coverage of <see cref="NarrativeSuiteReport"/> under the real
/// NUnit3 adapter. The sibling <c>NarrativeTrace.Testing.NUnit.Tests</c>
/// project pins the same contracts under xUnit; this class asserts the same
/// behaviour while running through the framework the report actually ships to,
/// so a regression that only manifests under NUnit's own scope/runner (the
/// gap <see cref="OrderServiceTests"/> exists to close) is caught here too.
/// </summary>
[TestFixture]
public sealed class NarrativeSuiteReportEngineTests
{
    private static TraceTree Tree()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);
    }

    // Glossary pinned OFF unless a test opts in, so a synthetic suite can never
    // reach a real glossary.json committed above the test bin folder.
    private static NarrativeSuiteReport Report(string dir, string? glossary = null)
    {
        return new NarrativeSuiteReport(
            dir,
            key => key == GlossarySettings.EnvKey
                ? glossary ?? GlossarySettings.OffValue
                : null);
    }

    private static string TempDir()
    {
        return Path.Combine(
            Path.GetTempPath(), "nt-nunit-engine-" + Guid.NewGuid().ToString("N"));
    }

    [Test]
    public void Flush_writes_results_and_a_footer_counting_the_recorded_scenarios()
    {
        var dir = TempDir();
        using var console = new StringWriter();
        try
        {
            var report = Report(dir);
            report.Record("first", Tree());
            report.Record("first", Tree());

            report.Flush(console);

            Assert.That(
                File.Exists(Path.Combine(dir, "clarity-results.json")), Is.True);
            Assert.That(
                console.ToString(), Does.Contain("2 scenarios recorded"));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Test]
    public void An_empty_report_writes_nothing_and_prints_nothing()
    {
        var dir = TempDir();
        using var console = new StringWriter();

        Report(dir).Flush(console);

        Assert.That(Directory.Exists(dir), Is.False);
        Assert.That(console.ToString(), Is.Empty);
    }

    [Test]
    public void A_registered_loss_source_is_summed_once_even_if_reported_twice()
    {
        var dir = TempDir();
        using var console = new StringWriter();
        try
        {
            var report = Report(dir);
            var lossy = new StubLossSource(new TraceLoss(0, 2, 30));
            report.Record("first", Tree());

            // A shared context reported per test must be counted once, not
            // multiplied — the reason sources are read at flush, not summed
            // as reported.
            report.ReportLoss(lossy);
            report.ReportLoss(lossy);
            report.Flush(console);

            Assert.That(
                console.ToString(),
                Does.Contain("Incomplete: 2 async scopes not adopted (cap, 30 spans)"));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Test]
    public void A_suite_with_no_registered_loss_prints_no_loss_line()
    {
        var dir = TempDir();
        using var console = new StringWriter();
        try
        {
            var report = Report(dir);
            report.Record("first", Tree());

            report.Flush(console);

            Assert.That(console.ToString(), Does.Not.Contain("Incomplete"));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Test]
    public void ReportLoss_rejects_a_null_source()
    {
        var report = Report(Path.GetTempPath());

        Assert.That(() => report.ReportLoss(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void A_malformed_glossary_degrades_to_the_built_in_dictionaries()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(glossaryFile, "{ not json");
        using var console = new StringWriter();
        try
        {
            var report = Report(dir, glossaryFile);
            report.Record("first", Tree());

            report.Flush(console);

            // The suite still produces its results — vocabulary governance
            // never outranks the test outcome.
            Assert.That(
                File.Exists(Path.Combine(dir, "clarity-results.json")), Is.True);
            Assert.That(
                console.ToString(), Does.Contain("built-in dictionaries only"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Test]
    public void A_configured_glossary_is_harvested_and_the_vocabulary_line_printed()
    {
        var dir = TempDir();
        Directory.CreateDirectory(dir);
        var glossaryFile = Path.Combine(dir, "glossary.json");
        File.WriteAllText(
            glossaryFile,
            "{\n  \"schemaVersion\": 1,\n  \"contexts\": {},\n  \"terms\": []\n}\n");
        using var console = new StringWriter();
        try
        {
            var report = Report(dir, glossaryFile);
            report.Record("first", Tree());

            report.Flush(console);

            Assert.That(console.ToString(), Does.Contain("Vocabulary:"));
            // The recorded scenario's verb is harvested into the committed file.
            Assert.That(
                File.ReadAllText(glossaryFile), Does.Contain("place order"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class StubLossSource(TraceLoss loss) : ITraceLossSource
    {
        public TraceLoss TraceLoss { get; } = loss;
    }
}
