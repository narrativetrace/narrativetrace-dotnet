// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

using System.Text.Json;

using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The writer-driven schema gate: the per-test entry artifacts are validated as
/// the bytes that land on disk, produced by the real writer and the real
/// serializer.
/// </summary>
/// <remarks>
/// A schema test that hand-builds its input proves the schema matches the test,
/// not the product. This one goes through <see cref="TraceArtifactWriter"/> with
/// the renderers the shipped integrations wire, reads the file back, and
/// validates every element of it.
/// </remarks>
public sealed class CanonicalArtifactSchemaTests : IDisposable
{
    private const string Secret = "alice@example.com";

    private static readonly TraceArtifactRenderers Renderers = new(
        MermaidStub,
        MermaidStub,
        (tree, metadata) => JsonExporter.Export(tree, metadata),
        CanonicalEntryArrayExporter.Canonical,
        CanonicalEntryArrayExporter.Structural);

    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-canonical-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Every_entry_of_the_written_canonical_artifact_validates()
    {
        WriteArtifacts(new EntryArtifacts(Canonical: true, Structural: false));

        AssertEveryEntryValidates(ReadArtifact("canonical"));
    }

    [Fact]
    public void Every_entry_of_the_written_structural_artifact_validates()
    {
        WriteArtifacts(new EntryArtifacts(Canonical: false, Structural: true));

        AssertEveryEntryValidates(ReadArtifact("structural"));
    }

    [Fact]
    public void The_canonical_artifact_holds_one_enter_and_one_exit_per_node()
    {
        WriteArtifacts(new EntryArtifacts(Canonical: true, Structural: false));

        var entries = ReadArtifact("canonical");

        Assert.Equal(4, entries.Count);
        Assert.Equal(
            ["method_enter", "method_enter", "method_exit", "method_exit"],
            entries.Select(e => e.GetProperty("nt.eventType").GetString()));
    }

    [Fact]
    public void The_canonical_artifact_carries_the_captured_values()
    {
        WriteArtifacts(new EntryArtifacts(Canonical: true, Structural: false));

        Assert.Contains(
            Secret,
            File.ReadAllText(ArtifactPath("canonical")),
            StringComparison.Ordinal);
    }

    [Fact]
    public void The_structural_artifact_carries_none_of_them()
    {
        WriteArtifacts(new EntryArtifacts(Canonical: false, Structural: true));

        var json = File.ReadAllText(ArtifactPath("structural"));

        Assert.DoesNotContain(Secret, json, StringComparison.Ordinal);
        Assert.Contains(StructuralProjection.Elided, json, StringComparison.Ordinal);
        Assert.Contains("customerEmail", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_artifacts_describe_the_same_call_shape()
    {
        WriteArtifacts(new EntryArtifacts(Canonical: true, Structural: true));

        var canonical = ReadArtifact("canonical");
        var structural = ReadArtifact("structural");

        Assert.Equal(
            canonical.Select(Identity), structural.Select(Identity));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static string MermaidStub(TraceTree tree) => "MERMAID";

    private static (string?, string?, string?) Identity(JsonElement entry) =>
        (entry.GetProperty("span_id").GetString(),
            entry.GetProperty("code.function").GetString(),
            entry.GetProperty("nt.eventType").GetString());

    private static void AssertEveryEntryValidates(IReadOnlyList<JsonElement> entries)
    {
        Assert.NotEmpty(entries);
        foreach (var entry in entries)
        {
            SchemaValidator.AssertValid("entry.schema.json", entry.GetRawText());
        }
    }

    private void WriteArtifacts(EntryArtifacts entryArtifacts)
    {
        TraceArtifactWriter.Write(
            Tree(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Renderers,
            TextWriter.Null, entryArtifacts);
    }

    /// <summary>
    /// Reads the artifact back as detached elements — cloned, because a
    /// <see cref="JsonElement"/> dies with the document it was parsed from.
    /// </summary>
    private IReadOnlyList<JsonElement> ReadArtifact(string kind)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(ArtifactPath(kind)));
        return document.RootElement.EnumerateArray()
            .Select(element => element.Clone())
            .ToList();
    }

    private string ArtifactPath(string kind) =>
        Path.Combine(_dir, "traces", "OrderTests", $"places_order.{kind}.json");

    private static TraceTree Tree()
    {
        var charge = new TraceNode(
            new MethodSignature("PaymentService", "Charge", []),
            new Threw(new InvalidOperationException("card " + Secret + " declined")),
            [],
            0);
        return new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "OrderService",
                    "PlaceOrder",
                    [new ParameterCapture("customerEmail", "\"" + Secret + "\"", false)]),
                new Returned("\"ORD-" + Secret + "\""),
                [charge],
                TimeSpan.TicksPerMillisecond * 3),
        ]);
    }
}
