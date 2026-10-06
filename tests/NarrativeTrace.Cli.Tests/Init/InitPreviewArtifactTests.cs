// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;
using NarrativeTrace.Cli;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>
/// Captures a real <c>init --dry-run --json</c> preview against a fresh, empty project into a
/// committed doc's snippet-checked artifact (D12: <c>agent-skills.md</c> leads with <c>init</c>,
/// the preview embedded from a run of the built artifact). Unlike <see cref="InitVerbTests"/>, this
/// opens NO <c>--from</c> carrier: <see cref="CarrierLocator.Bundled"/> reads
/// <c>typeof(CarrierLocator).Assembly</c>'s own embedded resources, which are the REAL rendered
/// catalogue pages baked into this build's <c>NarrativeTrace.Cli</c> assembly (<c>CarrierLocator</c>'s
/// no-<c>--from</c> path) — the same thing an adopter's own
/// <c>dotnet tool run dotnet-narrativetrace init --dry-run</c> would show.
///
/// <para><c>artifacts/</c> is gitignored (mirrors <c>CapturedOutput</c> in
/// <c>NarrativeTrace.Examples.SixtySeconds.Tests</c>): this test regenerates the file on every
/// <c>./build.sh Test</c> run, and <c>SnippetCheck</c> (which <c>DependsOn(Test)</c> in
/// <c>build/Build.cs</c>) is what actually pins the doc's embedded copy against it.</para>
/// </summary>
public sealed class InitPreviewArtifactTests : IDisposable
{
    private readonly string _project = Path.Combine(
        Path.GetTempPath(), "nt-agent-skills-preview-" + Guid.NewGuid().ToString("N"));

    public InitPreviewArtifactTests()
    {
        Directory.CreateDirectory(_project);
    }

    public void Dispose()
    {
        if (Directory.Exists(_project))
        {
            Directory.Delete(_project, recursive: true);
        }
    }

    [Fact]
    public void Captures_a_fresh_project_preview_for_the_agent_skills_doc()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = CliRunner.Run(["init", "--dry-run", "--json", "--dir", _project], output, error);

        Assert.Equal(0, exit);
        Assert.Empty(error.ToString());
        using var document = JsonDocument.Parse(output.ToString());
        Assert.True(document.RootElement.GetProperty("actions").GetArrayLength() > 0);

        WriteArtifact(output.ToString());
    }

    private static void WriteArtifact(string content)
    {
        var directory = Path.Combine(RepoRoot.Find(), "artifacts", "agent-skills");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "init-preview.json"), content);
    }
}
