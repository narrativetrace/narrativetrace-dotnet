// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using FsCheck;
using FsCheck.Xunit;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Core.Tests;

public class SyncNarrativeContextPropertyTests
{
    [Property]
    public bool CaptureTrace_is_idempotent(PositiveInt callCount)
    {
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        ctx.EnterMethod("Svc", "Run", []);
        ctx.ExitMethodWithReturn(null);

        var first = ctx.CaptureTrace();
        for (var i = 0; i < callCount.Get; i++)
        {
            var again = ctx.CaptureTrace();
            if (again.Roots.Count != first.Roots.Count)
            {
                return false;
            }
        }

        return true;
    }

    [Property(MaxTest = 50)]
    public bool Balanced_enter_exit_produces_correct_root_count(
        PositiveInt rootCount)
    {
        var n = rootCount.Get % 20 + 1;
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        for (var i = 0; i < n; i++)
        {
            ctx.EnterMethod("Svc", $"M{i}", []);
            ctx.ExitMethodWithReturn(null);
        }

        return ctx.CaptureTrace().Roots.Count == n;
    }

    [Property(MaxTest = 50)]
    public bool Balanced_detach_complete_produces_correct_root_count(
        PositiveInt rootCount)
    {
        var n = rootCount.Get % 20 + 1;
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());
        var handles = new SpanId[n];

        for (var i = 0; i < n; i++)
        {
            handles[i] = ctx.EnterMethod("Svc", $"M{i}", []);
            ctx.DetachFrame(handles[i]);
        }

        for (var i = 0; i < n; i++)
        {
            ctx.ExitMethodWithReturn(null, handles[i]);
        }

        return ctx.CaptureTrace().Roots.Count == n;
    }

    [Property(MaxTest = 50)]
    public bool Handle_values_are_non_empty_when_active(
        PositiveInt callCount)
    {
        var n = callCount.Get % 20 + 1;
        var ctx = new SyncNarrativeContext(new NarrativeTraceConfig());

        for (var i = 0; i < n; i++)
        {
            var h = ctx.EnterMethod("Svc", $"M{i}", []);
            if (h.IsEmpty)
            {
                return false;
            }

            ctx.ExitMethodWithReturn(null, h);
        }

        return true;
    }
}
