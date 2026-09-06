// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Reflection;
using Xunit;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Target 1 of the parity document's fuzzing list — <b>N/A for this port</b>.
/// </summary>
/// <remarks>
/// <para>
/// INTENT: Java's target parses the W3C <c>traceparent</c>/<c>tracestate</c> wire headers
/// (<c>ai.narrativetrace.api.event.Traceparent#parse</c>). This port has never implemented header
/// parsing — the parity plan lists it as an explicit non-goal ("Java
/// non-goal too"), and <c>SpanContext.TraceState</c> is a plain settable string, never parsed by
/// anything in this codebase. The hostile corpus's <c>headers.json</c> (30 traceparent + 9
/// tracestate cases) is still copied verbatim — see <c>HostileCorpus.Traceparents</c>/<c>Tracestates</c>
/// and <c>HostileCorpusTest</c> — so a future parser lands with its fuzz cases already in place.
/// </para>
/// <para>
/// @llmNote This is a single evidence-bearing test rather than a silent gap: if a
/// <c>Traceparent</c>-shaped parsing type is ever added to <c>NarrativeTrace.Core</c>, this test
/// fails and says so, rather than this whole target staying quietly unported forever.
/// </para>
/// </remarks>
public class TraceparentParsingPropertyTests
{
    [Fact]
    public void No_traceparent_parser_exists_in_this_port_yet()
    {
        var coreAssembly = typeof(NarrativeTrace.Core.SpanContext).Assembly;

        var candidate = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name.Contains("Traceparent", StringComparison.OrdinalIgnoreCase));

        Assert.True(
            candidate is null,
            $"a traceparent-shaped type ({candidate?.FullName}) now exists — port Target 1 " +
            "(TraceparentParsingPropertyTest) from the Java security suite instead of leaving this stub.");
    }
}
