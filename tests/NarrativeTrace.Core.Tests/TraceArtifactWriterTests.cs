// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using System.Text;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public sealed class TraceArtifactWriterTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-artifact-" + Guid.NewGuid().ToString("N"));

    private static readonly TraceArtifactRenderers Stubs = new(
        _ => "MERMAID", _ => "PLANTUML", (_, _) => "JSON",
        _ => "CANONICAL", _ => "STRUCTURAL");

    private static string RealJson(TraceTree tree, TraceMetadata metadata)
    {
        return NarrativeTrace.Runtime.JsonExporter.Export(tree, metadata);
    }

    private static TraceTree TreeWithNode()
    {
        return new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "PlacesOrder", []),
                new Returned(null), [], 0),
        ]);
    }

    [Theory]
    [InlineData(TraceArtifactFormat.Markdown, ".md")]
    [InlineData(TraceArtifactFormat.Text, ".txt")]
    [InlineData(TraceArtifactFormat.Mermaid, ".mmd")]
    [InlineData(TraceArtifactFormat.PlantUml, ".puml")]
    public void Extension_maps_each_format(TraceArtifactFormat format, string ext)
    {
        Assert.Equal(ext, TraceArtifactWriter.Extension(format));
    }

    [Fact]
    public void Markdown_writes_the_md_json_and_diagram_companions()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null);

        Assert.True(File.Exists(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.md")));
        Assert.True(File.Exists(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.json")));
        Assert.True(File.Exists(
            Path.Combine(_dir, "diagrams", "OrderTests", "places_order.mmd")));
    }

    /// <summary>
    /// Regression: <c>diagrams/</c> and <c>structural/</c> derive their directory segment from the
    /// test class name the same way the primary trace directory does, and used to reach
    /// <see cref="Path.Combine(string, string, string)"/> unsanitized. See
    /// <c>OutputDirectoryResolverTests</c> for the primary-directory half of this fix.
    /// </summary>
    [Fact]
    public void A_class_name_carrying_a_separator_cannot_escape_the_output_directory_for_companions()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "../../etc", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null);

        Assert.True(File.Exists(Path.Combine(_dir, "diagrams", "_etc", "places_order.mmd")));
        Assert.True(File.Exists(Path.Combine(_dir, "structural", "_etc", "places_order.nt")));
    }

    [Fact]
    public void The_entry_arrays_are_off_unless_asked_for()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null);

        Assert.False(File.Exists(TraceFile("places_order.canonical.json")));
        Assert.False(File.Exists(TraceFile("places_order.structural.json")));
    }

    [Fact]
    public void Each_entry_array_is_written_beside_the_trace_when_switched_on()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null,
            new EntryArtifacts(Canonical: true, Structural: true));

        Assert.Equal("CANONICAL", File.ReadAllText(TraceFile("places_order.canonical.json")));
        Assert.Equal("STRUCTURAL", File.ReadAllText(TraceFile("places_order.structural.json")));
    }

    [Fact]
    public void The_canonical_array_can_be_asked_for_on_its_own()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null, new EntryArtifacts(Canonical: true, Structural: false));

        Assert.True(File.Exists(TraceFile("places_order.canonical.json")));
        Assert.False(File.Exists(TraceFile("places_order.structural.json")));
    }

    [Fact]
    public void The_entry_arrays_do_not_depend_on_the_markdown_format()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Text, Stubs,
            TextWriter.Null,
            new EntryArtifacts(Canonical: true, Structural: true));

        Assert.True(File.Exists(TraceFile("places_order.txt")));
        Assert.True(File.Exists(TraceFile("places_order.canonical.json")));
        Assert.True(File.Exists(TraceFile("places_order.structural.json")));
    }

    [Fact]
    public void An_empty_trace_writes_no_entry_arrays_either()
    {
        TraceArtifactWriter.Write(
            new TraceTree([]), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null,
            new EntryArtifacts(Canonical: true, Structural: true));

        Assert.False(Directory.Exists(Path.Combine(_dir, "traces")));
    }

    private string TraceFile(string fileName) =>
        Path.Combine(_dir, "traces", "OrderTests", fileName);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Json_companion_does_not_smuggle_the_outcome_into_timestamp(bool failed)
    {
        // The real JSON exporter, not the stub — the point is the bytes on disk.
        var renderers = new TraceArtifactRenderers(
            _ => "MERMAID", _ => "PLANTUML", RealJson,
            _ => "CANONICAL", _ => "STRUCTURAL");

        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed, _dir, TraceArtifactFormat.Markdown, renderers,
            TextWriter.Null);

        var json = File.ReadAllText(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var scenario = doc.RootElement.GetProperty("scenario");

        Assert.False(
            scenario.TryGetProperty("timestamp", out var timestamp)
            && timestamp.GetString() is "PASSED" or "FAILED",
            "the test outcome must not be written into the timestamp field");
    }

    [Theory]
    [InlineData(true, "error")]
    [InlineData(false, "success")]
    public void Json_companion_reports_the_test_outcome_as_scenario_result(
        bool failed, string expected)
    {
        var renderers = new TraceArtifactRenderers(
            _ => "MERMAID", _ => "PLANTUML", RealJson,
            _ => "CANONICAL", _ => "STRUCTURAL");

        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed, _dir, TraceArtifactFormat.Markdown, renderers,
            TextWriter.Null);

        var json = File.ReadAllText(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.Equal(
            expected,
            doc.RootElement.GetProperty("scenario")
                .GetProperty("result").GetString());
    }

    [Fact]
    public void Raw_method_name_is_humanized_into_the_scenario_heading()
    {
        // Java's TraceTestSupport humanizes displayName before it reaches the
        // metadata, the JSON and the .nt artifact; only frame() sees it raw.
        var renderers = new TraceArtifactRenderers(
            _ => "MERMAID", _ => "PLANTUML", RealJson,
            _ => "CANONICAL", _ => "STRUCTURAL");

        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "PlacesOrder",
            failed: false, _dir, TraceArtifactFormat.Markdown, renderers,
            TextWriter.Null);

        var nt = File.ReadAllText(
            Path.Combine(_dir, "structural", "OrderTests", "places_order.nt"));
        Assert.StartsWith("scenario: Places order\n", nt);

        var json = File.ReadAllText(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.json"));
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(
            "Places order",
            doc.RootElement.GetProperty("scenario")
                .GetProperty("name").GetString());
    }

    [Fact]
    public void Text_format_keeps_the_raw_name_so_frame_can_strip_its_prefix()
    {
        // Humanizing first would insert a space and defeat the Test_/Should_
        // prefix strip that Frame does, so the Text path must see it raw.
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "Should_PlaceOrder",
            "Should_PlaceOrder", failed: false, _dir,
            TraceArtifactFormat.Text, Stubs, TextWriter.Null);

        var txt = File.ReadAllText(
            Path.Combine(_dir, "traces", "OrderTests", "should_place_order.txt"));

        Assert.StartsWith("Scenario: Place order\n", txt);
    }

    [Theory]
    [InlineData(false, "PASSED")]
    [InlineData(true, "FAILED")]
    public void Markdown_artifact_carries_the_document_header(
        bool failed, string expectedResult)
    {
        // Java's TraceTestSupport renders via renderDocument, so the per-test
        // .md carries a document header. This runtime emitted frontmatter without
        // one until 2026-08-28.
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "PlacesOrder",
            failed, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null);

        var md = File.ReadAllText(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.md"));

        Assert.Contains("## Trace: Svc.PlacesOrder", md);
        Assert.Contains("**Scenario:** Places order", md);
        Assert.Contains($"**Result:** {expectedResult}", md);
        Assert.Contains("### Call Flow", md);
        // The wire spelling must never reach human-facing prose.
        Assert.DoesNotContain("**Result:** success", md);
        Assert.DoesNotContain("**Result:** error", md);
    }

    [Fact]
    public void Markdown_writes_the_value_free_structural_nt_companion()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null);

        var nt = Path.Combine(
            _dir, "structural", "OrderTests", "places_order.nt");
        Assert.Equal(
            "scenario: places order\n\n- Svc.PlacesOrder()\n",
            File.ReadAllText(nt));
    }

    /// <summary>
    /// The .nt file is a cross-platform approval baseline, so its bytes are the
    /// contract: UTF-8 without a BOM, LF only, one trailing newline.
    /// </summary>
    [Fact]
    public void Structural_companion_is_utf8_without_bom_and_lf_terminated()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Café", "Prépare", []),
                new Returned("1"), [], 0),
        ]);

        TraceArtifactWriter.Write(
            tree, "OrderTests", "PlacesOrder", "places order", failed: false,
            _dir, TraceArtifactFormat.Markdown, Stubs, TextWriter.Null);

        var bytes = File.ReadAllBytes(Path.Combine(
            _dir, "structural", "OrderTests", "places_order.nt"));
        Assert.DoesNotContain((byte)'\r', bytes);
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.Equal(
            "scenario: places order\n\n- Café.Prépare() → value\n",
            new UTF8Encoding(false, true).GetString(bytes));
    }

    [Fact]
    public void Non_markdown_format_writes_no_structural_companion()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Text, Stubs,
            TextWriter.Null);

        Assert.False(Directory.Exists(Path.Combine(_dir, "structural")));
    }

    [Fact]
    public void Non_markdown_format_writes_only_the_primary_file()
    {
        TraceArtifactWriter.Write(
            TreeWithNode(), "OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Mermaid, Stubs,
            TextWriter.Null);

        Assert.True(File.Exists(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.mmd")));
        Assert.False(Directory.Exists(Path.Combine(_dir, "diagrams")));
        Assert.False(File.Exists(
            Path.Combine(_dir, "traces", "OrderTests", "places_order.json")));
    }

    [Fact]
    public void Empty_trace_writes_nothing()
    {
        TraceArtifactWriter.Write(
            new TraceTree([]), "OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, Stubs,
            TextWriter.Null);

        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void Console_echo_frames_the_scenario_and_reports_the_path()
    {
        var console = new StringWriter();

        TraceArtifactWriter.Write(
            TreeWithNode(), "OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Mermaid, Stubs, console);

        var text = console.ToString();
        Assert.Contains("Trace written:", text, StringComparison.Ordinal);
        Assert.Contains("places_order.mmd", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// A java security fuzz suite finding, mirrored here as the writer's own guarantee:
    /// whatever a renderer hands it — this runtime's shipped renderers already sanitize narration and
    /// captured values before <see cref="TraceArtifactWriter.Write"/> ever sees them, but the
    /// injected <see cref="TraceArtifactRenderers"/> delegate bundle is public API a caller can
    /// supply their own renderer through — the write itself must substitute an unencodable
    /// character rather than throw. .NET's default UTF-8 encoding raises
    /// <see cref="System.Text.EncoderFallbackException"/> on a lone surrogate; an observability
    /// failure must never become a failed test run.
    /// </summary>
    [Fact]
    public void A_renderer_output_carrying_an_unpaired_surrogate_does_not_fail_the_write()
    {
        var renderers = new TraceArtifactRenderers(
            _ => "MERMAID", _ => "PLANTUML", (_, _) => "{\"value\": \"a\ud800b\"}",
            _ => "CANONICAL", _ => "STRUCTURAL");

        var exception = Record.Exception(() => TraceArtifactWriter.Write(
            TreeWithNode(), "Foo.OrderTests", "PlacesOrder", "places order",
            failed: false, _dir, TraceArtifactFormat.Markdown, renderers, TextWriter.Null));

        Assert.Null(exception);
        var json = File.ReadAllText(Path.Combine(_dir, "traces", "OrderTests", "places_order.json"));
        Assert.Contains("a�b", json, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
