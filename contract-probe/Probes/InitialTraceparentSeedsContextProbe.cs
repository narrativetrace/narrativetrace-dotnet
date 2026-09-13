// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>, since 0.1.4: a context built from
/// <c>new NarrativeTraceConfig(initialTraceparent: Traceparent.Parse(...))</c> adopts the parsed
/// trace id as its own <c>CurrentTraceId</c> — configuration-guide.md "Traceparent seeding", the
/// no-HTTP-header sibling of <see cref="MiddlewareAdoptsTraceparentProbe"/>'s inbound-header path.
/// </summary>
/// <remarks>
/// <c>NarrativeTrace.Core.Traceparent</c> does not exist in the 0.1.3 package this project compiles
/// against by default (same situation <see cref="ReflectablePerInvocationIdentityProbe"/>
/// documents), so it is found and invoked entirely through reflection. <see cref="NarrativeTraceConfig"/>
/// and <see cref="TraceId"/> already existed before 0.1.4, so they are used directly — only the
/// specific 3-parameter constructor overload that accepts a <c>Traceparent</c> is new, found by
/// shape (parameter count) rather than by naming it at compile time.
/// </remarks>
internal static class InitialTraceparentSeedsContextProbe
{
    private const string SampleTraceparent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01";

    public static string Observe()
    {
        var traceparentType = Type.GetType("NarrativeTrace.Core.Traceparent, NarrativeTrace.Core")
            ?? throw new InvalidOperationException(
                "NarrativeTrace.Core.Traceparent not found in the installed package — this probe "
                    + "should never run before its contract entry's since version");
        var parse = traceparentType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static)!;
        var parsed = parse.Invoke(null, [SampleTraceparent])
            ?? throw new InvalidOperationException("Traceparent.Parse rejected a well-formed sample");
        var expectedTraceId = ((TraceId)traceparentType.GetProperty("TraceId")!.GetValue(parsed)!).Value;

        var config = BuildConfig(parsed);
        var context = new SyncNarrativeContext(config);

        return context.CurrentTraceId?.Value == expectedTraceId ? "true" : "false";
    }

    private static NarrativeTraceConfig BuildConfig(object parsedTraceparent)
    {
        var ctor = typeof(NarrativeTraceConfig).GetConstructors()
            .Single(c => c.GetParameters().Length == 3);
        return (NarrativeTraceConfig)ctor.Invoke([TracingLevel.Detail, null, parsedTraceparent]);
    }
}
