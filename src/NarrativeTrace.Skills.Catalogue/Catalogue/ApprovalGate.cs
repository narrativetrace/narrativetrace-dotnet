// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Skills.Catalogue;

/// <summary>
/// The approval gate every skill uses before it turns something it SHOWED into something durable or
/// public: show the whole artifact, ask once and stop the turn, and act only on what was shown.
/// </summary>
/// <remarks>
/// INTENT: the feedback skill files a public issue and the verify and debug skills promote a
/// committed approval baseline; each is a person's decision, made on an artifact they read, in a turn
/// of their own. Written once so every skill states the gate in the same words — a rule restated by
/// hand is a rule that drifts — with only the artifact and the act filled in.
/// </remarks>
internal static class ApprovalGate
{
    /// <summary>The verify line of the "show it" step: the artifact itself, whole, is in the reply.</summary>
    /// <param name="artifact">What is shown, as the sentence names it.</param>
    /// <param name="included">What else must be in the reply with it, as a clause; empty for nothing.</param>
    /// <param name="notInstead">What must not stand in for it beyond a summary, as a clause; empty for nothing.</param>
    internal static string VerifyShownWhole(string artifact, string included = "", string notInstead = "") =>
        "the whole text of " + artifact + " is in the reply" + included + ", not a summary of it" + notInstead;

    /// <summary>The verify line of the "ask once" step, followed by what must not happen before the yes.</summary>
    /// <param name="beforeTheYes">The act this skill must not start before the user's answer, as a sentence.</param>
    internal static string VerifyAskedThenStopped(string beforeTheYes) =>
        "the reply ends with the question and nothing after it — the answer is the user's next " +
        "message, never something assumed in this one. " + beforeTheYes;

    /// <summary>"Show the whole draft before asking anything", with this skill's reason.</summary>
    internal static ReasonedRule ShowTheWholeBeforeAsking(string artifact, string why) =>
        new("Show the whole " + artifact + " before asking anything", why);

    /// <summary>"Never file in the turn that asked" — the yes is the user's next message.</summary>
    internal static ReasonedRule NeverInTheTurnThatAsked(string act) =>
        new(
            "Never " + act + " in the turn that asked",
            "approval is the user's next message — a yes assumed in the same turn is not one");

    /// <summary>"Never edit the draft after showing it" — what was approved is what is acted on.</summary>
    /// <param name="artifact">What was shown, as the rule names it ("draft").</param>
    /// <param name="actedOn">The act in the past tense ("filed").</param>
    /// <param name="changed">What a change produces, and how it is made again ("report is drafted").</param>
    internal static ReasonedRule NeverEditAfterShowing(string artifact, string actedOn, string changed) =>
        new(
            "Never edit the " + artifact + " after showing it",
            "what was approved has to be what is " + actedOn + ", so a changed " + changed +
            " again and shown again");
}
