// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// Command and verify literals for the clarity skill — the <c>dotnet tool run</c> spelling of the
/// <c>clarity-scan</c> and <c>clarity-check</c> verbs, because a skill's closed command vocabulary is
/// <c>dotnet</c> and <c>git</c>. Public: the Tier A2 replay keys its executors off these exact
/// constants rather than a second, hand-copied literal.
/// </summary>
/// <remarks>
/// <para>
/// <b>@llmNote</b> The gate is the CLI, not the MSBuild targets, on purpose. The <c>ClarityScan</c> and
/// <c>ClarityCheck</c> targets shell out to a bare <c>dotnet-narrativetrace</c>, which a LOCAL tool
/// manifest never puts on <c>PATH</c>; the verbs behind <c>dotnet tool run</c> work with either install.
/// The targets stay a step of their own, taken only when a build gate is asked for.
/// </para>
/// <para>
/// The <c>&lt;project&gt;</c> placeholder is the one thing the agent substitutes: the built assembly
/// of the project being scanned.
/// </para>
/// </remarks>
public static class ClarityCommands
{
    /// <summary>The directory both reports are written to, relative to the project.</summary>
    public const string ReportDirectory = "narrativetrace";

    /// <summary>The JSON report the scan writes and the gate reads.</summary>
    public const string JsonReportPath = ReportDirectory + "/clarity-scan-results.json";

    /// <summary>Restores the local tool manifest so the verbs below resolve.</summary>
    public const string RestoreTool = "dotnet tool restore";

    /// <summary>Builds the project so the scan has an assembly to read.</summary>
    public const string Build = "dotnet build --configuration Release";

    /// <summary>Scans the built assembly by reflection, running nothing, into both reports.</summary>
    public const string Scan =
        "dotnet tool run dotnet-narrativetrace clarity-scan --assembly bin/Release/<target framework>/<project>.dll " +
        "--output-dir " + ReportDirectory;

    /// <summary>The gate: exits non-zero while any HIGH issue remains.</summary>
    public const string Check =
        "dotnet tool run dotnet-narrativetrace clarity-check --results " + JsonReportPath + " --max-high-issues 0";

    /// <summary>The build gate, for a project that asks for one.</summary>
    public const string BuildGate = "dotnet build --target ClarityCheck --property ClarityMaxHighIssues=0";

    /// <summary>The scan step's definition of done: both reports exist, written from the built assembly.</summary>
    public const string VerifyScanWritten =
        "the scan reported a nonzero type count and wrote fresh reports from the built " +
        "assembly, which it read by reflection without running anything";
}
