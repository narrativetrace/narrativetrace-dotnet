// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Glossary;
using Xunit;

namespace NarrativeTrace.Glossary.Tests;

public sealed class GlossarySuiteReporterTests : IDisposable
{
    private static readonly DateTime Today = new(2026, 8, 11, 0, 0, 0, DateTimeKind.Utc);

    private readonly string root = Path.Combine(
        Path.GetTempPath(), $"glossary-suite-{Guid.NewGuid():N}");

    private readonly string glossaryFile;
    private readonly string outputDir;
    private readonly StringWriter console = new();

    public GlossarySuiteReporterTests()
    {
        Directory.CreateDirectory(root);
        glossaryFile = Path.Combine(root, "glossary.json");
        outputDir = Path.Combine(root, "narrativetrace-output");
    }

    public void Dispose()
    {
        console.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Harvests_traces_into_the_glossary_and_emits_every_surface()
    {
        WriteEmptyBillingGlossary();

        Write(Entries(Node("OverdraftService", "openOverdraftAccount")));

        var updated = GlossaryJsonReader.Read(File.ReadAllText(glossaryFile));
        Assert.Contains(updated.Terms, term => term.Term == "overdraft account");
        Assert.Contains(
            "# Domain Glossary",
            File.ReadAllText(Path.Combine(root, "glossary.md")),
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(outputDir, GlossaryUsageReport.FileName)));
        Assert.Contains(
            "new terms harvested", console.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Reports_deprecated_synonym_use_in_the_summary_line()
    {
        File.WriteAllText(glossaryFile, """
            {
              "schemaVersion": 1,
              "contexts": { "billing": { "packages": ["Acme.Billing"] } },
              "terms": [
                { "term": "overdraft account", "context": "billing",
                  "kind": "noun-phrase", "status": "curated",
                  "synonyms": [ { "alias": "account with overdraft" } ],
                  "firstSeen": "2026-08-11" }
              ]
            }
            """);

        Write(
            Entries(Node("AccountService", "openAccountWithOverdraft")),
            className => "Acme.Billing");

        Assert.Contains(
            "deprecated synonym in use", console.ToString(), StringComparison.Ordinal);
        Assert.Contains(
            "openAccountWithOverdraft → use openOverdraftAccount",
            console.ToString(),
            StringComparison.Ordinal);
        var updated = GlossaryJsonReader.Read(File.ReadAllText(glossaryFile));
        Assert.DoesNotContain(
            updated.Terms, term => term.Term == "account with overdraft");
    }

    [Fact]
    public void Unchanged_vocabulary_leaves_the_glossary_file_untouched()
    {
        WriteEmptyBillingGlossary();
        Write(Entries(Node("OverdraftService", "openOverdraftAccount")));
        var stamp = new DateTime(2000, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(glossaryFile, stamp);

        Write(Entries(Node("OverdraftService", "openOverdraftAccount")));

        Assert.Equal(stamp, File.GetLastWriteTimeUtc(glossaryFile));
    }

    /// <summary>
    /// The production overload must resolve real namespaces, or bounded
    /// contexts are inert on the path that ships: every harvested term lands
    /// in <c>_unassigned</c> and translation, which is context-scoped,
    /// resolves nothing.
    /// </summary>
    [Fact]
    public void Production_overload_resolves_namespaces_into_their_bounded_context()
    {
        File.WriteAllText(glossaryFile, """
            {
              "schemaVersion": 1,
              "contexts": {
                "probe": { "packages": ["NarrativeTrace.Glossary.Tests.Wiring"] }
              },
              "terms": []
            }
            """);

        GlossarySuiteReporter.Write(
            Entries(Node(nameof(Wiring.SuiteWiringProbeService), "chargeProbe")),
            glossaryFile,
            outputDir,
            console);

        var updated = GlossaryJsonReader.Read(File.ReadAllText(glossaryFile));
        Assert.All(updated.Terms, term => Assert.Equal("probe", term.Context));
        Assert.Contains(updated.Terms, term => term.Term == "charge probe");
    }

    [Fact]
    public void Missing_glossary_file_and_empty_suite_are_silent_no_ops()
    {
        GlossarySuiteReporter.Write(
            Entries(Node("Svc", "charge")), glossaryFile, outputDir, console);
        WriteEmptyBillingGlossary();
        GlossarySuiteReporter.Write([], glossaryFile, outputDir, console);
        GlossarySuiteReporter.Write(
            Entries(Node("Svc", "charge")), null, outputDir, console);

        Assert.Equal(string.Empty, console.ToString());
        Assert.False(Directory.Exists(outputDir));
    }

    [Fact]
    public void Malformed_glossary_fails_loudly()
    {
        File.WriteAllText(glossaryFile, """{"schemaVersion": 1}""");

        Assert.Throws<ArgumentException>(
            () => Write(Entries(Node("Svc", "charge"))));
    }

    [Fact]
    public void Rejects_null_and_blank_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => GlossarySuiteReporter.Write(
            null!, glossaryFile, outputDir, console));
        Assert.Throws<ArgumentException>(() => GlossarySuiteReporter.Write(
            [], glossaryFile, " ", console));
        Assert.Throws<ArgumentNullException>(() => GlossarySuiteReporter.Write(
            [], glossaryFile, outputDir, null!));
        Assert.Throws<ArgumentNullException>(() => GlossarySuiteReporter.Write(
            [], glossaryFile, outputDir, console, null!, _ => null));
        Assert.Throws<ArgumentNullException>(() => GlossarySuiteReporter.Write(
            [], glossaryFile, outputDir, console, () => Today, null!));
    }

    private void WriteEmptyBillingGlossary()
    {
        File.WriteAllText(glossaryFile, """
            {
              "schemaVersion": 1,
              "contexts": { "billing": { "packages": ["Acme.Billing"] } },
              "terms": []
            }
            """);
    }

    private void Write(
        IReadOnlyList<KeyValuePair<string, TraceTree>> entries,
        Func<string, string?>? namespaceOf = null)
    {
        GlossarySuiteReporter.Write(
            entries, glossaryFile, outputDir, console,
            () => Today, namespaceOf ?? (_ => null));
    }

    private static List<KeyValuePair<string, TraceTree>> Entries(params TraceNode[] roots)
    {
        return [new KeyValuePair<string, TraceTree>("scenario", new TraceTree(roots))];
    }

    private static TraceNode Node(string className, string methodName)
    {
        return new TraceNode(
            new MethodSignature(className, methodName, []),
            new Incomplete(),
            [],
            0);
    }
}
