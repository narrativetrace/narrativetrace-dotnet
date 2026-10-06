// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Doctor;

/// <summary>
/// Which catalogue skill fixes each class of finding — the table <see cref="DoctorFinding.Skill"/>
/// reads.
/// </summary>
/// <remarks>
/// <para>INTENT: a finding tells a person what is wrong and how to fix it; an agent with the skills
/// installed can also be told WHICH tested procedure to follow next, by name, instead of improvising
/// one. That mapping belongs in one greppable place, not sprinkled across twelve checks where a new
/// check can quietly ship without anybody deciding.</para>
/// <para><b>@llmNote</b> The rule behind the table: a check that fires because the INSTALL is
/// incomplete points at <c>add-narrative-tracing</c> (it owns the dependency block, the interface
/// requirement for <c>NarrativeTraceProxy.Create&lt;T&gt;</c>, and the version-skew notes); a check
/// that fires on an already-wired project behaving wrongly points at <c>narrativetrace-doctor</c>
/// (its steps own proving redaction, reading a rendered trace, and the approval-trace flow). Two ids
/// carry NO skill, and that is a decision rather than an omission: no skill upgrades a target
/// framework, and no skill can install the skills.</para>
/// <para><b>@llmNote</b> Keyed on the id LITERALS rather than each check's own <c>Id</c> constant on
/// purpose: <c>NarrativeTrace.Tooling.Doctor.Checks</c> already depends on this namespace (through
/// <see cref="DoctorFinding"/>), so importing the checks back here to read twelve constants would
/// close a namespace cycle.</para>
/// </remarks>
public static class DoctorFindingSkills
{
    /// <summary>The skill a first install follows: dependencies, the proxy's interface requirement, wiring.</summary>
    internal const string AddNarrativeTracing = "add-narrative-tracing";

    /// <summary>The skill a wired-but-misbehaving project follows: diagnosis, read-only.</summary>
    internal const string NarrativeTraceDoctor = "narrativetrace-doctor";

    private static readonly Dictionary<string, string> ById = new(StringComparer.Ordinal)
    {
        ["toolchain.package-versions-aligned"] = AddNarrativeTracing,
        ["toolchain.companion-packages"] = AddNarrativeTracing,
        ["config.output-env"] = NarrativeTraceDoctor,
        ["config.registration-pair"] = AddNarrativeTracing,
        ["config.appsettings-keys"] = NarrativeTraceDoctor,
        ["trap.silent-sink"] = NarrativeTraceDoctor,
        ["trap.proxy-interface"] = AddNarrativeTracing,
        ["trap.redaction-proof"] = NarrativeTraceDoctor,
        ["trap.approval-traces"] = NarrativeTraceDoctor,
        ["trap.llms-before-you-start"] = AddNarrativeTracing,
    };

    /// <summary>
    /// Check ids that deliberately carry no skill. Listed rather than left to fall through, so
    /// <see cref="Knows"/> can tell "decided: none" from "nobody decided yet".
    /// </summary>
    private static readonly HashSet<string> NoSkill = new(StringComparer.Ordinal)
    {
        // No skill upgrades a target framework: the fix is the project file, or a different SDK.
        "toolchain.target-framework",
        // The finding IS that the skills are absent; naming one would point at a missing page.
        "config.skills-installed",
    };

    /// <summary>
    /// The catalogue skill that fixes this class of finding, or <see langword="null"/> when none does.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> id answers <see langword="null"/> rather than throwing: this runs
    /// inside <see cref="DoctorFinding"/>'s <c>Pass</c>/<c>Fail</c> factories, so a lookup failure here
    /// must never be what a caller sees instead of whatever a null or unrecognized id does elsewhere.
    /// </remarks>
    public static string? ForCheck(string? id) => id is not null && ById.TryGetValue(id, out var skill) ? skill : null;

    /// <summary>Whether the table has a decision — a skill or a deliberate none — for this check id.</summary>
    public static bool Knows(string? id) => id is not null && (ById.ContainsKey(id) || NoSkill.Contains(id));
}
