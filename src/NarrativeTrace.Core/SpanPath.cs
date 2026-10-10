// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Core;

/// <summary>
/// The citable id of one span in a rendered trace: its position path in the
/// tree, <c>#1</c>, <c>#1.3</c>, <c>#1.3.2</c> (root call, its third child,
/// that child's second child).
/// </summary>
/// <remarks>
/// INTENT: An agent's report, a grader, an approval delta and a human review
/// must be able to point at the same span. The id is DERIVED from the tree by
/// the renderer — never stored in the event stream, never random — so two runs
/// of the same flow give the same ids, and every flavour prints the same id for
/// the same span: the leading token of a <c>.nt</c> call line, a trailing
/// <c>#id</c> in indented text and Markdown, <c>(#id)</c> in prose, a note in a
/// sequence diagram. Not to be confused with <see cref="SpanId"/>, the captured
/// W3C span identifier, which no rendered flavour prints. Every sibling list
/// (roots, a call's children, the work a fire-and-forget launched) is numbered
/// in the order the structural trace prints it: plain calls in capture order,
/// the members of one fork or async group by <c>Class.method</c>, and a
/// fire-and-forget launch one position whose launched work nests under it.
/// </remarks>
public static class SpanPath
{
    /// <summary>The id of a child at a 1-based <paramref name="position"/> under <paramref name="parent"/>.</summary>
    /// <param name="parent">The parent's id; <see langword="null"/> for a root.</param>
    /// <param name="position">The 1-based position among its siblings.</param>
    /// <returns><c>#position</c> for a root, else <c>parent.position</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="position"/> is below 1.</exception>
    public static string Child(string? parent, int position)
    {
        if (position < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(position), position, "span positions are 1-based");
        }

        if (parent is null)
        {
            return position < RootIds.Length ? RootIds[position] : "#" + Digits(position);
        }

