// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Diagrams;

/// <summary>
/// PlantUML grammar: <c>-&gt;</c> call arrows, <c>--&gt;</c> returns, <c>-[#red]-&gt;</c> throws,
/// <c>hnote over</c> for an in-flight outcome, with optional <c>activate</c>/<c>deactivate</c>
/// lifeline bars around every call.
/// </summary>
/// <remarks>
/// Lifelines are entirely this grammar's concern, not the shared walk's: when
/// <see cref="_includeLifelines"/> is set, <see cref="CallArrow"/> appends the <c>activate</c>
/// statement for the target right after the arrow, and every outcome hook appends the matching
/// <c>deactivate</c> — reproducing the nesting the old renderer wrote by hand (activate on the way
/// in, deactivate on the way out, in call order) without <see cref="SequenceWalk"/> knowing
/// lifelines exist.
/// </remarks>
internal sealed class PlantUmlSequenceGrammar(bool includeLifelines) : ISequenceGrammar
{
    private readonly bool _includeLifelines = includeLifelines;

    /// <inheritdoc/>
    public string Header => "@startuml" + Environment.NewLine;

    /// <inheritdoc/>
    /// <remarks>
    /// PlantUML's own syntax is <c>&lt;participant_type&gt; &lt;label&gt; as &lt;alias&gt;</c> (see
    /// https://plantuml.com/sequence-diagram, "Declaring participant") — the display name comes
    /// first, and the identifier after <c>as</c> is the one every arrow must address. Declaring it
    /// the other way around leaves the display name as PlantUML's real reference name (never used
    /// by any arrow) and turns the short alias arrows do use into an unrecognized identifier, which
    /// PlantUML silently renders as a second, disconnected participant.
    /// </remarks>
    public string Participant(DiagramLabel alias, DiagramLabel displayName) =>
        "participant " + displayName.Text + " as " + alias.Text + Environment.NewLine;

    /// <inheritdoc/>
    public string CallArrow(DiagramLabel caller, DiagramLabel target, DiagramLabel signature)
    {
        var line = caller.Text + " -> " + target.Text + " : " + signature.Text + Environment.NewLine;
        return _includeLifelines ? line + Activation("activate", target) : line;
    }

    /// <inheritdoc/>
    public string ReturnArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel message) =>
        Outcome(target, caller, " --> ", message);

    /// <inheritdoc/>
    public string ThrowArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel exceptionType) =>
        Outcome(target, caller, " -[#red]-> ", exceptionType);

    /// <inheritdoc/>
    public string Incomplete(DiagramLabel target)
    {
        var line = "hnote over " + target.Text + " : in-flight" + Environment.NewLine;
        return _includeLifelines ? line + Activation("deactivate", target) : line;
    }

    /// <inheritdoc/>
    public string Footer => "@enduml" + Environment.NewLine;

    private string Outcome(DiagramLabel target, DiagramLabel caller, string arrow, DiagramLabel message)
    {
        var line = target.Text + arrow + caller.Text + " : " + message.Text + Environment.NewLine;
        return _includeLifelines ? line + Activation("deactivate", target) : line;
    }

    private static string Activation(string verb, DiagramLabel target) =>
        verb + " " + target.Text + Environment.NewLine;
}
