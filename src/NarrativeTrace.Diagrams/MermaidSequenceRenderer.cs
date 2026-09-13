// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// Renders a captured <see cref="TraceTree"/> as
/// <see href="https://mermaid.js.org/syntax/sequenceDiagram.html">Mermaid</see>
/// <c>sequenceDiagram</c> source text.
/// </summary>
/// <remarks>
/// Each distinct class in the trace becomes a <c>participant</c> whose
/// display name is quoted when necessary (<see cref="DiagramText.QuoteIfNeeded"/>)
/// and whose short alias comes from <see cref="AliasGenerator"/>. Every
/// call is emitted as a solid message arrow (<c>-&gt;&gt;</c>) carrying the
/// method name and its parameters; a normal return is a dashed reply
/// (<c>--&gt;&gt;</c>), a thrown call uses the cross arrow (<c>-x</c>) labelled
/// with the exception type name, and a still-running (<see cref="Incomplete"/>)
/// call is marked with <c>Note over &lt;alias&gt;: in-flight</c>. Returned
/// values that produced no rendered text fall back to a check mark
/// (<c>✔</c>). Rendered values are sanitized through
/// <see cref="DiagramText.Message"/> (via <see cref="DiagramLabel"/>) so control characters cannot
/// break the line-oriented Mermaid grammar — the traversal and arrow shape themselves live in
/// <see cref="SequenceWalk"/>, shared with <see cref="PlantUmlSequenceRenderer"/>; this class
/// supplies only the Mermaid <see cref="ISequenceGrammar"/>. <see cref="Render"/> already has the
/// shape of a <c>Func&lt;TraceTree, string&gt;</c> — this runtime's
/// structurally-typed equivalent of a single-method renderer interface — so
/// it needs no wrapper to be used as one; get an instance from
/// <see cref="SequenceDiagramRenderers.Mermaid"/> when a caller wants the
/// format without naming this class.
/// </remarks>
/// <example>
/// For a single <c>Cart.Checkout()</c> call that returns, the output is:
/// <code>
/// sequenceDiagram
///     participant C as Cart
///     C-&gt;&gt;C: Checkout()
///     C--&gt;&gt;C: ok
/// </code>
/// </example>
public static class MermaidSequenceRenderer
{
    /// <summary>
    /// Renders <paramref name="tree"/> to a Mermaid <c>sequenceDiagram</c>
    /// document.
    /// </summary>
    /// <param name="tree">The captured trace to render.</param>
    /// <returns>
    /// Mermaid source beginning with the <c>sequenceDiagram</c> header,
    /// followed by one <c>participant</c> line per class and the call/return
    /// message arrows for every node in <see cref="TraceTree.Roots"/>.
    /// </returns>
    public static string Render(TraceTree tree) =>
        SequenceWalk.Render(tree, MermaidSequenceGrammar.Instance);
}
