// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.SixtySeconds.Tests;

/// <summary>
/// Saves a byte-stable captured-stdout file for
/// <c>documentation/first-10-minutes.md</c>'s output blocks to embed via
/// <c>snippet-check</c> — the design note's other option alongside the
/// runtime's own test-artifact writer (<see cref="Testing.Xunit.NarrativeFixture.WriteArtifacts"/>),
/// used here because the page's output blocks are the bare renderer line the
/// tutorial actually prints, not that writer's framed <c>traces/&lt;Class&gt;/</c>
/// artifact.
/// </summary>
internal static class CapturedOutput
{
    /// <summary>
    /// The repository root, found by climbing up from this test assembly's
    /// build output directory — same technique and same depth as
    /// <c>tests/BuildScript.Tests/NukeBuildTests.cs</c>'s own <c>RepoRoot</c>,
    /// since both sit at <c>tests/&lt;Project&gt;/bin/&lt;Config&gt;/&lt;TFM&gt;/</c>.
    /// </summary>
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    /// <summary>
    /// Writes <paramref name="content"/> to
    /// <c>artifacts/sixty-seconds/&lt;name&gt;</c> — gitignored, like every other
    /// generated file under <c>artifacts/</c>, and a fixed, TFM-independent
    /// path so <c>snippet-check</c>'s marker in the page never has to change.
    /// </summary>
    public static string Write(string name, string content)
    {
        var directory = Path.Combine(RepoRoot, "artifacts", "sixty-seconds");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
