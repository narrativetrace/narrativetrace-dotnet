// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;

using System.Globalization;

namespace NarrativeTrace.Runtime;

/// <summary>
/// Flattens a finished <see cref="TraceTree"/> into the canonical entry list.
/// </summary>
/// <remarks>
/// <para>
/// The per-test canonical artifact is derived from the captured tree after the
/// run, not from the live event stream: each node yields one
/// <c>method_enter</c> and one <c>method_exit</c> entry in depth-first order,
/// linked by span ids. Nodes captured without a <see cref="SpanContext"/> —
/// plain unit-test trees — take the tree's own identity
/// (<see cref="TraceIdentity"/>) and sequential span ids, so nesting depth
/// survives the flattening either way.
/// </para>
/// <para>
/// Unlike <see cref="CanonicalEntryMapper.FromEvent"/>, the exit entries here
/// carry the node's real <c>code.namespace</c> / <c>code.function</c>: the tree
/// still knows its signature, whereas an exit <em>event</em> knows only its
/// span name.
/// </para>
/// </remarks>
public static class TraceTreeCanonicalMapper
{
    /// <summary>Flattens the tree into enter/exit entries, depth-first, roots in order.</summary>
    /// <param name="tree">The finished trace; must not be null.</param>
    /// <returns>
    /// The entries in emission order, empty for an empty tree. Every entry
    /// validates against <c>entry.schema.json</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="tree"/> is null.</exception>
    public static IReadOnlyList<CanonicalEntry> FromTree(TraceTree tree)
    {
        if (tree is null)
        {
            throw new ArgumentNullException(nameof(tree));
        }

        var entries = new List<CanonicalEntry>();
        var identity = TraceIdentity.Of(tree);
        var spans = new SpanIdSequence();
        // TraceNode.Children is a type, not a guarantee of acyclicity - bound
        // once, here, so AppendNode's recursive walk can never overflow the
        // stack or loop forever on a hand-built or replayed cycle. Cheap on
        // ordinary input: TreeWalk.Bound returns Roots unchanged once it
        // confirms there is nothing to bound.
        foreach (var root in TreeWalk.Bound(tree.Roots))
        {
            AppendNode(entries, root, null, spans, identity);
        }

        return entries;
    }

    private static void AppendNode(
        List<CanonicalEntry> entries,
        TraceNode node,
        string? parentSpanId,
        SpanIdSequence spans,
        TraceIdentity identity)
    {
        var spanId = spans.Next(node);
        entries.Add(EnterEntry(node, spanId, parentSpanId, identity));
        foreach (var child in node.Children)
        {
            AppendNode(entries, child, spanId, spans, identity);
        }

        entries.Add(ExitEntry(node, spanId, parentSpanId, identity));
    }

    private static CanonicalEntry EnterEntry(
        TraceNode node, string spanId, string? parentSpanId, TraceIdentity identity)
    {
        var signature = node.Signature;
        return Common(node, spanId, parentSpanId, identity) with
        {
            Timestamp = Timestamp(node.StartTimestamp),
            Level = "trace",
            Message = "→ " + signature.ClassName + "." + signature.MethodName,
            NtEventType = "method_enter",
            NtParameters = Parameters(signature),
            NtNarrationTemplate = signature.NarrationTemplate,
            NtReturnType = signature.ReturnType,
        };
    }

    private static CanonicalEntry ExitEntry(
        TraceNode node, string spanId, string? parentSpanId, TraceIdentity identity)
    {
        var outcome = node.Outcome;
        return Common(node, spanId, parentSpanId, identity) with
        {
            Timestamp = Timestamp(node.StartTimestamp + node.DurationTicks),
            Level = outcome is Threw ? "error" : "trace",
            Message = ExitMessage(node.Signature, outcome),
            NtEventType = "method_exit",
            NtOutcome = OutcomeName(outcome),
            DurationMs = node.DurationTicks / TimeSpan.TicksPerMillisecond,
            NtReturnValue = (outcome as Returned)?.RenderedValue,
            ExceptionType = (outcome as Threw)?.Error?.GetType().Name,
            ExceptionMessage = ExceptionMessage.Of((outcome as Threw)?.Error),
            NtExceptionPackage = (outcome as Threw)?.Error?.GetType().Namespace,
        };
    }

    /// <summary>Identity and correlation shared by one node's enter and exit entries.</summary>
    /// <remarks>
    /// Trace-scoped fields come off the node's own context when it has one and
    /// off the tree's resolved identity otherwise — never regenerated per node,
    /// or a context-free child would split its own trace's identity.
    /// </remarks>
    private static CanonicalEntry Common(
        TraceNode node, string spanId, string? parentSpanId, TraceIdentity identity)
    {
        var nodeIdentity = identity.For(node);
        return new CanonicalEntry(
            Timestamp: string.Empty,
            Level: "trace",
            Message: string.Empty,
            Service: CanonicalEntryMapper.ServiceNameOrUnknown(
                nodeIdentity.Inherited?.ServiceName),
            Environment: nodeIdentity.Inherited?.Environment,
            TraceId: nodeIdentity.TraceId.Value,
            SpanId: spanId,
            ParentSpanId: parentSpanId,
            CodeNamespace: node.Signature.ClassName,
            CodeFunction: node.Signature.MethodName,
            NtEventType: "method_enter",
            NtTraceName: nodeIdentity.TraceName,
            NtStoryId: nodeIdentity.StoryId,
            NtChapterId: nodeIdentity.ChapterId,
            NtPackage: node.Signature.Namespace);
    }

    /// <summary>
    /// Node clocks are monotonic with an arbitrary origin, and this port carries
    /// no per-trace wall-clock anchor, so the reading is formatted as an offset
    /// from the epoch. That keeps the artifact byte-identical run to run, which
    /// is what the conformance fixtures compare.
    /// </summary>
    private static string Timestamp(long timeSpanTicks)
    {
        return DateTimeOffset
            .FromUnixTimeMilliseconds(timeSpanTicks / TimeSpan.TicksPerMillisecond)
            .UtcDateTime
            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
    }

    private static string ExitMessage(MethodSignature signature, TraceOutcome outcome)
    {
        if (outcome is Threw threw)
        {
            return "!! " + threw.Error?.GetType().Name + ": " + ExceptionMessage.Text(threw.Error);
        }

        var name = signature.ClassName + "." + signature.MethodName;
        return outcome is Incomplete ? "← " + name + " incomplete" : "← " + name;
    }

    private static string OutcomeName(TraceOutcome outcome)
    {
        return outcome switch
        {
            Returned => "success",
            Threw => "failure",
            _ => "incomplete",
        };
    }

    private static IReadOnlyList<ParameterCapture>? Parameters(MethodSignature signature)
    {
        if (signature.Parameters.Count == 0)
        {
            return null;
        }

        var captured = new List<ParameterCapture>(signature.Parameters.Count);
        foreach (var parameter in signature.Parameters)
        {
            captured.Add(
                parameter.Redacted
                    ? parameter with { RenderedValue = "[REDACTED]" }
                    : parameter);
        }

        return captured;
    }

    /// <summary>
    /// Hands out each node's span id: its real one, or the next deterministic
    /// synthetic id in document order.
    /// </summary>
    private sealed class SpanIdSequence
    {
        private long issued;

        internal string Next(TraceNode node)
        {
            return node.SpanContext is { } context
                ? context.SpanId.Value
                : (++this.issued).ToString("x16", CultureInfo.InvariantCulture);
        }
    }
}
