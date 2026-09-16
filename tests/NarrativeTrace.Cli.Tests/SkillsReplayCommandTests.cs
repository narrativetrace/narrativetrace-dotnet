// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>Locates the repository root from this test assembly's build output directory.</summary>
internal static class RepoRoot
{
    public static string Find()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "NarrativeTrace.sln")))
        {
            dir = Path.GetDirectoryName(dir);
        }

        return dir ?? throw new InvalidOperationException(
            $"could not find NarrativeTrace.sln above {AppContext.BaseDirectory}");
    }
}

/// <summary>
/// Tier A2 (skill-harness design §4.2): the real oracle replay, against the real repository and its
/// real <c>examples/NarrativeTrace.Examples.SixtySeconds</c> fixture, no fakes, no LLM — real
/// <c>dotnet</c> subprocesses via <see cref="SkillReplayRegistry"/>. This is what proves the
/// catalogue's own step data still executes today against this commit's code, the same guarantee
/// the golden TypeScript source's own Tier A2 block proves for its fixture.
/// </summary>
public sealed class SkillsReplayCommandTests : IDisposable
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _err = new();

    public void Dispose()
    {
        _out.Dispose();
        _err.Dispose();
    }

    [Fact]
    public void Both_catalogue_skills_replay_cleanly_against_the_real_sixty_seconds_fixture()
    {
        var exit = SkillsReplayCommand.Run(RepoRoot.Find(), _out, _err);

        Assert.Equal(0, exit);
        Assert.Contains("0 problems", _out.ToString(), StringComparison.Ordinal);
        Assert.Empty(_err.ToString());
    }

    /// <summary>
    /// Pins the bug class the v0.1.4 publish run died of, not just that one instance: every replay
    /// subprocess is spawned with <c>--no-build</c>, so the configuration it is told to use must be
    /// the one this build actually produced — <c>Debug</c> locally, <c>Release</c> on a server
    /// (<c>build/Build.cs</c>: <c>IsLocalBuild ? Debug : Release</c>). The <c>dotnet</c> CLI's own
    /// default is <c>Debug</c> unconditionally, which is invisible on a developer machine (a
    /// <c>bin/Debug</c> from an earlier build is always lying around) and fatal on a cold CI
    /// checkout that only ever built <c>Release</c>. Asserting against the directory this test
    /// assembly was itself loaded from is what makes the check environment-carried rather than
    /// environment-assumed.
    /// </summary>
    [Fact]
    public void Replay_subprocesses_target_the_configuration_this_build_actually_produced()
    {
        var builtInto = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

        Assert.Equal(builtInto, SkillReplayRegistry.BuildConfiguration);
    }

    [Fact]
    public void A_missing_fixture_is_reported_as_a_problem_never_an_unhandled_exception()
    {
        var emptyRoot = Path.Combine(Path.GetTempPath(), "nt-skills-replay-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyRoot);
        try
        {
            var exit = SkillsReplayCommand.Run(emptyRoot, _out, _err);

            Assert.Equal(1, exit);
            Assert.Contains("problem", _out.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(emptyRoot, recursive: true);
        }
    }
}
