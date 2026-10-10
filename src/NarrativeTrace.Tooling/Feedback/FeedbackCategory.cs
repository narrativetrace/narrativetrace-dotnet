// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// Which part of NarrativeTrace a problem report is about — the one field that decides where the
/// report is triaged and what it must carry.
/// </summary>
/// <remarks>
/// INTENT: a closed set, not free text. The issue form renders it as a dropdown, the label
/// <c>category:&lt;id&gt;</c> comes from it, and <see cref="RequiresDoctorReport"/> is the one rule
/// that depends on it: a complaint about the doctor or about a skill is unarguable only with the
/// doctor's own JSON beside it, while a complaint about the published prompt or about the library may
/// be filed from a project whose build cannot run the doctor at all.
/// </remarks>
public sealed class FeedbackCategory
{
    /// <summary>The published install prompt, or any text an adopter was told to follow.</summary>
    public static readonly FeedbackCategory Prompt = new("prompt", requiresDoctorReport: false);

    /// <summary>A catalogue skill: a step that does not work, a verify that cannot be met, wrong wording.</summary>
    public static readonly FeedbackCategory Skill = new("skill", requiresDoctorReport: true);

    /// <summary>A doctor check: wrong finding, wrong fix, a fix that does not work.</summary>
    public static readonly FeedbackCategory Doctor = new("doctor", requiresDoctorReport: true);

    /// <summary>The library itself: capture, rendering, redaction, an integration.</summary>
    public static readonly FeedbackCategory Library = new("library", requiresDoctorReport: false);

    private static readonly IReadOnlyList<FeedbackCategory> AllCategories = [Prompt, Skill, Doctor, Library];

    private FeedbackCategory(string id, bool requiresDoctorReport)
    {
        Id = id;
        RequiresDoctorReport = requiresDoctorReport;
    }

    /// <summary>The lower-case id the issue form's dropdown and the <c>category:</c> label carry.</summary>
    public string Id { get; }

    /// <summary>Whether a report in this category is only meaningful with the doctor's JSON attached.</summary>
    public bool RequiresDoctorReport { get; }

    /// <summary>
    /// The category with this id.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// on an unknown id — a category is a closed set, and a silent fallback would file a report into
    /// the wrong triage queue.
    /// </exception>
    public static FeedbackCategory OfId(string id) =>
        AllCategories.FirstOrDefault(category => category.Id.Equals(id, StringComparison.Ordinal))
        ?? throw new ArgumentException(
            $"unknown category \"{id}\" — one of prompt, skill, doctor, library", nameof(id));
}
