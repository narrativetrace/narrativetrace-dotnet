// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Uniqueness is the rule, not the example: a trace id exists to tell two
/// captures apart, so no pair of span-less captures may ever share one — while
/// the story and chapter, which are derived rather than generated, stay stable
/// for the same call shape.
/// </summary>
/// <remarks>
/// The retired synthetic constant satisfied every example-based test of the
/// old behaviour and still broke exactly this property. Pinning the class of
/// defect, not the instance, is what keeps a future "make the fixtures
/// deterministic again" shortcut from reintroducing it.
/// </remarks>
public class TraceIdentityPropertyTests
{
    [Property(MaxTest = 100)]
    public void Two_span_less_captures_never_share_a_trace_id(
        NonEmptyString className, NonEmptyString methodName)
    {
        var first = SpanLessTree(className.Get, methodName.Get);
        var second = SpanLessTree(className.Get, methodName.Get);

        Assert.NotEqual(first.TraceId.Value, second.TraceId.Value);
        Assert.Matches("^[0-9a-f]{32}$", first.TraceId.Value);
        Assert.Matches("^[0-9a-f]{32}$", second.TraceId.Value);
    }

    [Property(MaxTest = 100)]
    public void Story_and_chapter_stay_derived_and_stable(
        NonEmptyString className, NonEmptyString methodName)
    {
        var expected = className.Get + "." + methodName.Get;

        var first = TraceIdentity.Of(SpanLessTree(className.Get, methodName.Get));
        var second = TraceIdentity.Of(SpanLessTree(className.Get, methodName.Get));

        Assert.Equal(expected, first.StoryId);
        Assert.Equal(expected, first.ChapterId);
        Assert.Equal(first.StoryId, second.StoryId);
        Assert.Equal(first.ChapterId, second.ChapterId);
    }

    [Property(MaxTest = 100)]
    public void Every_entry_of_one_span_less_capture_names_that_one_trace(
        NonEmptyString className, NonEmptyString methodName)
    {
        var tree = SpanLessTree(className.Get, methodName.Get);

        var entries = TraceTreeCanonicalMapper.FromTree(tree);

        Assert.All(entries, entry => Assert.Equal(tree.TraceId.Value, entry.TraceId));
        Assert.All(
            entries, entry => Assert.Equal(tree.TraceId.HumanName, entry.NtTraceName));
    }

    private static TraceTree SpanLessTree(string className, string methodName)
    {
        var child = new TraceNode(
            new MethodSignature(className + "Inner", methodName, []),
            new Returned("\"ok\""),
            [],
            0L);
        return new TraceTree([
            new TraceNode(
                new MethodSignature(className, methodName, []),
                new Returned("\"ok\""),
                [child],
                0L),
        ]);
    }
}
