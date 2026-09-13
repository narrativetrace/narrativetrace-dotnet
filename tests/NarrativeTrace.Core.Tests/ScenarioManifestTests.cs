// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class ScenarioManifestTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-manifest-" + Guid.NewGuid().ToString("N"));

    private static readonly TraceArtifactRenderers Stubs = new(
        _ => "MERMAID", _ => "PLANTUML", (_, _) => "JSON", _ => "CANONICAL", _ => "STRUCTURAL");

    private static TraceTree TreeWithNode()
    {
        return new TraceTree([
            new TraceNode(new MethodSignature("Svc", "PlacesOrder", []), new Returned(null), [], 0),
        ]);
    }

    [Fact]
    public void EntryFor_lists_only_the_artifacts_actually_written()
    {
        var identity = ArtifactIdentity.OfMethod("Foo.OrderTests", "PlacesOrder");
        TraceArtifactWriter.Write(
            TreeWithNode(), identity, "places an order", failed: false, _dir,
            TraceArtifactFormat.Markdown, Stubs, TextWriter.Null);

        var entry = ScenarioManifest.EntryFor(_dir, identity, "Places an order");

        var roles = entry.Artifacts.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.Equal("traces/OrderTests/places_order.md".Replace('/', Path.DirectorySeparatorChar), roles["trace"]);
        Assert.Equal(
            "traces/OrderTests/places_order.json".Replace('/', Path.DirectorySeparatorChar), roles["json"]);
        Assert.Equal(
            "diagrams/OrderTests/places_order.mmd".Replace('/', Path.DirectorySeparatorChar), roles["diagram"]);
        Assert.Equal(
            "structural/OrderTests/places_order.nt".Replace('/', Path.DirectorySeparatorChar),
            roles["structural"]);
        Assert.False(roles.ContainsKey("canonicalJson"));
    }

    [Fact]
    public void EntryFor_omits_a_role_whose_file_was_never_written()
    {
        var identity = ArtifactIdentity.OfMethod("Foo.OrderTests", "PlacesOrder");
        TraceArtifactWriter.Write(
            TreeWithNode(), identity, "places an order", failed: false, _dir,
            TraceArtifactFormat.Mermaid, Stubs, TextWriter.Null);

        var entry = ScenarioManifest.EntryFor(_dir, identity, "Places an order");

        Assert.True(entry.Artifacts.Any(kv => kv.Key == "trace"));
        Assert.False(entry.Artifacts.Any(kv => kv.Key == "json"));
        Assert.False(entry.Artifacts.Any(kv => kv.Key == "structural"));
    }

    [Fact]
    public void Write_writes_nothing_for_an_empty_entry_list()
    {
        ScenarioManifest.Write([], _dir);

        Assert.False(File.Exists(Path.Combine(_dir, "manifest.json")));
    }

    [Fact]
    public void Write_creates_manifest_json_in_the_output_directory()
    {
        var entry = new ScenarioManifest.Entry(
            "Places an order", ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder"),
            [new KeyValuePair<string, string>("trace", "traces/OrderTests/places_order.md")]);

        ScenarioManifest.Write([entry], _dir);

        Assert.True(File.Exists(Path.Combine(_dir, "manifest.json")));
    }

    [Fact]
    public void Render_includes_the_invocation_index_only_for_an_invocation()
    {
        var invocation = new ScenarioManifest.Entry(
            "Equipment can be found #2",
            ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 2, "find TENT"),
            []);
        var ordinary = new ScenarioManifest.Entry(
            "Places an order", ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder"), []);

        var rendered = ScenarioManifest.Render([invocation, ordinary]);

        Assert.Contains("\"invocation\": 2", rendered, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(rendered, "\"invocation\""));
    }

    [Fact]
    public void Render_escapes_quotes_and_backslashes_in_scenario_and_artifact_values()
    {
        var entry = new ScenarioManifest.Entry(
            "Says \"hi\"", ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder"),
            [new KeyValuePair<string, string>("trace", "a\\b\"c")]);

        var rendered = ScenarioManifest.Render([entry]);

        Assert.Contains("Says \\\"hi\\\"", rendered, StringComparison.Ordinal);
        Assert.Contains("a\\\\b\\\"c", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_produces_valid_json()
    {
        var entry = new ScenarioManifest.Entry(
            "Places an order", ArtifactIdentity.OfMethod("OrderTests", "PlacesOrder"),
            [new KeyValuePair<string, string>("trace", "traces/OrderTests/places_order.md")]);

        var rendered = ScenarioManifest.Render([entry]);

        using var doc = System.Text.Json.JsonDocument.Parse(rendered);
        Assert.Equal(
            "narrativetrace/scenario-manifest/1",
            doc.RootElement.GetProperty("schema").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("scenarios").GetArrayLength());
    }

    private static int CountOccurrences(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }

        return count;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
