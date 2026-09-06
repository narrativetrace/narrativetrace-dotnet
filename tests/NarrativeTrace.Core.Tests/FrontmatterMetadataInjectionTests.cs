// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// <c>entry_point</c> is the one YAML frontmatter field built directly from
/// trace metadata (class/method name) rather than from a value already
/// passed through <see cref="YamlEscape"/> — found by mirroring an
/// adversarial audit. A class name carrying a raw colon and newline could
/// otherwise inject a sibling YAML key into the frontmatter block.
/// </summary>
public class FrontmatterMetadataInjectionTests
{
    [Fact]
    public void Entry_point_with_a_newline_and_colon_cannot_inject_a_sibling_key()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc\ninjected_key: evil", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.DoesNotContain("\ninjected_key:", result);
    }

    [Fact]
    public void Entry_point_frontmatter_block_stays_well_formed_under_injection()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature(
                    "Svc\ninjected_key: evil", "Run", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);
        var frontmatter = result.Split("---", 3)[1];
        var lines = frontmatter.Split(
            '\n', StringSplitOptions.RemoveEmptyEntries);

        // Every real frontmatter line is "key: value" at the top level — an
        // injected sibling key would add one more such line than the known,
        // fixed set this renderer ever writes for a tree with no scenario
        // option and no captured trace identity: type, entry_point,
        // method_count, error_count, result, duration_ms.
        Assert.Equal(6, lines.Length);
    }

    [Fact]
    public void Entry_point_with_ordinary_names_is_unquoted()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("OrderService", "PlaceOrder", []),
                new Returned(null), [], 0),
        ]);

        var result = MarkdownRenderer.Render(tree);

        Assert.Contains("entry_point: OrderService.PlaceOrder", result);
    }
}
