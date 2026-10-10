// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.SkillsDevTool;
using Xunit;

namespace NarrativeTrace.Cli.Tests;

/// <summary>
/// The clarity skill's replay executors, held to what they claim: a replay that cannot find the
/// fixture's built assembly FAILS rather than passing on nothing, and the build gate it vouches for is
/// really declared by the MSBuild package.
/// </summary>
public sealed class ClarityReplayTests : IDisposable
{
    private readonly string _empty = Path.Combine(Path.GetTempPath(), "nt-clarity-" + Guid.NewGuid().ToString("N"));

    public ClarityReplayTests() => Directory.CreateDirectory(_empty);

    public void Dispose() => Directory.Delete(_empty, recursive: true);

    [Fact]
    public void A_fixture_with_no_built_assembly_has_none_to_scan()
    {
        Assert.Null(ClarityReplay.FixtureAssembly(_empty));
    }

    [Fact]
    public void A_scan_of_an_unbuilt_fixture_does_not_succeed_and_a_check_of_it_is_not_decisive()
    {
        Assert.False(ClarityReplay.ScanSucceeds(RepoRoot.Find(), _empty));
        Assert.False(ClarityReplay.CheckIsDecisive(RepoRoot.Find(), _empty));
    }

    [Fact]
    public void The_msbuild_package_declares_the_check_target_and_the_property_the_skill_names()
    {
        Assert.True(ClarityReplay.MsBuildGateIsDeclared(RepoRoot.Find()));
    }

    [Fact]
    public void The_check_against_the_poorly_named_clarity_example_is_decisive()
    {
        var fixture = Path.Combine(RepoRoot.Find(), "examples", "NarrativeTrace.Examples.Clarity");

        Assert.True(ClarityReplay.CheckIsDecisive(RepoRoot.Find(), fixture));
    }
}
