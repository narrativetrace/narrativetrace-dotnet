// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The version guard (D4), which Java did not need: a port's CLI can be a different release than the
/// project's library, so <c>init</c> warns — never refuses — when the project resolves a family version
/// different from the carrier the running CLI carries. No resolvable project version says nothing.
/// </summary>
public sealed class VersionGuardTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-guard-" + Guid.NewGuid().ToString("N"));
    private readonly Carrier _carrier;

    public VersionGuardTest()
    {
        Directory.CreateDirectory(_root);
        _carrier = Carriers.Open(Path.Combine(_root, "carrier"), "0.1.5");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private InitPlan Plan(string? resolvedFamilyVersion) =>
        InitPlanner.Plan(
            ProjectState.NewBuilder().ResolvedFamilyVersion(resolvedFamilyVersion).Build(),
            _carrier,
            InitOptions.Defaults());

    [Fact]
    public void WarnsWhenTheProjectResolvesADifferentVersion()
    {
        var warning = Assert.Single(Plan("0.1.3").Warnings);

        Assert.Contains("0.1.5", warning, StringComparison.Ordinal);
        Assert.Contains("0.1.3", warning, StringComparison.Ordinal);
        Assert.Contains("dotnet tool run dotnet-narrativetrace init", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void SaysNothingWhenTheVersionsMatch()
    {
        Assert.Empty(Plan("0.1.5").Warnings);
    }

    [Fact]
    public void SaysNothingWhenTheProjectVersionCannotBeDetermined()
    {
        Assert.Empty(Plan(null).Warnings);
    }

    [Fact]
    public void AMismatchWarnsButNeverRefusesAndTheResultStillExitsZero()
    {
        var plan = Plan("0.1.3");

        Assert.False(plan.HasRefusals);
        Assert.Equal(0, plan.ExitCode);
        Assert.NotEmpty(plan.Actions);
    }

    [Fact]
    public void TheRendererPrintsTheWarningLineBesideThePlan()
    {
        var text = PlanRenderer.RenderText(Plan("0.1.3"));

        Assert.Contains("note: this CLI carries NarrativeTrace.Skills/0.1.5", text, StringComparison.Ordinal);
        Assert.Contains("the project resolves NarrativeTrace 0.1.3", text, StringComparison.Ordinal);
    }
}
