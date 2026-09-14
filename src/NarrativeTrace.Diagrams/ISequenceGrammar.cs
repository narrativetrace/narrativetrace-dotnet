// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Diagrams;

/// <summary>
/// The per-format literals <see cref="SequenceWalk"/> needs to render one trace tree as a
/// sequence diagram — everything Mermaid and PlantUML disagree about, in one place.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MermaidSequenceRenderer"/> and <see cref="PlantUmlSequenceRenderer"/> share one
/// traversal (<see cref="SequenceWalk"/>); the two formats differ only in header/footer text,
/// participant declaration syntax, arrow syntax, and return/throw/in-flight notation — including
/// PlantUML's optional lifeline bars, which stay entirely inside its grammar (see
/// <see cref="PlantUmlSequenceGrammar"/>) rather than becoming a walk concern. Every hook here is a
/// pure function of its inputs.
/// </para>
/// <para>
/// Every hook that carries trace-derived text takes a <see cref="DiagramLabel"/>, never a
/// <see cref="string"/> — see <see cref="DiagramLabel"/>'s own class doc. A grammar implementation
/// cannot receive a raw trace string; only <see cref="SequenceWalk"/> ever turns a raw class name
/// into a label, through the label-mapping function each renderer supplies, before any hook here is
/// called. The test project's shape test pins this via reflection: no hook parameter here is typed
/// <see cref="string"/>.
/// </para>
/// </remarks>
internal interface ISequenceGrammar
{
    /// <summary>The opening line(s) of the diagram, before any participant declaration.</summary>
    string Header { get; }

    /// <summary>
    /// One participant declaration line for <paramref name="alias"/>/<paramref name="displayName"/>
    /// pair — the order of the two in the emitted line is a genuine format difference (PlantUML
    /// names the display text first, then the alias after <c>as</c>; Mermaid is the reverse), so
    /// each grammar composes its own line rather than receiving one pre-composed.
    /// </summary>
    string Participant(DiagramLabel alias, DiagramLabel displayName);

    /// <summary>One call arrow, caller to target, naming the call signature.</summary>
    string CallArrow(DiagramLabel caller, DiagramLabel target, DiagramLabel signature);

    /// <summary>One return arrow, target back to caller, carrying the return message.</summary>
    string ReturnArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel message);

    /// <summary>One throw arrow, target back to caller, naming the exception type.</summary>
    string ThrowArrow(DiagramLabel target, DiagramLabel caller, DiagramLabel exceptionType);

    /// <summary>The note for a node whose outcome never arrived (an in-flight call).</summary>
    string Incomplete(DiagramLabel target);

    /// <summary>The closing line(s) of the diagram, after every root has been walked.</summary>
    string Footer { get; }
}
