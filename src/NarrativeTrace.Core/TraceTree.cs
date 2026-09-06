// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// A finished trace: the forest of root calls captured in one
/// <see cref="INarrativeContext.CaptureTrace"/>.
/// </summary>
/// <remarks>
/// A <b>forest, not a single tree</b> despite the name — a trace has several
/// roots whenever work was grafted in from a fire-and-forget branch whose parent
/// span had already closed, or when instrumentation entered more than one
/// top-level call. Never assume <c>Roots[0]</c> is the whole story.
/// Immutable and already level-filtered, so it is safe to hold, share across
/// threads, and render more than once.
/// </remarks>
/// <param name="Roots">The top-level calls, in capture order. Never <see langword="null"/>; empty when nothing was captured.</param>
/// <param name="TraceId">
/// The trace this forest belongs to. Pass the capturing context's id; leave it
/// defaulted (<see cref="Core.TraceId.Empty"/>) when there is none to pass and
/// the tree will resolve its own — see the <see cref="TraceId"/> property.
/// </param>
public sealed record TraceTree(IReadOnlyList<TraceNode> Roots, TraceId TraceId = default)
{
    private readonly IReadOnlyList<TraceNode> _roots = Roots;
    private readonly TraceId _traceId = TraceIdentity.AtConstruction(TraceId, Roots);

    /// <summary>The top-level calls, in capture order.</summary>
    /// <remarks>
    /// Setting them re-resolves <see cref="TraceId"/> against the new forest —
    /// adopting the id already held, so replacing a tree's nodes never renames
    /// its trace, while a copy that gains its first nodes stops being
    /// unidentified.
    /// </remarks>
    public IReadOnlyList<TraceNode> Roots
    {
        get => _roots;
        init
        {
            _roots = value;
            _traceId = TraceIdentity.AtConstruction(_traceId, value);
        }
    }

    /// <summary>
    /// The one trace id every exporter of this tree reports, or
    /// <see cref="Core.TraceId.Empty"/> when the tree is empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Resolved once, here, at construction — <b>adopt → inherit → generate</b>
    /// (<see cref="TraceIdentity"/>): the id passed in wins; otherwise the first
    /// <see cref="SpanContext"/> found anywhere in the forest supplies it;
    /// otherwise a tree that has nodes but no context at all — a hand-built one,
    /// or a capture that ran before any span opened — mints a real, unique,
    /// W3C-shaped id. Identity belongs to the tree rather than to an exporter
    /// precisely so two exporters of one tree cannot name two different traces.
    /// </para>
    /// <para>
    /// An <see cref="IsEmpty"/> tree keeps no id: nothing ran, so there is
    /// nothing to identify, and asking an idle context for its trace must not
    /// give it an identity. A chapter exported from such a tree still validates
    /// — <see cref="TraceIdentity.Of"/> mints one at resolution time.
    /// </para>
    /// <para>
    /// Resolution runs in the <c>init</c> accessor, not only in a field
    /// initializer, because a record copy (<c>with</c>) does not re-run the
    /// constructor: <c>emptyTree with { Roots = … }</c> would otherwise carry
    /// nodes and no id, and each exporter of that tree would mint its own.
    /// </para>
    /// </remarks>
    public TraceId TraceId
    {
        get => _traceId;
        init => _traceId = TraceIdentity.AtConstruction(value, _roots);
    }

    /// <summary>Whether the trace captured nothing at all.</summary>
    /// <remarks>
    /// True for a disabled context, and also when a level filter removed every
    /// node — at <see cref="TracingLevel.Errors"/> a fully successful run
    /// legitimately produces an empty tree, which is not an error.
    /// </remarks>
    public bool IsEmpty => Roots.Count == 0;
}
