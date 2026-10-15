// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>What a context hands out to attach to an outgoing request.</summary>
public class OutboundTraceparentTests
{
    [Fact]
    public void No_value_before_any_span_exists()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnsureTraceId();

        Assert.Null(context.OutboundTraceparent());
    }

    [Fact]
    public void Value_follows_the_innermost_active_span_and_falls_back_when_it_exits()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        var outer = context.EnterMethod("Svc", "Outer", []);
        var inner = context.EnterMethod("Svc", "Inner", []);

        Assert.Equal(inner, context.OutboundTraceparent()!.ParentSpanId);
        context.ExitMethodWithReturn(null, inner);
        Assert.Equal(outer, context.OutboundTraceparent()!.ParentSpanId);
        Assert.Equal(context.CurrentTraceId, context.OutboundTraceparent()!.TraceId);
    }

    [Fact]
    public void Async_context_answers_inside_a_scope_and_null_outside()
    {
        var context = new AsyncNarrativeContext(new NarrativeTraceConfig());
        Assert.Null(context.OutboundTraceparent());

        context.Run(() =>
        {
            var span = context.EnterMethod("Svc", "Run", []);
            Assert.Equal(span, context.OutboundTraceparent()!.ParentSpanId);
        });
    }

    [Fact]
    public void Disabled_context_has_nothing_to_propagate()
    {
        Assert.Null(NoopContext.Instance.OutboundTraceparent());
        Assert.Null(TraceparentHeaders.Current(NoopContext.Instance));
    }

    [Fact]
    public void Header_helper_formats_the_context_value_and_rejects_null()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        context.EnterMethod("Svc", "Run", []);

        Assert.Equal(context.OutboundTraceparent()!.Format(), TraceparentHeaders.Current(context));
        Assert.Throws<ArgumentNullException>(() => TraceparentHeaders.Current(null!));
    }
}
