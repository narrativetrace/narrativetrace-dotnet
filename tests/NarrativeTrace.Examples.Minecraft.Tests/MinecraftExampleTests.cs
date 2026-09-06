// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Examples.Common;
using NarrativeTrace.Examples.Minecraft;
using Xunit;

namespace NarrativeTrace.Examples.Minecraft.Tests;

public sealed class MinecraftExampleTests
{
    private static string RunExample()
    {
        using var output = new StringWriter();
        using var run = new DemoRun(new ConsoleLoggerFactory(output, LogFormat.Bare), "minecraft");
        MinecraftExample.Run(run);
        return output.ToString();
    }

    [Fact]
    public void Run_traces_both_halves_and_renders_the_refactored_one_as_diagrams()
    {
        var text = RunExample();

        var headers = text.Split('\n').Where(l => l.StartsWith("=== ", StringComparison.Ordinal)).ToList();
        Assert.Equal(
            ["=== Refactored: Player Joins World ===", "=== Unrefactored: Player Joins World ==="],
            headers);
        Assert.Equal(2, text.Split("--- Trace tree ---").Length - 1);
        Assert.Contains("--- Mermaid ---", text, StringComparison.Ordinal);
        Assert.Contains("--- PlantUML ---", text, StringComparison.Ordinal);
        Assert.Contains("→ IWorldServer.PlayerJoined(playerName: \"Steve\")", text, StringComparison.Ordinal);
        Assert.Matches(@"refactored \d\.\d\d vs unrefactored \d\.\d\d", text);
    }
}
