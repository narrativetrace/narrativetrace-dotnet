// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Diagrams;

/// <summary>
/// Mermaid <c>sequenceDiagram</c> grammar: <c>-&gt;&gt;</c> call arrows, <c>--&gt;&gt;</c> returns,
/// <c>-x</c> throws, <c>Note over</c> for an in-flight outcome.
/// </summary>
/// <remarks>Stateless, so one shared <see cref="Instance"/> serves every render.</remarks>
internal sealed class MermaidSequenceGrammar : ISequenceGrammar
{
    /// <summary>The one instance — this grammar holds no state.</summary>
    public static readonly MermaidSequenceGrammar Instance = new();

    private MermaidSequenceGrammar()
    {
    }

    /// <inheritdoc/>
    public string Header => "sequenceDiagram" + Environment.NewLine;

    /// <inheritdoc/>
    public string Participant(DiagramLabel alias, DiagramLabel displayName) =>
        "    participant " + alias.Text + " as " + displayName.Text + Environment.NewLine;

    /// <inheritdoc/>
    public string CallArrow(DiagramLabel caller, DiagramLabel target, DiagramLabel signature) =>
        "    " + caller.Text + "->>" + target.Text + ": " + signature.Text + Environment.NewLine;

    /// <inheritdoc/>
    public string ReturnArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel message) =>
        "    " + target.Text + "-->>" + caller.Text + ": " + message.Text + Environment.NewLine;

    /// <inheritdoc/>
    public string ThrowArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel exceptionType) =>
        "    " + target.Text + "-x" + caller.Text + ": " + exceptionType.Text + Environment.NewLine;

    /// <inheritdoc/>
    public string Incomplete(DiagramLabel target) =>
        "    Note over " + target.Text + ": in-flight" + Environment.NewLine;

    /// <inheritdoc/>
    public string Footer => "";
}
