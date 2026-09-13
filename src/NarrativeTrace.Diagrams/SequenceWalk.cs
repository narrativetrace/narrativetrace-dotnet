// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using NarrativeTrace.Core;

namespace NarrativeTrace.Diagrams;

/// <summary>
/// The one traversal both sequence-diagram renderers share: emitting exactly one call arrow on the
/// way into a node and exactly one outcome (return, throw, or in-flight note) on the way out.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="MermaidSequenceRenderer"/> and <see cref="PlantUmlSequenceRenderer"/> used to each own
/// a private copy of this enter/exit pair, differing only in the grammar (arrow syntax, note
/// wording) and the label mapping they closed over. Extracted by composition, not inheritance: this
/// class owns the walk — written once — an <see cref="ISequenceGrammar"/> owns everything the two
/// formats disagree about, and the caller-supplied <c>participantLabel</c> function owns how a raw
/// class name becomes the <see cref="DiagramLabel"/> an arrow names (an alias lookup for both
/// formats here) — deliberately a parameter, never a grammar hook, per <see cref="DiagramLabel"/>'s
/// own invariant that a raw trace string never reaches a grammar.
/// </para>
/// <para>
/// A node past <see cref="TreeWalk.MaxDepth"/> or already on the current path is not this class's
/// concern: <see cref="TreeWalk.Bound"/> replaces it with a synthetic marker leaf before either
/// renderer calls here, so by the time this walk runs, every node — real or marker — takes the same
/// ordinary path and gets its own call arrow and outcome, rendered exactly like a leaf.
/// </para>
/// </remarks>
internal static class SequenceWalk
{
    /// <summary>
    /// Renders <paramref name="tree"/> end to end with <paramref name="grammar"/>: bound the forest,
    /// collect and alias its participants, then emit the header, participant lanes, every call
    /// arrow and outcome, and the footer. The one method both <see cref="MermaidSequenceRenderer"/>
    /// and <see cref="PlantUmlSequenceRenderer"/> call — everything a renderer's own
    /// <c>Render</c> used to do by hand, before this class existed, and the wiring that was still
    /// duplicated between them even after the walk itself moved here.
    /// </summary>
    /// <param name="tree">The captured trace to render.</param>
    /// <param name="grammar">The format-specific hooks to render with.</param>
    public static string Render(TraceTree tree, ISequenceGrammar grammar)
    {
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound once, here, so the
        // walk below can never overflow the stack or loop forever on a hand-built or replayed
        // cycle. Cheap on ordinary input: TreeWalk.Bound returns Roots unchanged once it confirms
        // there is nothing to bound.
        var roots = TreeWalk.Bound(tree.Roots);
        var participants = SequenceParticipants.Collect(roots);
        var aliases = new Dictionary<string, string>();
        foreach (var name in participants)
        {
            AliasGenerator.Generate(name, aliases);
        }

        var sb = new StringBuilder();
        sb.Append(grammar.Header);
        RenderParticipants(participants, aliases, grammar, sb);
        RenderCalls(roots, grammar, className => DiagramLabel.Alias(aliases[className]), sb);
        sb.Append(grammar.Footer);

        return sb.ToString();
    }

    /// <summary>
    /// Appends one participant declaration line per name in <paramref name="participants"/>, in
    /// order, using each name's already-assigned alias.
    /// </summary>
    /// <param name="participants">Class names in first-appearance order, from <see cref="SequenceParticipants.Collect"/>.</param>
    /// <param name="aliases">The class-name-to-alias map built by <see cref="AliasGenerator.Generate"/>.</param>
    /// <param name="grammar">The format-specific hooks to render with.</param>
    /// <param name="sb">The buffer to append to.</param>
    private static void RenderParticipants(
        List<string> participants,
        Dictionary<string, string> aliases,
        ISequenceGrammar grammar,
        StringBuilder sb)
    {
        for (var i = 0; i < participants.Count; i++)
        {
            var name = participants[i];
            var aliasLabel = DiagramLabel.Alias(aliases[name]);
            var displayLabel = DiagramLabel.QuotedIdentifier(name);
            sb.Append(grammar.Participant(aliasLabel.AliasedAs(displayLabel)));
        }
    }

    /// <summary>Walks every root, appending every call arrow and outcome the grammar produces.</summary>
    /// <param name="roots">The (already-bounded) forest to walk.</param>
    /// <param name="grammar">The format-specific hooks to render with.</param>
    /// <param name="participantLabel">How a raw class name becomes the label an arrow names.</param>
    /// <param name="sb">The buffer to append to.</param>
    public static void RenderCalls(
        IReadOnlyList<TraceNode> roots,
        ISequenceGrammar grammar,
        Func<string, DiagramLabel> participantLabel,
        StringBuilder sb)
    {
        for (var i = 0; i < roots.Count; i++)
        {
            var target = participantLabel(roots[i].Signature.ClassName);
            RenderNode(roots[i], target, grammar, participantLabel, sb);
        }
    }

    private static void RenderNode(
        TraceNode node,
        DiagramLabel callerLabel,
        ISequenceGrammar grammar,
        Func<string, DiagramLabel> participantLabel,
        StringBuilder sb)
    {
        var targetLabel = participantLabel(node.Signature.ClassName);
        sb.Append(grammar.CallArrow(callerLabel, targetLabel, Signature(node)));
        RenderChildren(node.Children, targetLabel, grammar, participantLabel, sb);
        AppendOutcome(node, targetLabel, callerLabel, grammar, sb);
    }

    private static void RenderChildren(
        IReadOnlyList<TraceNode> children,
        DiagramLabel callerLabel,
        ISequenceGrammar grammar,
        Func<string, DiagramLabel> participantLabel,
        StringBuilder sb)
    {
        for (var i = 0; i < children.Count; i++)
        {
            RenderNode(children[i], callerLabel, grammar, participantLabel, sb);
        }
    }

    private static DiagramLabel Signature(TraceNode node)
    {
        var method = DiagramLabel.Identifier(node.Signature.MethodName);
        return method.WithParameters(BuildParameters(node.Signature.Parameters));
    }

    private static (DiagramLabel Name, DiagramLabel Value)[] BuildParameters(
        IReadOnlyList<ParameterCapture> parameters)
    {
        var result = new (DiagramLabel, DiagramLabel)[parameters.Count];
        for (var i = 0; i < parameters.Count; i++)
        {
            result[i] = (
                DiagramLabel.Identifier(parameters[i].Name),
                DiagramLabel.Message(parameters[i].RenderedValue));
        }

        return result;
    }

    private static void AppendOutcome(
        TraceNode node,
        DiagramLabel targetLabel,
        DiagramLabel callerLabel,
        ISequenceGrammar grammar,
        StringBuilder sb)
    {
        switch (node.Outcome)
        {
            case Incomplete:
                sb.Append(grammar.Incomplete(targetLabel));
                break;
            case Threw { Error: { } ex }:
                sb.Append(grammar.ThrowArrow(
                    targetLabel, callerLabel, DiagramLabel.Identifier(ex.GetType().Name)));
                break;
            default:
                sb.Append(grammar.ReturnArrow(targetLabel, callerLabel, OutcomeMessage(node)));
                break;
        }
    }

    private static DiagramLabel OutcomeMessage(TraceNode node) =>
        node.Outcome is Returned { RenderedValue: { } value }
            ? DiagramLabel.Message(value)
            : DiagramLabel.CheckMark;
}
