// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The only two attachment kinds a report may carry: the doctor's own JSON report, and at most one
/// structural trace.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: a closed set, because every other artifact a project has carries runtime values. A
/// rendered narrative, a log file and a source file are all refused by construction here rather than
/// by a rule — there is no field to put them in.
/// </para>
/// <para>
/// <b>@llmNote</b> <see cref="DoctorReport"/> and <see cref="DoctorUnavailable"/> are exclusive and
/// exhaustive (Q3 as ruled): exactly one is non-empty. "No doctor report and no reason" would read,
/// to triage, as a reporter who did not bother rather than as a project whose build cannot run the
/// task — and those two get different answers. The check sits in the constructor, so every
/// construction path (including a record <c>with</c>) is validated.
/// </para>
/// </remarks>
/// <param name="DoctorReport">The doctor's JSON report verbatim, or <c>""</c>.</param>
/// <param name="DoctorUnavailable">Why there is no doctor report, or <c>""</c> when there is one.</param>
/// <param name="StructuralTrace">One <c>.nt</c> file's content, or <c>""</c>.</param>
public sealed record Attachments(string DoctorReport, string DoctorUnavailable, string StructuralTrace)
{
    /// <summary>The doctor's JSON report verbatim, or <c>""</c>.</summary>
    public string DoctorReport { get; } = RequireExclusive(DoctorReport, DoctorUnavailable);

    /// <summary>Why there is no doctor report, or <c>""</c> when there is one.</summary>
    public string DoctorUnavailable { get; } = RequireNonNull(DoctorUnavailable);

    /// <summary>One <c>.nt</c> file's content, or <c>""</c>.</summary>
    public string StructuralTrace { get; } = RequireNonNull(StructuralTrace);

    /// <summary>A report with the doctor's JSON, and optionally one structural trace.</summary>
    public static Attachments Of(string doctorReport, string structuralTrace) =>
        new(doctorReport, "", structuralTrace);

    /// <summary>A report whose project could not run the doctor, with the reason it could not.</summary>
    public static Attachments WithoutDoctorReport(string reason, string structuralTrace) =>
        new("", reason, structuralTrace);

    /// <summary>Whether the doctor's own JSON is attached.</summary>
    public bool HasDoctorReport() => !string.IsNullOrWhiteSpace(DoctorReport);

    /// <summary>Whether a structural trace is attached.</summary>
    public bool HasStructuralTrace() => !string.IsNullOrWhiteSpace(StructuralTrace);

    private static string RequireExclusive(string doctorReport, string doctorUnavailable)
    {
        RequireNonNull(doctorReport);
        RequireNonNull(doctorUnavailable);
        if (string.IsNullOrWhiteSpace(doctorReport) == string.IsNullOrWhiteSpace(doctorUnavailable))
        {
            throw new ArgumentException(
                "a report carries the doctor's JSON or says why it has none, never both and never neither");
        }

        return doctorReport;
    }

    private static string RequireNonNull(string value) =>
        value ?? throw new ArgumentException("an absent attachment is \"\", never null");
}
