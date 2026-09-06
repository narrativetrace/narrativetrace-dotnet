// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
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
/// <see cref="DiagramText.Message"/> so control characters cannot break the
/// line-oriented Mermaid grammar.
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
    public static string Render(TraceTree tree)
    {
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so every recursive walk below can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        var roots = TreeWalk.Bound(tree.Roots);
        var aliases = new Dictionary<string, string>();
        CollectParticipants(roots, aliases);

        var sb = new StringBuilder();
        sb.AppendLine("sequenceDiagram");
        WriteParticipants(sb, aliases);
        foreach (var root in roots)
        {
            WriteNodeMessages(
                sb, root, aliases[root.Signature.ClassName], aliases);
        }

        return sb.ToString();
    }

    private static void CollectParticipants(
        IReadOnlyList<TraceNode> nodes,
        Dictionary<string, string> aliases)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            AliasGenerator.Generate(
                nodes[i].Signature.ClassName, aliases);
            CollectParticipants(
                nodes[i].Children, aliases);
        }
    }

    private static void WriteParticipants(
        StringBuilder sb,
        Dictionary<string, string> aliases)
    {
        foreach (var kvp in aliases)
        {
            sb.Append("    participant ");
            sb.Append(kvp.Value);
            sb.Append(" as ");
            sb.AppendLine(DiagramText.QuoteIfNeeded(
                DiagramText.Identifier(kvp.Key)));
        }
    }

    private static void WriteMessages(
        StringBuilder sb,
        IReadOnlyList<TraceNode> nodes,
        string callerAlias,
        Dictionary<string, string> aliases)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            WriteNodeMessages(
                sb, nodes[i], callerAlias, aliases);
        }
    }

    private static void WriteNodeMessages(
        StringBuilder sb, TraceNode node,
        string callerAlias,
        Dictionary<string, string> aliases)
    {
        var targetAlias = aliases[node.Signature.ClassName];
        WriteCallArrow(sb, callerAlias, targetAlias, node);
        WriteMessages(sb, node.Children, targetAlias, aliases);
        WriteReturn(sb, node, targetAlias, callerAlias);
    }

    private static void WriteReturn(
        StringBuilder sb, TraceNode node,
        string targetAlias, string callerAlias)
    {
        if (node.Outcome is Incomplete)
        {
            sb.Append("    Note over ");
            sb.Append(targetAlias);
            sb.AppendLine(": in-flight");
            return;
        }

        WriteReturnArrow(sb, targetAlias, callerAlias, node);
    }

    private static void WriteCallArrow(
        StringBuilder sb, string from, string to,
        TraceNode node)
    {
        sb.Append("    ");
        sb.Append(from);
        sb.Append("->>");
        sb.Append(to);
        sb.Append(": ");
        sb.Append(DiagramText.Identifier(node.Signature.MethodName));
        AppendParameters(sb, node.Signature.Parameters);
        sb.AppendLine();
    }

    private static void WriteReturnArrow(
        StringBuilder sb, string from, string to,
        TraceNode node)
    {
        sb.Append("    ");
        sb.Append(from);
        sb.Append(node.Outcome is Threw ? "-x" : "-->>");
        sb.Append(to);
        sb.Append(": ");
        AppendOutcome(sb, node);
        sb.AppendLine();
    }

    private static void AppendParameters(
        StringBuilder sb,
        IReadOnlyList<ParameterCapture> parameters)
    {
        sb.Append('(');
        for (var i = 0; i < parameters.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(DiagramText.Identifier(parameters[i].Name));
            sb.Append(": ");
            sb.Append(DiagramText.Message(parameters[i].RenderedValue));
        }

        sb.Append(')');
    }

    private static void AppendOutcome(
        StringBuilder sb, TraceNode node)
    {
        switch (node.Outcome)
        {
            case Returned { RenderedValue: { } value }:
                sb.Append(DiagramText.Message(value));
                break;
            case Threw { Error: { } ex }:
                sb.Append(DiagramText.Identifier(ex.GetType().Name));
                break;
            default:
                sb.Append('\u2714');
                break;
        }
    }
}