        return string.Concat(parent, ".", Digits(position));
    }

    /// <summary>
    /// Ids and digit runs of the first positions, built once: every rendered span takes one, and
    /// the common sibling list is short (a renderer benchmark of fifty roots allocated a string per
    /// span for its id alone).
    /// </summary>
    private const int CachedPositions = 128;

    private static readonly string[] PositionDigits = Enumerable.Range(0, CachedPositions)
        .Select(i => i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    private static readonly string[] RootIds = PositionDigits.Select(digits => "#" + digits).ToArray();

    internal static string Digits(int position) =>
        position < CachedPositions
            ? PositionDigits[position]
            : position.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// The id of every node in <paramref name="siblings"/>, index for index,
    /// for a renderer that walks them in capture order rather than in the
    /// order ids are given in.
    /// </summary>
    /// <param name="siblings">One node's children, or a tree's roots.</param>
    /// <param name="parent">The id of the node they belong to; <see langword="null"/> for roots.</param>
    /// <returns>
    /// One id per sibling. A fire-and-forget segment takes one position: its
    /// launcher stub carries that id and the launched work is numbered under it.
    /// </returns>
    public static IReadOnlyList<string> IdsOf(IReadOnlyList<TraceNode> siblings, string? parent)
    {
        if (siblings is null)
        {
            throw new ArgumentNullException(nameof(siblings));
        }

        var ids = new List<string>(siblings.Count);
        var cursor = new SpanCursor(parent);
        foreach (var segment in ChildSegment.Partition(siblings))
        {
            AddIds(segment, cursor, ids);
        }

        return ids;
    }

    private static void AddIds(ChildSegment segment, SpanCursor cursor, List<string> ids)
    {
        if (segment.Kind == ConcurrencyKind.FireAndForget)
        {
            AddLaunchIds(segment, cursor.Next(), ids);
            return;
        }

        var segmentIds = new string[segment.Nodes.Count];
        foreach (var index in RanksInConcurrentOrder(segment.Nodes))
        {
            segmentIds[index] = cursor.Next();
        }

        ids.AddRange(segmentIds);
    }

    /// <summary>
    /// A fire-and-forget segment takes one position: its launcher stub carries
    /// the launch's id, and the launched work is numbered under it in capture order.
    /// </summary>
    private static void AddLaunchIds(ChildSegment segment, string launchId, List<string> ids)
    {
        var launched = new SpanCursor(launchId);
        foreach (var node in segment.Nodes)
        {
            ids.Add(FireAndForgetSection.IsLauncherStub(node) ? launchId : launched.Next());
        }
    }

    /// <summary>
    /// <paramref name="line"/> without its leading span id: <c>"  #1.2 - A.B()"</c>
    /// becomes <c>"  - A.B()"</c>. A line that carries no id — a marker, a
    /// header, a baseline written before ids existed — is returned unchanged,
    /// which is what lets an id-free <c>.approved.nt</c> still compare.
    /// </summary>
    /// <param name="line">One line of a structural trace, without its terminator.</param>
    /// <returns>The line with the id and the one space after it removed.</returns>
    public static string Strip(string line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        var start = IndentOf(line);
        var end = IdEnd(line, start);
        return end < 0 ? line : line[..start] + line[(end + 1)..];
    }

    /// <summary>The span id <paramref name="line"/> opens with (after its indent), or <see langword="null"/>.</summary>
    /// <param name="line">One line of a structural trace, without its terminator.</param>
    public static string? Of(string line)
    {
        if (line is null)
        {
            throw new ArgumentNullException(nameof(line));
        }

        var start = IndentOf(line);
        var end = IdEnd(line, start);
        return end < 0 ? null : line[start..end];
    }

    /// <summary>
    /// Whether <paramref name="text"/> is exactly one span id: <c>#</c>, then
    /// dot-separated runs of ASCII digits.
    /// </summary>
    public static bool IsWellFormed(string? text)
    {
        return text is not null && IdEnd(text + " ", 0) == text.Length;
    }

    /// <summary>
    /// The members of one concurrent group in the order they take their ids:
    /// by <c>Class.method</c> (ordinal), equal signatures keeping capture
    /// order. Capture order across threads is the scheduler's, so it can
    /// number nothing; the sort is stable so equal signatures stay put.
    /// </summary>
    internal static IReadOnlyList<TraceNode> InConcurrentOrder(IReadOnlyList<TraceNode> members)
    {
        var ranks = RanksInConcurrentOrder(members);
        var ordered = new TraceNode[ranks.Length];
        for (var i = 0; i < ranks.Length; i++)
        {
            ordered[i] = members[ranks[i]];
        }

        return ordered;
    }

    /// <summary>The capture indices of <paramref name="members"/>, in concurrent order (stable).</summary>
    private static int[] RanksInConcurrentOrder(IReadOnlyList<TraceNode> members)
    {
        var ranks = new int[members.Count];
        for (var i = 0; i < ranks.Length; i++)
        {
            ranks[i] = i;
        }

        Array.Sort(ranks, new ConcurrentOrder(members));
        return ranks;
    }

    /// <summary>
    /// Orders capture indices by their member's <c>Class.method</c>, ordinally, as if the two names were
    /// joined by a dot — without joining them (a key string per member was the bulk of a fork's render
    /// allocation) — and by capture index on a tie, which makes the unstable array sort stable.
    /// </summary>
    private sealed class ConcurrentOrder(IReadOnlyList<TraceNode> members) : IComparer<int>
    {
        public int Compare(int x, int y)
        {
            var bySignature = CompareJoined(members[x].Signature, members[y].Signature);
            return bySignature != 0 ? bySignature : x.CompareTo(y);
        }

        private static int CompareJoined(MethodSignature a, MethodSignature b)
        {
            var shared = Math.Min(a.ClassName.Length, b.ClassName.Length);
            var byClass = string.CompareOrdinal(a.ClassName, 0, b.ClassName, 0, shared);
            if (byClass != 0)
            {
                return byClass;
            }

            return a.ClassName.Length == b.ClassName.Length
                ? string.CompareOrdinal(a.MethodName, b.MethodName)
                : CompareFrom(a, b, shared);
        }

        /// <summary>
        /// The rare case — one class name is a prefix of the other (<c>Outer</c>, <c>Outer+Inner</c>):
        /// walk the joined names from where they first can differ.
        /// </summary>
        private static int CompareFrom(MethodSignature a, MethodSignature b, int start)
        {
            var length = Math.Min(JoinedLength(a), JoinedLength(b));
            for (var i = start; i < length; i++)
            {
                var difference = JoinedAt(a, i) - JoinedAt(b, i);
                if (difference != 0)
                {
                    return difference;
                }
            }

            return JoinedLength(a) - JoinedLength(b);
        }

        private static int JoinedLength(MethodSignature s) => s.ClassName.Length + 1 + s.MethodName.Length;

        private static char JoinedAt(MethodSignature s, int i) =>
            i < s.ClassName.Length ? s.ClassName[i]
            : i == s.ClassName.Length ? '.'
            : s.MethodName[i - s.ClassName.Length - 1];
    }

    private static int IndentOf(string line)
    {
        var i = 0;
        while (i < line.Length && line[i] == ' ')
        {
            i++;
        }

        return i;
    }

    /// <summary>
    /// Index of the space that ends an id starting at <paramref name="start"/>,
    /// or -1 when there is no well-formed id there (<c>#</c>, then
    /// dot-separated runs of ASCII digits, then one space).
    /// </summary>
    private static int IdEnd(string line, int start)
    {
        if (start >= line.Length || line[start] != '#')
        {
            return -1;
        }

        var i = start + 1;
        var digitsInRun = 0;
        while (i < line.Length && IsIdCharacter(line[i], ref digitsInRun))
        {
            i++;
        }

        var endsWithSpace = i < line.Length && line[i] == ' ';
        return endsWithSpace && digitsInRun > 0 ? i : -1;
    }

    /// <summary>
    /// Whether <paramref name="c"/> continues an id: a digit, or a dot after at
    /// least one digit (which starts a new run).
    /// </summary>
    private static bool IsIdCharacter(char c, ref int digitsInRun)
    {
        if (c is >= '0' and <= '9')
        {
            digitsInRun++;
            return true;
        }

        if (c != '.' || digitsInRun == 0)
        {
            return false;
        }

        digitsInRun = 0;
        return true;
    }
}

