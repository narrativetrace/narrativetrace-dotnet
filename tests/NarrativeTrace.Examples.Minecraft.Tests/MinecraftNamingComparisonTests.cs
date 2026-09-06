// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Examples.Minecraft;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Examples.Minecraft.Tests;

public sealed class MinecraftNamingComparisonTests
{
    private static SyncNarrativeContext NewContext()
    {
        return new SyncNarrativeContext(new NarrativeTraceConfig(TracingLevel.Detail));
    }

    private static string Shape(TraceNode node)
    {
        return "(" + string.Join(",", node.Children.Select(Shape)) + ")";
    }

    private static string Shape(TraceTree tree)
    {
        return string.Join(";", tree.Roots.Select(Shape));
    }

    [Fact]
    public void Clean_and_cryptic_versions_trace_to_the_same_structure()
    {
        var clean = MinecraftNamingDemo.TraceRefactored(NewContext());
        var cryptic = MinecraftNamingDemo.TraceUnrefactored(NewContext());

        Assert.Equal(Shape(clean), Shape(cryptic));
    }

    [Fact]
    public void Clean_names_score_higher_for_clarity_than_generic_names()
    {
        var (clean, cryptic) = MinecraftNamingDemo.CompareClarity();

        Assert.True(clean > cryptic, $"clean={clean} should beat cryptic={cryptic}");
    }
}
