// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.ContractProbe.Fixtures;

/// <summary>
/// The one piece of setup every probe under <c>Probes/</c> that runs a real traced call shares:
/// wrap <paramref name="target"/> behind a fresh <see cref="SyncNarrativeContext"/>, make the
/// call, and render the captured tree as plain text — the same three calls
/// <c>documentation/sixty-seconds.md</c> shows a first-time reader making, so a probe observes
/// exactly what a real consumer's terminal would show, never a logging-framework capture seam
/// .NET's own docs don't ask a reader to set up for this.
/// </summary>
internal static class TracedRender
{
    public static string Render<T>(T target, Action<T> call, ProxyOptions? options = null) where T : class
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        var proxy = NarrativeTraceProxy.Create(target, context, options);
        call(proxy);
        return IndentedTextRenderer.Render(context.CaptureTrace());
    }
}
