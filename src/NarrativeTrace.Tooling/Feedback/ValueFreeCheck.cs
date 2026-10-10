// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Feedback;

/// <summary>
/// The hard gate: every <see cref="ValueFreeRule"/> over every field of a report, before anything is
/// built that a user could file.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: with no private inbox, a filed report is public from the first second — so the check runs
/// BEFORE the draft, the URL and the body file exist, and the verb refuses to produce any of them
/// while a violation stands. A gate that ran afterwards would be a warning, and a warning on this
/// path is a leak with a note attached.
/// </para>
/// <para>
/// <b>@llmNote</b> Every rule is reported, not the first: a report that leaks two different shapes
/// should be fixed once, not twice. Order is field order then <see cref="ValueFreeRule"/> declaration
/// order, so the same report always refuses in the same words — which is what lets a test assert on
/// them.
/// </para>
/// <para>
/// <b>@llmNote</b> Which rules apply is a property of the FIELD, not of the report: see
/// <see cref="DoctorReportField"/> for the one exemption and why it exists. <see cref="RulesRefusing"/>
/// is the text-scoped reading with no field to exempt, and it is what the shared
/// <c>hostile-corpus/feedback.json</c> replay uses — so a corpus row's verdict never depends on which
/// field a value happened to arrive in.
/// </para>
/// </remarks>
public static class ValueFreeCheck
{
    /// <summary>
    /// The field whose text is the doctor's OWN generated JSON, and the one field
    /// <see cref="ValueFreeRule.Marker"/> cannot read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The doctor's check vocabulary NAMES the redaction marker: a redaction-proof check is the check
    /// "no test asserts the literal <c>"[REDACTED]"</c>", and both its message and its fix quote that
    /// literal — whether the check passes or fails. So one rule, read against this one field, refused
    /// every report from every project that has NarrativeTrace installed at all, and the verb could
    /// draft nothing. Found by running the verb against a real project rather than against a
    /// hand-written stand-in for one.
    /// </para>
    /// <para>
    /// <b>@llmNote</b> Named by a string because that is what a field map is keyed by, so this constant
    /// and <c>FeedbackReport.Fields()</c> have to agree: a renamed field silently re-arms the rule.
    /// <c>ValueFreeCheckTests</c> asserts the report carries exactly this key.
    /// </para>
    /// </remarks>
    public const string DoctorReportField = "doctor report";

    /// <summary>
    /// Which rules read <paramref name="fieldName"/>.
    /// </summary>
    /// <remarks>
    /// Every rule reads every field except this one pair, and the exemption is deliberately as narrow
    /// as it can be: the doctor's report is still read by the other nine, so an email, a home path, a
    /// credential shape or a rendered call line inside a doctor message is refused exactly as it would
    /// be anywhere else. What is exempt is one rule whose whole premise — "the marker only appears
    /// where a value was blanked" — is false for OUR OWN generated text about that marker.
    /// </remarks>
    private static IReadOnlyList<ValueFreeRule> RulesFor(string fieldName) =>
        DoctorReportField.Equals(fieldName, StringComparison.Ordinal)
            ? ValueFreeRule.All().Where(rule => rule != ValueFreeRule.Marker).ToList()
            : ValueFreeRule.All();

    /// <summary>Every rule that refuses this one piece of text, in rule order; empty means it may be filed.</summary>
    public static IReadOnlyList<ValueFreeRule> RulesRefusing(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return ValueFreeRule.All().Where(rule => rule.Rejects(text)).ToList();
    }

    /// <summary>
    /// Every violation across a report's named fields.
    /// </summary>
    /// <param name="fields">Field name to its text, in the order a refusal should list them.</param>
    /// <exception cref="ArgumentException">
    /// when the map or any value is null — the drafter represents an absent field as <c>""</c>, so a
    /// null here is a caller's bug and must not be read as empty.
    /// </exception>
    public static IReadOnlyList<ValueFreeViolation> Violations(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var violations = new List<ValueFreeViolation>();
        foreach (var (field, text) in fields)
        {
            if (text is null)
            {
                throw new ArgumentException($"field \"{field}\" is null — an absent field is \"\"", nameof(fields));
            }

            violations.AddRange(
                RulesFor(field).Where(rule => rule.Rejects(text)).Select(rule => new ValueFreeViolation(field, rule)));
        }

        return violations;
    }
}
