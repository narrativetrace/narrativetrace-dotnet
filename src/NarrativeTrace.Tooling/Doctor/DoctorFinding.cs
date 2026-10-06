// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Doctor;

/// <summary>
/// One check's outcome: a stable <paramref name="Id"/> that is never
/// renamed, a human-readable <paramref name="Message"/> true on both pass
/// and fail, a <paramref name="Fix"/> that is non-empty exactly when
/// <paramref name="Passed"/> is <see langword="false"/>, and a
/// <paramref name="DocUrl"/> anchor into the public documentation.
/// </summary>
/// <param name="Id">
/// The check's stable, dotted identifier (e.g. <c>trap.silent-sink</c>).
/// Never renamed across releases — tooling and skills reference it by id.
/// </param>
/// <param name="Passed">Whether the check found nothing wrong.</param>
/// <param name="Message">Explains the outcome; true whether pass or fail.</param>
/// <param name="Fix">
/// The remediation. Empty on a pass; a concrete, actionable instruction on
/// a fail — never a restatement of the message.
/// </param>
/// <param name="DocUrl">A URL into the public documentation for this check's topic.</param>
/// <param name="Skill">
/// The catalogue skill that fixes this class of finding, or <see langword="null"/> when none does.
/// Derived from <paramref name="Id"/> by <see cref="DoctorFindingSkills"/> when created through
/// <see cref="Pass"/> or <see cref="Fail"/> — this canonical constructor still takes it explicitly,
/// which is what lets a test build a finding whose id the table has never heard of, with an
/// explicit skill of its own choosing.
/// </param>
public sealed record DoctorFinding(
    string Id, bool Passed, string Message, string Fix, string DocUrl, string? Skill)
{
    /// <summary>Creates a passing finding, its skill resolved from <see cref="DoctorFindingSkills"/>.</summary>
    public static DoctorFinding Pass(string id, string message, string docUrl)
    {
        return new DoctorFinding(id, true, message, string.Empty, docUrl, DoctorFindingSkills.ForCheck(id));
    }

    /// <summary>Creates a failing finding with its remediation, its skill resolved from <see cref="DoctorFindingSkills"/>.</summary>
    public static DoctorFinding Fail(string id, string message, string fix, string docUrl)
    {
        return new DoctorFinding(id, false, message, fix, docUrl, DoctorFindingSkills.ForCheck(id));
    }
}
