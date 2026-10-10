// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.RegularExpressions;
using NarrativeTrace.Tooling.Frameworks;

namespace NarrativeTrace.Tooling.Doctor.Checks;

/// <summary>
/// <c>config.approval-mode</c>: committed <c>*.approved.nt</c> baselines, with approval mode off.
/// </summary>
/// <remarks>
/// <para>INTENT: a baseline is the durable form of what a flow is supposed to do, and it only guards
/// anything while a run compares against it. With approval mode off every test passes whatever the
/// structure does, so a committed baseline reads as protection the project does not have.</para>
/// <para><b>@llmNote</b> This port's switch is the <c>NARRATIVETRACE_APPROVAL</c> environment
/// variable, so "on" is read where a test run's environment is actually set: the doctor's own
/// environment (a shell or CI job that exports it); a <c>*.runsettings</c> that sets it under
/// <c>EnvironmentVariables</c> AND that a project references through <c>&lt;RunSettingsFilePath&gt;</c>,
/// matched by file name (an unreferenced file, or a reference to another one, sets nothing); or test
/// code that calls
/// <c>Environment.SetEnvironmentVariable</c> with it. Every text is read comment-free, the key as a
/// whole word (<c>NARRATIVETRACE_APPROVED_DIR</c> is not it), the value the way
/// <c>ConfigResolver</c> parses it: <c>true</c> in any case, or <c>1</c>, surrounding space
/// ignored. A <c>.received.nt</c> alone is not a baseline: it is what a first approval run writes.
/// Not read: a value inside CDATA, an MSBuild property that computes the path, a shell script — those
/// read as off, and the fix says how to make the switch visible.</para>
/// </remarks>
public static class ConfigApprovalModeCheck
{
    private const string Id = "config.approval-mode";
    private const string Key = "NARRATIVETRACE_APPROVAL";

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        if (!snapshot.ApprovedDirFiles.Keys.Any(path => path.EndsWith(".approved.nt", StringComparison.Ordinal)))
        {
            return DoctorFinding.Pass(
                Id,
                "no .approved.nt baselines — nothing for approval mode to compare yet",
                DoctorDocUrls.StructuralTraceFormat);
        }

        if (SwitchedOn(snapshot))
        {
            return DoctorFinding.Pass(
                Id,
                ".approved.nt baselines exist and approval mode is on — every run compares them",
                DoctorDocUrls.StructuralTraceFormat);
        }

        return DoctorFinding.Fail(
            Id,
            ".approved.nt baselines exist but approval mode is off — nothing compares them",
            Fix,
            DoctorDocUrls.StructuralTraceFormat);
    }

    private const string Fix =
        "Turn approval mode on for every test run: add a .runsettings beside the test project with\n" +
        "<RunSettings><RunConfiguration><EnvironmentVariables>" +
        "<NARRATIVETRACE_APPROVAL>true</NARRATIVETRACE_APPROVAL>" +
        "</EnvironmentVariables></RunConfiguration></RunSettings>\n" +
        "and reference it from the test project: " +
        "<PropertyGroup><RunSettingsFilePath>$(MSBuildProjectDirectory)/approval.runsettings</RunSettingsFilePath></PropertyGroup>\n" +
        "(for one run only: NARRATIVETRACE_APPROVAL=true dotnet test). A run whose structure differs " +
        "from its baseline then fails and writes a .received.nt to review.";

    private static bool SwitchedOn(DoctorSnapshot snapshot) =>
        IsTrue(snapshot.Env.GetValueOrDefault(Key))
        || ReferencedRunSettingsSwitchOn(snapshot)
        || snapshot.SourceTexts().Any(source => SetInCode.IsMatch(source));

    /// <summary>
    /// A run settings file that sets the switch AND is the one a project references — matched by file
    /// name, since a reference is usually written through an MSBuild property
    /// (<c>$(MSBuildProjectDirectory)/approval.runsettings</c>) the doctor does not evaluate.
    /// </summary>
    private static bool ReferencedRunSettingsSwitchOn(DoctorSnapshot snapshot)
    {
        var referenced = RunSettingsReference.Matches(snapshot.ManifestText())
            .Select(match => FileNameOf(match.Groups[1].Value))
            .Where(name => name.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return snapshot.RunSettingsFiles
            .Where(file => referenced.Contains(FileNameOf(file.Key)))
            .Any(file => SetInRunSettings.IsMatch(ManifestPatterns.WithoutCommentsAndCData(file.Value)));
    }

    private static string FileNameOf(string path) =>
        path.Trim().Replace('\\', '/').Split('/')[^1];

    private static bool IsTrue(string? value) =>
        value is not null && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1");

    // Plain compiled patterns, as the framework table's evidence is: source-generated regex code is
    // counted as this assembly's own lines by the coverage gate, and mostly never runs.
    private static readonly Regex RunSettingsReference = new(
        @"<RunSettingsFilePath\s*>([^<]*)</RunSettingsFilePath\s*>", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SetInRunSettings = new(
        @"<NARRATIVETRACE_APPROVAL\s*>\s*(?i:true|1)\s*</NARRATIVETRACE_APPROVAL\s*>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SetInCode = new(
        @"(?<![\w.])(?:System\.)?Environment\s*\.\s*SetEnvironmentVariable\s*\(\s*" +
        @"(?:""NARRATIVETRACE_APPROVAL""|(?:NarrativeTrace\.Core\.)?ConfigResolver\s*\.\s*ApprovalKey)" +
        @"\s*,\s*""\s*(?i:true|1)\s*""\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
