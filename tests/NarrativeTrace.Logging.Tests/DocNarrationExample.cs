// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using NarrativeTrace.Logging;

using Xunit;

namespace NarrativeTrace.Logging.Tests;

/// <summary>
/// Backs the Configuration Guide's worked example for
/// <c>NARRATIVETRACE_NARRATION</c> (<c>documentation/guides/configuration.md</c>)
/// — rule 8 (docs as tests): the fragment the page embeds is the exact
/// fragment this test runs, through <see cref="LoggingServiceCollectionExtensions.AddNarrativeLogging(IServiceCollection,TraceLoggingOptions?,Func{string,string?})"/>'s
/// injected-reader overload rather than the real process environment.
/// </summary>
public sealed class DocNarrationExample
{
    private static readonly string RepoRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    private static void Write(string name, string content)
    {
        var directory = Path.Combine(RepoRoot, "artifacts", "config-envvars");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, name), content);
    }

    private static string Capture(Action run)
    {
        var originalOut = Console.Out;
        var captured = new StringWriter { NewLine = "\n" };
        Console.SetOut(captured);
        try
        {
            run();
            return captured.ToString();
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Fact]
    public void Narration_worked_example()
    {
        var output = Capture(() =>
        {
            // snippet:begin narration
            var services = new ServiceCollection();
            services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

            services.AddNarrativeLogging(null, key => key == "NARRATIVETRACE_NARRATION" ? "off" : null);

            var listener = services.BuildServiceProvider().GetService<LoggingTraceEventListener>();
            Console.WriteLine($"listener is null == {listener is null}");
            // snippet:end narration
        });

        Write("narration.txt", output);
        Assert.Equal("listener is null == True\n", output);
    }
}
