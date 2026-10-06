// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// The always-on pointer an agent reads on its next session. Its text is the catalogue's own, so the
/// cases here pin the frame around it: the markers, the detected facts, and which commands a project
/// is told to run. Method names mirror the Java <c>AgentsMdBlockTest</c>; this port names the CLI
/// verbs (there is no Gradle alternative) and carries two rules, not three (D6).
/// </summary>
public sealed class AgentsMdBlockTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-agentsmd-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private Carrier CarrierWith(string name, string version = "1.2.3") =>
        Carriers.Open(Path.Combine(_root, name), version);

    [Fact]
    public void IsOneWellFormedRegionStampedWithTheCarrier()
    {
        var carrier = CarrierWith("one", "1.2.3");

        var block = AgentsMdBlock.Render(carrier, ProjectState.NewBuilder().Build());

        var scan = MarkedBlock.ScanText(block);
        Assert.Empty(scan.Problems);
        Assert.Single(scan.Regions);
        Assert.Equal("NarrativeTrace.Skills/1.2.3", scan.Regions[0].Coordinate);
        Assert.StartsWith("<!-- narrativetrace:start ", block, StringComparison.Ordinal);
        Assert.EndsWith("<!-- narrativetrace:end -->\n", block, StringComparison.Ordinal);
    }

    [Fact]
    public void ListsEverySkillWithItsCatalogueDescriptionVerbatimAndInOrder()
    {
        var carrier = CarrierWith("order");

        var block = AgentsMdBlock.Render(carrier, ProjectState.NewBuilder().Build());

        var previous = -1;
        foreach (var skill in carrier.Skills)
        {
            var line = "- `" + skill.Name + "` — " + skill.Description;
            var at = block.IndexOf(line, StringComparison.Ordinal);
            Assert.True(at > previous, "catalogue order is kept");
            previous = at;
        }
    }

    [Fact]
    public void NamesWhereTracesLand()
    {
        var carrier = CarrierWith("traces");

        Assert.Contains("`out/traces`",
            AgentsMdBlock.Render(carrier, ProjectState.NewBuilder().OutputDirectory("out/traces").Build()),
            StringComparison.Ordinal);
        Assert.Contains("`TestResults/narrativetrace`",
            AgentsMdBlock.Render(carrier, ProjectState.NewBuilder().Build()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void NamesTheCommandLineVerbs()
    {
        var block = AgentsMdBlock.Render(CarrierWith("verbs"), ProjectState.NewBuilder().Build());

        Assert.Contains("dotnet tool run dotnet-narrativetrace doctor", block, StringComparison.Ordinal);
        Assert.Contains("dotnet tool run dotnet-narrativetrace init --dry-run", block, StringComparison.Ordinal);
        Assert.Contains("dotnet tool run dotnet-narrativetrace uninstall", block, StringComparison.Ordinal);
        Assert.DoesNotContain("./gradlew", block, StringComparison.Ordinal);
    }

    [Fact]
    public void PointsAtTheRuntimesDocumentationIndex()
    {
        Assert.Contains("https://narrativetrace.ai/dotnet/llms.txt",
            AgentsMdBlock.Render(CarrierWith("docs"), ProjectState.NewBuilder().Build()),
            StringComparison.Ordinal);
    }

    [Fact]
    public void CarriesTheRulesTheEvaluationsKeepTrippingOver()
    {
        var block = AgentsMdBlock.Render(CarrierWith("rules"), ProjectState.NewBuilder().Build());

        Assert.Contains("redaction", block, StringComparison.Ordinal);
        Assert.Contains(".received.nt", block, StringComparison.Ordinal);
        // This port's prompt carries no parameter-name / compiler-flag rule, so the block invents none.
        Assert.DoesNotContain("-parameters", block, StringComparison.Ordinal);
    }

    [Fact]
    public void SaysNothingAboutAVersionBeyondTheMachineWrittenStamp()
    {
        var block = AgentsMdBlock.Render(CarrierWith("noversion"), ProjectState.NewBuilder().Build());
        var withoutTheMarker = block[block.IndexOf('\n', StringComparison.Ordinal)..];

        Assert.DoesNotMatch(@"\d+\.\d+\.\d+", withoutTheMarker);
    }

    [Fact]
    public void RefusesToRenderWithoutACarrierOrAProject()
    {
        Assert.Throws<ArgumentException>(() => AgentsMdBlock.Render(null!, ProjectState.NewBuilder().Build()));
        Assert.Throws<ArgumentException>(() => AgentsMdBlock.Render(CarrierWith("nullstate"), null!));
    }

    [Fact]
    public void IsTheSameTextForTheSameInputs()
    {
        var carrier = CarrierWith("same");
        var state = ProjectState.NewBuilder().Build();

        Assert.Equal(AgentsMdBlock.Render(carrier, state), AgentsMdBlock.Render(carrier, state));
    }
}
