// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// Seeds a scratch repository root with every file a catalogue skill embeds as a listing, so the
/// skills devtool's render and lint verbs can run against it. One copy for every test class that
/// renders — a new listing is added here once, not in each class.
/// </summary>
internal static class SkillListingSeed
{
    public static void Seed(string root)
    {
        Write(root, "examples/NarrativeTrace.Examples.SixtySeconds/Program.cs", "// program\n");
        Write(root, "examples/NarrativeTrace.Examples.SixtySeconds.WithLogger/Program.cs", "// with logger\n");

        // The verify/debug skills embed compiled listings from the sixty-seconds test project.
        const string Listings = "tests/NarrativeTrace.Examples.SixtySeconds.Tests/";
        Write(root, Listings + "PlaceOrderFlowTests.cs", "// flow test\n");
        Write(root, Listings + "approval.runsettings", "<RunSettings />\n");
        Write(
            root,
            Listings + "NarrativeTrace.Examples.SixtySeconds.Tests.csproj",
            "<Project>\n  <!-- snippet:begin approval-mode -->\n  <PropertyGroup />\n  <!-- snippet:end approval-mode -->\n</Project>\n");
    }

    private static void Write(string root, string relative, string content)
    {
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
