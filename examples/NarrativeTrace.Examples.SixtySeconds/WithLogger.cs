// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.Examples.SixtySeconds;

/// <summary>
/// The "Send it to your logger" postscript
/// (<c>documentation/sixty-seconds.md</c>) — the same tutorial call as
/// <c>Program.cs</c>, plus the two packages and four lines the page's diff
/// block adds. A second file rather than a second run mode of
/// <c>Program.cs</c>, so that file — and the code block it backs — stays
/// byte-identical forever; this one is never itself the entry point
/// <c>dotnet run</c> executes.
/// </summary>
public static class WithLogger
{
    /// <summary>
    /// Runs the postscript's call and returns exactly what a reader would see
    /// printed to their terminal after applying the diff and running
    /// <c>dotnet run</c> again: the unchanged console renderer line, then the
    /// <c>ILogger</c> record <see cref="TraceLogExporter"/> replays onto it.
    /// </summary>
    /// <remarks>
    /// Disables the console formatter's color codes — not part of the page's
    /// diff, which configures nothing — purely so the captured text is the
    /// plain characters a terminal shows, never the ANSI escapes underneath
    /// them: a real terminal emulator strips those before a reader could ever
    /// copy them into a page, but a redirected <see cref="Console.Out"/>
    /// wouldn't know to.
    /// </remarks>
    public static string Run()
    {
        var originalOut = Console.Out;
        var captured = new StringWriter { NewLine = "\n" };
        Console.SetOut(captured);
        try
        {
            RunTutorial();
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    // Console.Out is already redirected by Run() above by the time this
    // executes — split out only to keep Run() under the line-count gate.
    private static void RunTutorial()
    {
        var context = new SyncNarrativeContext(new NarrativeTraceConfig());
        var orders = NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);

        orders.PlaceOrder("cust-1", "book-123", 2);

        var tree = context.CaptureTrace();
        Console.WriteLine(IndentedTextRenderer.Render(tree));

        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddSimpleConsole(options => options.ColorBehavior = LoggerColorBehavior.Disabled));
        // Disposal (end of scope) blocks until the console provider's
        // background writer thread has flushed everything queued above —
        // without it, Run()'s captured text could be read before the
        // logger's line ever lands in it.
        TraceLogExporter.ExportToLogger(tree, loggerFactory.CreateLogger("NarrativeTrace"));
    }
}
