// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Diagrams;
using Xunit;

namespace NarrativeTrace.Diagrams.Tests;

/// <summary>
/// Trace METADATA (class name, method name, parameter name) reaching a
/// diagram renderer unescaped, while the rendered VALUE beside it already
/// is escaped (<see cref="DiagramText.Message"/>) — the asymmetry an
/// adversarial-audit mirror found. Assertions are structural (line count,
/// no unbalanced quote) rather than substring, because the payload IS the
/// class/method name and is expected to appear in the output; injection
/// means an extra line or a directive the renderer never wrote.
/// </summary>
public class DiagramMetadataInjectionTests
{
    private const string HostileMethodName =
        "Run\nclick Svc href \"javascript:alert(1)\"";

    private const string HostileParamName = "id\"; click Svc \"x";

    [Fact]
    public void Mermaid_method_name_newline_does_not_inject_a_click_directive()
    {
        var root = new TraceNode(
            new MethodSignature("Svc", HostileMethodName, []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);
        var lines = result.Split('\n');

        Assert.DoesNotContain(lines, l => l.TrimStart().StartsWith("click", StringComparison.Ordinal));
        Assert.DoesNotContain("\nclick", result);
    }

    [Fact]
    public void PlantUml_method_name_newline_does_not_inject_a_directive()
    {
        var root = new TraceNode(
            new MethodSignature("Svc", HostileMethodName, []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);
        var lines = result.Split('\n');

        Assert.DoesNotContain(lines, l => l.TrimStart().StartsWith("click", StringComparison.Ordinal));
        Assert.DoesNotContain("\nclick", result);
    }

    [Fact]
    public void Mermaid_parameter_name_cannot_break_out_of_the_message_line()
    {
        var root = new TraceNode(
            new MethodSignature("Svc", "Run",
                [new ParameterCapture(HostileParamName, "1", false)]),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.DoesNotContain('"', result);
    }

    [Fact]
    public void PlantUml_parameter_name_cannot_break_out_of_the_message_line()
    {
        var root = new TraceNode(
            new MethodSignature("Svc", "Run",
                [new ParameterCapture(HostileParamName, "1", false)]),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.DoesNotContain('"', result);
    }

    [Fact]
    public void Mermaid_empty_class_name_renders_the_unnamed_marker_not_a_bare_line()
    {
        var root = new TraceNode(
            new MethodSignature("", "Run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);

        Assert.Contains("<unnamed>", result);
        Assert.DoesNotContain("participant  as \n", result);
        Assert.DoesNotContain("participant  as ", result.Replace("<unnamed>", string.Empty));
    }

    [Fact]
    public void PlantUml_empty_class_name_renders_the_unnamed_marker_not_a_bare_line()
    {
        var root = new TraceNode(
            new MethodSignature("", "Run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = PlantUmlSequenceRenderer.Render(tree);

        Assert.Contains("<unnamed>", result);
    }

    [Fact]
    public void Mermaid_class_name_with_no_uppercase_and_hostile_characters_keeps_arrows_well_formed()
    {
        var root = new TraceNode(
            new MethodSignature("hostile name\nwith \"quotes\"", "run", []),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        var result = MermaidSequenceRenderer.Render(tree);
        var arrowLine = result.Split('\n')
            .First(l => l.Contains("->>", StringComparison.Ordinal));

        Assert.DoesNotContain(' ', arrowLine.TrimStart().Split("->>")[0]);
    }

    [Theory]
    [InlineData("\u2028")]
    [InlineData("\u2029")]
    [InlineData("\u0085")]
    public void Unicode_line_terminators_in_every_name_field_fold_to_a_space_on_both_grammars(string separator)
    {
        var hostile = "Pay/" + separator + "ment";
        var root = new TraceNode(
            new MethodSignature(hostile, hostile, [new ParameterCapture(hostile, "1", false)]),
            new Returned(null), [], 0);
        var tree = new TraceTree([root]);

        foreach (var diagram in new[] { MermaidSequenceRenderer.Render(tree), PlantUmlSequenceRenderer.Render(tree) })
        {
            Assert.DoesNotContain(separator, diagram);
            Assert.Contains("Pay/ ment", diagram);
        }
    }

    [Fact]
    public void Unicode_line_terminators_in_a_return_value_fold_to_a_space_on_both_grammars()
    {
        var root = new TraceNode(
            new MethodSignature("C", "m", []),
            new Returned("\"a\u2029b\u2028c\""), [], 0);
        var tree = new TraceTree([root]);

        foreach (var diagram in new[] { MermaidSequenceRenderer.Render(tree), PlantUmlSequenceRenderer.Render(tree) })
        {
            Assert.DoesNotContain('\u2028', diagram);
            Assert.DoesNotContain('\u2029', diagram);
        }
    }
}
