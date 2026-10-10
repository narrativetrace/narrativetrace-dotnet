// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Tests.Doctor;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

/// <summary>
/// Adversarial probes of <c>config.approval-mode</c>: comments, CDATA, reference shape, the env
/// value grammar, and <c>SetEnvironmentVariable</c> near-misses.
/// </summary>
public sealed class AdversarialPhase7ApprovalModeTests
{
    private const string Baseline = "narratives/OrderTests/places.approved.nt";
    private const string ReferenceProject =
        "<Project><PropertyGroup><RunSettingsFilePath>approval.runsettings</RunSettingsFilePath></PropertyGroup></Project>";

    private static DoctorSnapshot WithBaseline() =>
        DoctorSnapshotFixtures.WithProject() with
        {
            ApprovedDirFiles = new Dictionary<string, string> { [Baseline] = "scenario: x\n" },
        };

    private static bool Passes(DoctorSnapshot snapshot) => ConfigApprovalModeCheck.Run(snapshot).Passed;

    private static DoctorSnapshot WithRunSettings(string settings, string project = ReferenceProject) =>
        WithBaseline() with
        {
            ProjectFiles = new Dictionary<string, string> { ["app.csproj"] = project },
            RunSettingsFiles = new Dictionary<string, string> { ["approval.runsettings"] = settings },
        };

    private static string Settings(string body) =>
        "<RunSettings><RunConfiguration><EnvironmentVariables>" + body +
        "</EnvironmentVariables></RunConfiguration></RunSettings>";

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("1")]
    [InlineData(" 1 ")]
    [InlineData("  True  ")]
    public void Env_switch_with_a_true_value_in_any_case_or_one_turns_approval_on(string value)
    {
        var snapshot = WithBaseline() with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_APPROVAL"] = value },
        };

        Assert.True(Passes(snapshot));
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("")]
    public void Env_switch_with_any_other_value_leaves_approval_off(string value)
    {
        var snapshot = WithBaseline() with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_APPROVAL"] = value },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Env_key_that_is_only_a_prefix_of_the_approved_dir_key_does_not_turn_approval_on()
    {
        var snapshot = WithBaseline() with
        {
            Env = new Dictionary<string, string?> { ["NARRATIVETRACE_APPROVED_DIR"] = "true" },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Runsettings_switch_inside_an_xml_comment_does_not_count()
    {
        var snapshot = WithRunSettings(Settings(
            "<!-- <NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL> -->"));

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Runsettings_switch_inside_a_cdata_section_does_not_count()
    {
        var snapshot = WithRunSettings(Settings(
            "<![CDATA[<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>]]>"));

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Runsettings_switch_whose_value_is_split_across_lines_with_padding_counts()
    {
        var snapshot = WithRunSettings(Settings(
            "<NARRATIVETRACE_APPROVAL>\n    true\n  </NARRATIVETRACE_APPROVAL>"));

        Assert.True(Passes(snapshot));
    }

    [Fact]
    public void Runsettings_switch_with_a_value_that_merely_starts_with_true_does_not_count()
    {
        var snapshot = WithRunSettings(Settings(
            "<NARRATIVETRACE_APPROVAL>truer</NARRATIVETRACE_APPROVAL>"));

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Reference_to_runsettings_inside_a_project_comment_does_not_count()
    {
        var snapshot = WithRunSettings(
            Settings("<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>"),
            "<Project><!-- <RunSettingsFilePath>approval.runsettings</RunSettingsFilePath> --></Project>");

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Runsettings_file_that_no_project_references_does_not_turn_approval_on()
    {
        var snapshot = WithRunSettings(
            Settings("<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>"),
            "<Project></Project>");

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Runsettings_switch_does_not_count_when_the_project_references_a_different_runsettings_file()
    {
        var snapshot = WithRunSettings(
            Settings("<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>"),
            "<Project><PropertyGroup><RunSettingsFilePath>other.runsettings</RunSettingsFilePath></PropertyGroup></Project>");

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Switch_written_as_a_string_literal_outside_SetEnvironmentVariable_does_not_count()
    {
        var snapshot = WithBaseline() with
        {
            SourceFiles = new Dictionary<string, string>
            {
                ["Tests.cs"] = "class T { const string s = \"NARRATIVETRACE_APPROVAL=true\"; }",
            },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void SetEnvironmentVariable_with_the_approval_key_constant_counts()
    {
        var snapshot = WithBaseline() with
        {
            SourceFiles = new Dictionary<string, string>
            {
                ["Tests.cs"] = "class T { void S() => System.Environment.SetEnvironmentVariable(ConfigResolver.ApprovalKey, \"true\"); }",
            },
        };

        Assert.True(Passes(snapshot));
    }

    [Fact]
    public void SetEnvironmentVariable_inside_a_line_comment_does_not_count()
    {
        var snapshot = WithBaseline() with
        {
            SourceFiles = new Dictionary<string, string>
            {
                ["Tests.cs"] = "class T { void S() { // Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"true\");\n } }",
            },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void SetEnvironmentVariable_setting_the_switch_to_false_does_not_count()
    {
        var snapshot = WithBaseline() with
        {
            SourceFiles = new Dictionary<string, string>
            {
                ["Tests.cs"] = "class T { void S() => Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", \"false\"); }",
            },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void SetEnvironmentVariable_on_the_approved_dir_key_does_not_turn_approval_on()
    {
        var snapshot = WithBaseline() with
        {
            SourceFiles = new Dictionary<string, string>
            {
                ["Tests.cs"] = "class T { void S() => Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVED_DIR\", \"true\"); }",
            },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void SetEnvironmentVariable_with_a_non_literal_value_cannot_be_read_and_does_not_count()
    {
        var snapshot = WithBaseline() with
        {
            SourceFiles = new Dictionary<string, string>
            {
                ["Tests.cs"] = "class T { void S(string v) => Environment.SetEnvironmentVariable(\"NARRATIVETRACE_APPROVAL\", v); }",
            },
        };

        Assert.False(Passes(snapshot));
    }

    [Fact]
    public void Received_nt_alone_is_not_a_baseline_so_the_check_passes_with_nothing_to_compare()
    {
        var snapshot = DoctorSnapshotFixtures.WithProject() with
        {
            ApprovedDirFiles = new Dictionary<string, string> { ["narratives/A/x.received.nt"] = "scenario: x\n" },
        };

        Assert.True(Passes(snapshot));
    }

    [Fact]
    public void A_file_named_approved_nt_without_a_scenario_is_not_a_baseline()
    {
        var snapshot = DoctorSnapshotFixtures.WithProject() with
        {
            ApprovedDirFiles = new Dictionary<string, string> { ["narratives/approved.nt"] = "scenario: x\n" },
        };

        Assert.True(Passes(snapshot));
    }

    [Fact]
    public void A_baseline_nested_several_folders_deep_still_counts_and_fails_with_approval_off()
    {
        var snapshot = DoctorSnapshotFixtures.WithProject() with
        {
            ApprovedDirFiles = new Dictionary<string, string> { ["narratives/A/B/C/x.approved.nt"] = "scenario: x\n" },
        };

        Assert.False(Passes(snapshot));
    }
}
