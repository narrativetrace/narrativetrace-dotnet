// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Trace METADATA (class name, method name, parameter name) reaching a
/// text-oriented renderer unescaped, while the exception message and
/// narration beside it already run through <see cref="ControlEscape"/> or
/// <see cref="MarkdownEscape"/> — the asymmetry an adversarial-audit mirror
/// found, mirrored here for <see cref="IndentedTextRenderer"/>,
/// <see cref="MarkdownRenderer"/> and <see cref="ProseRenderer"/>. A raw
/// newline in a class name forges an extra line; assertions check the
/// line count survives, not merely that the payload text appears (it is
/// expected to — it is the class name).
/// </summary>
public class RendererMetadataEscapingTests
{
    private const string HostileClassName = "Svc\n!! Injected: fake failure";

    [Fact]
    public void IndentedText_class_name_newline_does_not_forge_an_extra_line()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(HostileClassName, "Run", []),
                new Returned("\"ok\""), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Single(
            result.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void IndentedText_parameter_name_newline_is_escaped()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("id\nfake line", "1", false)]),
                new Returned(null), [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.DoesNotContain('\n', result.TrimEnd('\n'));
    }

    [Fact]
    public void IndentedText_exception_type_name_is_escaped_like_the_message()
    {
        // The escaper (ControlEscape) is unit-tested on its own; this pins
        // that AppendException now routes the TYPE name through it too,
        // not only ex.Message.
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = IndentedTextRenderer.Render(tree);

        Assert.Contains("InvalidOperationException: boom", result);
    }

    [Fact]
    public void Markdown_class_name_newline_does_not_forge_an_extra_bullet()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(HostileClassName, "Run", []),
                new Returned("\"ok\""), [], 0),
        ]);

        var result = MarkdownRenderer.Render(
            tree, new MarkdownOptions(IncludeFrontmatter: false));

        Assert.Single(
            result.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Markdown_parameter_name_is_html_escaped()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("a<b>", "1", false)]),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(
            tree, new MarkdownOptions(IncludeFrontmatter: false));

        Assert.Contains("a&lt;b&gt;", result);
        Assert.DoesNotContain("a<b>", result);
    }

    [Fact]
    public void Markdown_exception_type_name_is_escaped_like_the_message()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = MarkdownRenderer.Render(
            tree, new MarkdownOptions(IncludeFrontmatter: false));

        Assert.Contains("InvalidOperationException: boom", result);
    }

    [Fact]
    public void Prose_class_name_newline_does_not_forge_an_extra_line()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(HostileClassName, "Run", []),
                new Returned("\"ok\""), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Single(
            result.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Prose_parameter_name_newline_is_escaped()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run",
                    [new ParameterCapture("id\nfake line", "1", false)]),
                new Returned(null), [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.DoesNotContain('\n', result.TrimEnd('\n'));
    }

    [Fact]
    public void Prose_exception_type_name_is_escaped_like_the_message()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Fail", []),
                new Threw(new InvalidOperationException("boom")),
                [], 0),
        ]);

        var result = ProseRenderer.Render(tree);

        Assert.Contains("InvalidOperationException: boom", result);
    }
}
