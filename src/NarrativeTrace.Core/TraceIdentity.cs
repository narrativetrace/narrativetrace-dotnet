// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The identity every entry and every chapter of one <see cref="TraceTree"/>
/// shares: which trace it belongs to, and which story and chapter name it.
/// </summary>
/// <remarks>
/// <para>
/// One resolution, read by every exporter, so two exporters reading one tree
/// cannot name two different traces. <c>chapter.schema.json</c> requires
/// <c>trace_id</c>, <c>nt.storyId</c> and <c>nt.chapterId</c>, so identity is
/// never omitted; it is resolved eagerly instead —
/// <b>adopt → inherit → generate</b>: adopt the id the tree already carries,
/// else inherit the first <see cref="SpanContext"/> found <em>anywhere</em> in
/// the tree (depth-first, never roots only), else generate a real, unique,
/// W3C-shaped id.
/// </para>
/// <para>
/// Generation lives in <see cref="TraceTree"/>, not in an exporter, and the
/// placement is the point: a tree with nodes has resolved its id once, at
/// construction, so <see cref="Of"/> only reaches its generate rung for a tree
/// that carries neither an id nor a context — an empty one, which has no
/// entries that could disagree with the chapter.
/// </para>
/// <para>
/// The story is <b>never</b> generated and the title never stands in for it:
/// it is inherited when the span context carries one, else derived from the
/// first root-level call (<c>ClassName.MethodName</c>), else
/// <see cref="UnknownStory"/> — the fallback an empty tree's title already
/// uses. The chapter id is the inherited one, or the story.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var identity = TraceIdentity.Of(tree);
/// writer.WriteString("trace_id", identity.TraceId.Value);   // always present
/// writer.WriteString("nt.storyId", identity.StoryId);       // never generated
/// </code>
/// </example>
/// <param name="Inherited">
/// The first span context found in the tree, depth-first, or
/// <see langword="null"/> when no node carried one. It answers for service and
/// environment too, so a mixed tree — some nodes captured with a context, some
/// grafted in without — reports one service instead of falling back to
/// <c>unknown</c> because its first root happened to be context-free.
/// </param>
/// <param name="TraceId">
/// The resolved trace id: always well-formed (32 lowercase hex, never
/// all-zeroes), never <see cref="Core.TraceId.Empty"/>.
/// </param>
/// <param name="StoryId">The resolved story id; never <see langword="null"/> or empty.</param>
/// <param name="ChapterId">The resolved chapter id; never <see langword="null"/> or empty.</param>
public sealed record TraceIdentity(
    SpanContext? Inherited,
    TraceId TraceId,
    string StoryId,
    string ChapterId)
{
    /// <summary>
    /// The story and chapter of a trace with no root call to name them.
    /// </summary>
    /// <remarks>
    /// An empty tree has nothing to derive a story from, and the schema still
    /// requires one — so it takes the same fallback the chapter title uses,
    /// rather than the title standing in for an id.
    /// </remarks>
    public const string UnknownStory = "unknown";

    /// <summary>
    /// The human-readable name of <see cref="TraceId"/>, always in agreement
    /// with it because it is derived from it rather than carried beside it.
    /// </summary>
    /// <remarks>
    /// Three lowercase words (<c>"bold elk soars"</c>), matching the
    /// <c>nt.traceName</c> pattern in both schemas. Never empty, since
    /// <see cref="TraceId"/> is always a real id.
    /// </remarks>
    public string TraceName => TraceId.HumanName;

    /// <summary>Resolves the one identity a finished trace reports.</summary>
    /// <param name="tree">The finished trace; must not be <see langword="null"/>.</param>
    /// <returns>
    /// The tree's identity — complete, with a well-formed trace id and a
    /// non-empty story and chapter, for an empty tree as much as a full one.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is null.</exception>
    /// <remarks>
    /// Cheap but not free — it walks the tree for the first span context, so
    /// resolve once per export rather than per entry.
    /// </remarks>
    public static TraceIdentity Of(TraceTree tree)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        var inherited = FirstSpanContext(tree.Roots);
        var story = StoryOf(inherited, tree.Roots);
        return new TraceIdentity(
            inherited,
            Resolve(tree.TraceId, inherited),
            story,
            NonEmpty(inherited?.ChapterId) ?? story);
    }

    /// <summary>The identity one node reports inside this tree.</summary>
    /// <param name="node">The node being emitted; must not be <see langword="null"/>.</param>
    /// <returns>
    /// The node's own span context where it has one, this identity otherwise —
    /// so a node that lost its context is never stamped with a second trace id.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is null.</exception>
    public TraceIdentity For(TraceNode node)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (node.SpanContext is not { } own)
        {
            return this;
        }

        return new TraceIdentity(
            own,
            own.TraceId.IsEmpty ? TraceId : own.TraceId,
            NonEmpty(own.StoryId) ?? StoryId,
            NonEmpty(own.ChapterId) ?? ChapterId);
    }

    /// <summary>
    /// The trace id of a tree being constructed: adopt → inherit → generate,
    /// with the generate rung reserved for a tree that actually ran something.
    /// </summary>
    /// <remarks>
    /// An empty tree keeps <see cref="Core.TraceId.Empty"/>: nothing ran, so
    /// there is nothing to identify, and an idle context must not acquire an
    /// identity merely by being asked for its trace.
    /// </remarks>
    internal static TraceId AtConstruction(TraceId adopted, IReadOnlyList<TraceNode> roots)
    {
        return roots.Count == 0 ? adopted : Resolve(adopted, FirstSpanContext(roots));
    }

    private static TraceId Resolve(TraceId adopted, SpanContext? inherited)
    {
        if (!adopted.IsEmpty)
        {
            return adopted;
        }

        return inherited is { TraceId.IsEmpty: false }
            ? inherited.TraceId
            : SpanIdGenerator.GenerateTraceId();
    }

    // Bounded and cycle-safe (TreeWalk), not plain recursion: nodes[i].Children
    // is a type, not a guarantee of acyclicity, and this runs unconditionally
    // on every TraceTree construction — a hand-built or replayed tree can hold
    // a genuine reference cycle, and this must terminate rather than recurse
    // the call stack forever.
    private static SpanContext? FirstSpanContext(IReadOnlyList<TraceNode> nodes)
    {
        return TreeWalk.FindFirst(nodes, n => n.SpanContext is not null)?.SpanContext;
    }

    private static string StoryOf(SpanContext? inherited, IReadOnlyList<TraceNode> roots)
    {
        if (NonEmpty(inherited?.StoryId) is { } inheritedStory)
        {
            return inheritedStory;
        }

        return roots.Count == 0
            ? UnknownStory
            : roots[0].Signature.ClassName + "." + roots[0].Signature.MethodName;
    }

    private static string? NonEmpty(string? value)
    {
        return string.IsNullOrEmpty(value) ? null : value;
    }
}
