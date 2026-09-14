// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// Renders a captured <see cref="TraceTree"/> as
/// <see href="https://plantuml.com/sequence-diagram">PlantUML</see>
/// sequence-diagram source text wrapped in <c>@startuml</c>/<c>@enduml</c>.
/// </summary>
/// <remarks>
/// Each distinct class in the trace becomes a <c>participant</c> whose
/// display name is quoted when necessary (<see cref="DiagramText.QuoteIfNeeded"/>)
/// and whose short alias comes from <see cref="AliasGenerator"/>. Every
/// call is emitted as a synchronous message arrow (<c>-&gt;</c>) carrying the
/// method name and its parameters; a normal return is a dashed reply
/// (<c>--&gt;</c>), a thrown call uses a red arrow (<c>-[#red]-&gt;</c>) labelled
/// with the exception type name, and a still-running (<see cref="Incomplete"/>)
/// call is marked with <c>hnote over &lt;alias&gt; : in-flight</c>. Returned
/// values that produced no rendered text fall back to a check mark
/// (<c>✔</c>). Rendered values are sanitized through
/// <see cref="DiagramText.Message"/> (via <see cref="DiagramLabel"/>) so control characters cannot
/// break the line-oriented PlantUML grammar — the traversal and arrow shape themselves live in
/// <see cref="SequenceWalk"/>, shared with <see cref="MermaidSequenceRenderer"/>; this class
/// supplies only the PlantUML <see cref="ISequenceGrammar"/>, including its optional lifeline bars
/// (<see cref="PlantUmlSequenceGrammar"/>). The single-argument <see cref="Render(TraceTree)"/>
/// already has the shape of a <c>Func&lt;TraceTree, string&gt;</c> — this
/// runtime's structurally-typed equivalent of a single-method renderer
/// interface — so it needs no wrapper to be used as one; get an instance
/// from <see cref="SequenceDiagramRenderers.PlantUml"/> when a caller wants
/// the format without naming this class.
/// </remarks>
/// <example>
/// For a single <c>Cart.Checkout()</c> call that returns, the output is:
/// <code>
/// @startuml
/// participant Cart as C
/// C -&gt; C : Checkout()
/// C --&gt; C : ok
/// @enduml
/// </code>
/// </example>
public static class PlantUmlSequenceRenderer
{
    /// <summary>
    /// Renders <paramref name="tree"/> to a PlantUML sequence diagram without
    /// explicit lifeline activation bars.
    /// </summary>
    /// <param name="tree">The captured trace to render.</param>
    /// <returns>
    /// PlantUML source wrapped in <c>@startuml</c>/<c>@enduml</c>.
    /// </returns>
    public static string Render(TraceTree tree) =>
        Render(tree, includeLifelines: false);

    /// <summary>
    /// Renders <paramref name="tree"/> to a PlantUML sequence diagram,
    /// optionally emitting <c>activate</c>/<c>deactivate</c> lifeline bars
    /// around each call.
    /// </summary>
    /// <param name="tree">The captured trace to render.</param>
    /// <param name="includeLifelines">
    /// When <see langword="true"/>, wraps every call in <c>activate</c> and
    /// <c>deactivate</c> statements for the target participant so the diagram
    /// shows execution lifelines.
    /// </param>
    /// <returns>
    /// PlantUML source beginning with <c>@startuml</c>, one <c>participant</c>
    /// line per class, the call/return arrows for every node in
    /// <see cref="TraceTree.Roots"/>, and a trailing <c>@enduml</c>.
    /// </returns>
    public static string Render(
        TraceTree tree, bool includeLifelines) =>
        SequenceWalk.Render(tree, new PlantUmlSequenceGrammar(includeLifelines));
}
