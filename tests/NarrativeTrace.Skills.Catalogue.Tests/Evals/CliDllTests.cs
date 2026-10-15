// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsEvals;
using Xunit;

namespace NarrativeTrace.SkillsEvals.Tests;

/// <summary>
/// The one locator of the built CLI assembly, shared by the graders' environment and the installer
/// delivery: the most recently written build under <c>src/NarrativeTrace.Cli/bin</c>, or a plain
/// answer that there is none.
/// </summary>
public sealed class CliDllTests : IDisposable
{
    private readonly string _repo = Path.Combine(Path.GetTempPath(), "nt-clidll-" + Guid.NewGuid().ToString("N"));

    public void Dispose() => Directory.Delete(_repo, recursive: true);

    private string Build(string configuration, DateTime writtenAt)
    {
        var dir = Path.Combine(_repo, "src", "NarrativeTrace.Cli", "bin", configuration, "net10.0");
        Directory.CreateDirectory(dir);
        var dll = Path.Combine(dir, "dotnet-narrativetrace.dll");
        File.WriteAllText(dll, "");
        File.SetLastWriteTimeUtc(dll, writtenAt);
        return dll;
    }

    [Fact]
    public void An_unbuilt_cli_is_not_located()
    {
        Directory.CreateDirectory(_repo);

        Assert.Null(CliDll.Locate(_repo));
    }

    [Fact]
    public void The_most_recently_written_build_wins()
    {
        Build("Debug", new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var newer = Build("Release", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(newer, CliDll.Locate(_repo));
    }

    [Fact]
    public void Requiring_an_unbuilt_cli_names_the_project_to_build()
    {
        Directory.CreateDirectory(_repo);

        var error = Assert.Throws<InvalidOperationException>(() => CliDll.Require(_repo));

        Assert.Contains("src/NarrativeTrace.Cli", error.Message, StringComparison.Ordinal);
    }
}