/// <summary>
/// Hands out the <see cref="SpanPath"/> ids of one sibling list, in the order a
/// renderer lays it out.
/// </summary>
/// <remarks>
/// Every renderer here lays a sibling list out segment by segment — a plain
/// call, a fork's members in concurrent order, a fire-and-forget launch — and
/// that layout order IS the id order. Taking the next id at the moment a span
/// is laid out keeps the numbering in one place instead of an offset threaded
/// through every renderer.
/// </remarks>
internal sealed class SpanCursor
{
    private readonly string? _parent;
    private int _position;

    /// <param name="parent">The id of the node whose children are laid out; <see langword="null"/> for roots.</param>
    internal SpanCursor(string? parent)
    {
        _parent = parent;
    }

    /// <summary>The id of the next sibling in layout order.</summary>
    internal string Next() => NextRef().ToString();

    /// <summary>
    /// The next sibling's id as a <see cref="SpanRef"/>: printed straight into a renderer's buffer,
    /// made a string only when the span has children that need it as their parent.
    /// </summary>
    internal SpanRef NextRef()
    {
        _position++;
        return new SpanRef(_parent, _position);
    }
}

/// <summary>
/// A span id not yet made a string: its parent's id and its position. Every rendered line prints one,
/// and most spans are leaves, so a renderer appends it where it goes instead of allocating it.
/// </summary>
internal readonly record struct SpanRef(string? Parent, int Position)
{
    /// <summary>Appends the id — <c>#3</c>, <c>#1.3</c> — to <paramref name="sb"/>.</summary>
    internal System.Text.StringBuilder AppendTo(System.Text.StringBuilder sb) =>
        Parent is null
            ? sb.Append('#').Append(SpanPath.Digits(Position))
            : sb.Append(Parent).Append('.').Append(SpanPath.Digits(Position));

    /// <summary>The id as a string — what the span's children are numbered under.</summary>
    public override string ToString() => SpanPath.Child(Parent, Position);
}

/// <summary>A node and the span id a renderer prints for it.</summary>
internal readonly record struct CitedNode(TraceNode Node, SpanRef Id);
