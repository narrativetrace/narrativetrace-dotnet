// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

/// <summary>
/// <c>config.approval-mode</c>: committed <c>.approved.nt</c> baselines guard nothing while approval
/// mode is off — every test passes whatever the structure does. "On" is read where this port's switch,
/// the <c>NARRATIVETRACE_APPROVAL</c> environment variable, is actually set: the doctor's own
/// environment, a <c>.runsettings</c> a project references, or test code that sets it — from live
/// text only (comments set nothing), the key as a whole word, the value as the runtime parses it.
/// </summary>
public sealed class ConfigApprovalModeCheckTests
{
    private const string Settings = """
        <RunSettings>
          <RunConfiguration>
            <EnvironmentVariables>
              <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>
            </EnvironmentVariables>
          </RunConfiguration>
        </RunSettings>
        """;

    private const string ReferencingProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <RunSettingsFilePath>$(MSBuildProjectDirectory)/approval.runsettings</RunSettingsFilePath>
          </PropertyGroup>
        </Project>
        """;

    private static DoctorSnapshot WithBaseline() => DoctorSnapshotFixtures.Empty() with
    {
        ApprovedDirFiles = new Dictionary<string, string> { ["Orders/place.approved.nt"] = "scenario: x\n" },
    };

    private static DoctorFinding Run(DoctorSnapshot snapshot) => ConfigApprovalModeCheck.Run(snapshot);

    private static DoctorSnapshot WithEnv(string? value) => WithBaseline() with
    {
        Env = new Dictionary<string, string?> { ["NARRATIVETRACE_APPROVAL"] = value },
    };

    private static DoctorSnapshot WithRunSettings(string settings, string project = ReferencingProject) =>
        WithBaseline() with
        {
            RunSettingsFiles = new Dictionary<string, string> { ["tests/approval.runsettings"] = settings },
            ProjectFiles = new Dictionary<string, string> { ["tests/App.Tests.csproj"] = project },
        };

    private static DoctorSnapshot WithSource(string source) => WithBaseline() with
    {
        SourceFiles = new Dictionary<string, string> { ["tests/ApprovalOn.cs"] = source },
    };

    [Fact]
    public void Passes_with_nothing_to_compare_when_no_baseline_is_committed()
    {
        var finding = Run(DoctorSnapshotFixtures.Empty() with
        {
            ApprovedDirFiles = new Dictionary<string, string> { ["Orders/place.received.nt"] = "x" },
        });

        Assert.True(finding.Passed);
        Assert.Equal("config.approval-mode", finding.Id);
        Assert.Contains("nothing", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Fails_when_baselines_exist_and_nothing_switches_approval_mode_on()
    {
        var finding = Run(WithBaseline());

        Assert.False(finding.Passed);
        Assert.Contains("approval mode is off", finding.Message, StringComparison.Ordinal);
        Assert.Contains("<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>", finding.Fix, StringComparison.Ordinal);
        Assert.Contains("<RunSettingsFilePath>", finding.Fix, StringComparison.Ordinal);
        Assert.Contains("NARRATIVETRACE_APPROVAL=true dotnet", finding.Fix, StringComparison.Ordinal);
        Assert.Equal("narrativetrace-verify", finding.Skill);
    }

    [Theory]
    [InlineData("true")]
    [InlineData(" TRUE ")]
    [InlineData("1")]
    public void Passes_when_the_doctors_own_environment_switches_it_on(string value)
    {
        Assert.True(Run(WithEnv(value)).Passed);
    }

    [Theory]
    [InlineData("false")]
    [InlineData("yes")]
    [InlineData("")]
    [InlineData(null)]
    public void Fails_on_an_environment_value_the_runtime_reads_as_off(string? value)
    {
        Assert.False(Run(WithEnv(value)).Passed);
    }

    [Fact]
    public void Passes_when_a_referenced_runsettings_sets_the_variable()
    {
        Assert.True(Run(WithRunSettings(Settings)).Passed);
    }

    [Fact]
    public void Passes_on_a_runsettings_value_in_another_case_or_one_padded_with_space()
    {
        Assert.True(Run(WithRunSettings(Settings.Replace(">true<", "> True <", StringComparison.Ordinal))).Passed);
        Assert.True(Run(WithRunSettings(Settings.Replace(">true<", ">1<", StringComparison.Ordinal))).Passed);
    }

    [Fact]
    public void Fails_when_no_project_references_the_runsettings()
    {
        var finding = Run(WithRunSettings(Settings, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>"));

        Assert.False(finding.Passed);
    }

    [Fact]
    public void Fails_when_the_reference_is_only_in_a_comment()
    {
        var commented = "<Project><!-- <RunSettingsFilePath>a.runsettings</RunSettingsFilePath> --></Project>";

        Assert.False(Run(WithRunSettings(Settings, commented)).Passed);
    }

    [Fact]
    public void Fails_when_the_runsettings_switch_is_only_in_a_comment()
    {
        var commented = Settings.Replace(
            "<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>",
            "<!-- <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL> -->",
            StringComparison.Ordinal);

        Assert.False(Run(WithRunSettings(commented)).Passed);
    }

    [Theory]
    [InlineData("<NARRATIVETRACE_APPROVAL>false</NARRATIVETRACE_APPROVAL>")]
    [InlineData("<NARRATIVETRACE_APPROVAL>trueish</NARRATIVETRACE_APPROVAL>")]
    [InlineData("<NARRATIVETRACE_APPROVED_DIR>true</NARRATIVETRACE_APPROVED_DIR>")]
    [InlineData("<X_NARRATIVETRACE_APPROVAL>true</X_NARRATIVETRACE_APPROVAL>")]
    [InlineData("<NARRATIVETRACE_APPROVALS>true</NARRATIVETRACE_APPROVALS>")]
    public void Fails_on_a_runsettings_near_miss(string element)
    {
        var settings = Settings.Replace(
            "<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>", element, StringComparison.Ordinal);

        Assert.False(Run(WithRunSettings(settings)).Passed);
    }

    [Theory]
    [InlineData("Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"true\");")]
    [InlineData("System.Environment.SetEnvironmentVariable( \"NARRATIVETRACE_APPROVAL\" , \"1\" );")]
    [InlineData("Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"True\");")]
    [InlineData("Environment.SetEnvironmentVariable(ConfigResolver.ApprovalKey, \"true\");")]
    public void Passes_when_live_test_code_sets_the_variable(string statement)
    {
        Assert.True(Run(WithSource("static class On { static void Init() { " + statement + " } }")).Passed);
    }

    [Theory]
    [InlineData("// Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"true\");")]
    [InlineData("/* Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"true\"); */")]
    [InlineData("Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"false\");")]
    [InlineData("Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVALS\", \"true\");")]
    [InlineData("Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVED_DIR\", \"true\");")]
    [InlineData("MySetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"true\");")]
    public void Fails_on_test_code_that_does_not_switch_it_on(string statement)
    {
        Assert.False(Run(WithSource("static class On { static void Init() { " + statement + " } }")).Passed);
    }

    [Fact]
    public void Is_registered_with_the_doctor()
    {
        Assert.Contains(DoctorChecks.Run(WithBaseline()).Findings, f => f.Id == "config.approval-mode");
    }
}
