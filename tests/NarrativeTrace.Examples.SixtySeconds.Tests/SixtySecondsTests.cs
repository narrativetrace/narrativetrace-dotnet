// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;
using NarrativeTrace.TestingXunit;
using Xunit;

namespace NarrativeTrace.Examples.SixtySeconds.Tests;

/// <summary>
/// "See a trace in 60 seconds" (<c>documentation/sixty-seconds.md</c>) as
/// a real, compiled, tested project — rule 8 (docs as tests). Runs the
/// page's exact call
/// through the real traced proxy and saves the two output blocks the page
/// embeds via <c>snippet-check</c>; if either changes, the page was wrong —
/// run <c>./build.sh SnippetSync</c> and read the diff before touching the
/// page by hand.
/// </summary>
public sealed class SixtySecondsTests : IClassFixture<NarrativeFixture>
{
    private readonly NarrativeFixture _trace;

    public SixtySecondsTests(NarrativeFixture trace)
    {
        _trace = trace;
    }

    /// <summary>
    /// Section 2/3 of the page: the plain <c>Program.cs</c> call, rendered by
    /// <see cref="IndentedTextRenderer"/> exactly as the console-run entry
    /// point prints it, plus the runtime's own test-artifact write (output on
    /// by default since 2026-09-11) that proves the traced proxy really ran
    /// under the shipped test integration, not only by hand.
    /// </summary>
    [Fact]
    public void Places_an_order_through_the_real_traced_proxy()
    {
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), _trace.Context);

        var confirmation = orders.PlaceOrder("cust-1", "book-123", 2);

        Assert.Equal("confirmed:cust-1:book-123:2", confirmation);

        var rendered = IndentedTextRenderer.Render(_trace.CaptureTrace());
        Assert.Contains("IOrderService.PlaceOrder", rendered);
        CapturedOutput.Write("see-a-trace.txt", rendered);

        _trace.WriteArtifacts(
            nameof(SixtySecondsTests), nameof(Places_an_order_through_the_real_traced_proxy), failed: false);
    }

    /// <summary>
    /// The "Send it to your logger" postscript: the same call, plus the
    /// diff's four lines, run for real via <see cref="WithLogger.Run"/> —
    /// backs the page's second output block, the one showing the
    /// <c>ILogger</c> record.
    /// </summary>
    [Fact]
    public void Sends_the_trace_to_its_logger()
    {
        var captured = WithLogger.Run();

        Assert.Contains("info: NarrativeTrace[1]", captured);
        CapturedOutput.Write("see-a-trace-with-logger.txt", captured);
    }
}
