// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class TraceIdEagerGenerationTests
{
    [Fact]
    public void EnsureTraceId_equals_first_span_trace_id()
    {
        INarrativeContext ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var eager = ctx.EnsureTraceId();
        var span = ctx.EnterMethod("A", "B", []);
        ctx.ExitMethodWithReturn(null, span);

        var root = ctx.CaptureTrace().Roots[0];
        Assert.Equal(eager, root.SpanContext!.TraceId);
    }

    [Fact]
    public void EnsureTraceId_is_non_null_and_stable_before_first_enter()
    {
        INarrativeContext ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var first = ctx.EnsureTraceId();
        var second = ctx.EnsureTraceId();

        Assert.False(first.IsEmpty);
        Assert.Equal(first, second);
        Assert.Equal(first, ctx.CurrentTraceId);
    }

    [Fact]
    public void EnsureTraceId_regenerates_after_Reset()
    {
        INarrativeContext ctx = new SyncNarrativeContext(
            new NarrativeTraceConfig());

        var before = ctx.EnsureTraceId();
        ctx.Reset();
        var after = ctx.EnsureTraceId();

        Assert.NotEqual(before, after);
    }
}
