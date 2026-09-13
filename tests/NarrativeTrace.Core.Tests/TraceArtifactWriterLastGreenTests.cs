// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// The last-green lifecycle: the <c>.nt</c> file on disk is the last-GREEN
/// baseline — a non-green run compares against it but never overwrites it —
/// and every write reports the scenario's <see cref="ScenarioDelta"/> against
/// that baseline. Mirrors the Java runtime's <c>LastGreenLifecycleTest</c>.
/// </summary>
public sealed class TraceArtifactWriterLastGreenTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "nt-lastgreen-" + Guid.NewGuid().ToString("N"));

    private static readonly TraceArtifactRenderers Stubs = new(
        _ => "MERMAID", _ => "PLANTUML", (_, _) => "JSON", _ => "CANONICAL", _ => "STRUCTURAL");

    private static TraceTree TreeWithCalls(params string[] methods)
    {
        var nodes = methods.Select(m =>
            new TraceNode(new MethodSignature("Svc", m, []), new Returned(null), [], 0)).ToArray();
        return new TraceTree(nodes);
    }

    private string NtFile() => Path.Combine(_dir, "structural", "OrderTests", "places_order.nt");

    private ScenarioDelta? Write(TraceTree tree, bool failed)
    {
        return TraceArtifactWriter.Write(
            tree, "OrderTests", "PlacesOrder", "places an order", failed, _dir,
            TraceArtifactFormat.Markdown, Stubs, TextWriter.Null);
    }

    [Fact]
    public void First_green_run_writes_the_baseline_and_reports_new()
    {
        var delta = Write(TreeWithCalls("A"), failed: false);

        Assert.Equal(ScenarioDeltaKind.New, delta!.Kind);
        Assert.True(File.Exists(NtFile()));
    }

    [Fact]
    public void Second_identical_green_run_reports_unchanged()
    {
        Write(TreeWithCalls("A"), failed: false);

        var delta = Write(TreeWithCalls("A"), failed: false);

        Assert.Equal(ScenarioDeltaKind.Unchanged, delta!.Kind);
    }

    /// <summary>
    /// Reproduces the exact sequence the brief calls out: approved baseline,
    /// add a call, run, fail — baseline unchanged; remove the call, run —
    /// "unchanged since last green".
    /// </summary>
    [Fact]
    public void A_failed_run_never_advances_the_baseline_and_reverting_reports_unchanged()
    {
        Write(TreeWithCalls("A"), failed: false);
        var baselineAfterGreen = File.ReadAllText(NtFile());

        var deltaOnFailure = Write(TreeWithCalls("A", "B"), failed: true);

        Assert.Equal(ScenarioDeltaKind.Changed, deltaOnFailure!.Kind);
        Assert.Equal("+1 call Svc.B", deltaOnFailure.Summary);
        Assert.Equal(baselineAfterGreen, File.ReadAllText(NtFile()));

        var deltaAfterRevert = Write(TreeWithCalls("A"), failed: false);

        Assert.Equal(ScenarioDeltaKind.Unchanged, deltaAfterRevert!.Kind);
        Assert.Equal(baselineAfterGreen, File.ReadAllText(NtFile()));
    }

    [Fact]
    public void A_green_run_with_a_real_structural_change_advances_the_baseline()
    {
        Write(TreeWithCalls("A"), failed: false);

        var delta = Write(TreeWithCalls("A", "B"), failed: false);

        Assert.Equal(ScenarioDeltaKind.Changed, delta!.Kind);
        Assert.Contains("Svc.B", File.ReadAllText(NtFile()), StringComparison.Ordinal);
    }

    [Fact]
    public void Non_markdown_formats_produce_no_delta_since_they_write_no_structural_artifact()
    {
        var delta = TraceArtifactWriter.Write(
            TreeWithCalls("A"), "OrderTests", "PlacesOrder", "places an order", failed: false, _dir,
            TraceArtifactFormat.Text, Stubs, TextWriter.Null);

        Assert.Null(delta);
    }

    [Fact]
    public void Per_invocation_writes_do_not_overwrite_each_others_baseline()
    {
        var first = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 1, "kayak");
        var second = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 2, "tent");

        TraceArtifactWriter.Write(
            TreeWithCalls("FindKayak"), first, "find kayak", failed: false, _dir,
            TraceArtifactFormat.Markdown, Stubs, TextWriter.Null);
        TraceArtifactWriter.Write(
            TreeWithCalls("FindTent"), second, "find tent", failed: false, _dir,
            TraceArtifactFormat.Markdown, Stubs, TextWriter.Null);

        var resolver = new OutputDirectoryResolver(_dir);
        Assert.Contains("FindKayak", File.ReadAllText(resolver.StructuralFile(first)), StringComparison.Ordinal);
        Assert.Contains("FindTent", File.ReadAllText(resolver.StructuralFile(second)), StringComparison.Ordinal);
    }

    /// <summary>The header rule end to end: the .nt file's scenario line never carries the display name.</summary>
    [Fact]
    public void Invocation_nt_header_is_the_structural_scenario_not_the_display_name()
    {
        var identity = ArtifactIdentity.OfInvocation("EquipmentTests", "equipmentCanBeFound", 2, "find TENT");

        TraceArtifactWriter.Write(
            TreeWithCalls("FindTent"), identity, "find TENT", failed: false, _dir,
            TraceArtifactFormat.Markdown, Stubs, TextWriter.Null);

        var resolver = new OutputDirectoryResolver(_dir);
        var nt = File.ReadAllText(resolver.StructuralFile(identity));
        Assert.StartsWith("scenario: Equipment can be found #2\n", nt);
        Assert.DoesNotContain("TENT", nt, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
