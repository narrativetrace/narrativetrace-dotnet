// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Examples.Common;
using Xunit;

namespace NarrativeTrace.Examples.Common.Tests;

public sealed class ConsoleLoggerFactoryTests
{
    private readonly StringWriter _output = new();

    private ILogger Logger(LogFormat format, string category = "demo")
    {
        using var factory = new ConsoleLoggerFactory(_output, format);
        return factory.CreateLogger(category);
    }

    [Fact]
    public void Bare_format_writes_the_message_and_nothing_else()
    {
        Logger(LogFormat.Bare).LogInformation("=== Scenario 1 ===");

        Assert.Equal("=== Scenario 1 ===" + Environment.NewLine, _output.ToString());
    }

    // The exact prefix demo/colorize.awk strips: logback's
    // "%d{yyyy-MM-dd HH:mm:ss.SSS} %-5level [%thread] [%X{traceId}] [%logger] - %msg".
    private const string ClassicPrefix =
        @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [A-Z]+ +\[[^\]]*\] \[[^\]]*\] \[[^\]]*\] - ";

    [Fact]
    public void Classic_format_prefixes_timestamp_level_thread_trace_id_and_logger()
    {
        Logger(LogFormat.Classic, "narrativetrace").LogTrace("→ OrderService.PlaceOrder()");

        var line = _output.ToString().TrimEnd();
        Assert.Matches(ClassicPrefix + @"→ OrderService\.PlaceOrder\(\)$", line);
        Assert.Contains(" TRACE [", line, StringComparison.Ordinal);
        Assert.Contains("] [] [narrativetrace] - ", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Classic_format_shows_the_trace_id_of_the_enclosing_scope_until_it_is_disposed()
    {
        var logger = Logger(LogFormat.Classic);
        using (logger.BeginScope(new Dictionary<string, object> { ["traceId"] = "trace-001" }))
        {
            logger.LogInformation("inside");
        }

        logger.LogInformation("outside");

        var lines = _output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Contains("] [trace-001] [demo] - inside", lines[0], StringComparison.Ordinal);
        Assert.Contains("] [] [demo] - outside", lines[1], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(LogLevel.Trace, "TRACE")]
    [InlineData(LogLevel.Debug, "DEBUG")]
    [InlineData(LogLevel.Information, "INFO ")]
    [InlineData(LogLevel.Warning, "WARN ")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Critical, "FATAL")]
    public void Classic_level_names_follow_logback_padded_to_five(LogLevel level, string expected)
    {
        Logger(LogFormat.Classic).Log(level, "x");

        Assert.Contains($" {expected} [", _output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Classic_format_prefixes_only_the_first_line_of_a_multi_line_message()
    {
        Logger(LogFormat.Classic).LogInformation("\n--- Trace tree ---\n");

        var lines = _output.ToString().Split(Environment.NewLine);
        Assert.Matches(ClassicPrefix + "$", lines[0]);
        Assert.Equal("--- Trace tree ---", lines[1]);
        Assert.Equal("", lines[2]);
    }

    [Fact]
    public void Nested_scopes_show_the_innermost_trace_id_and_restore_the_outer_one()
    {
        var logger = Logger(LogFormat.Classic);
        using (logger.BeginScope(new Dictionary<string, object> { ["traceId"] = "outer" }))
        {
            using (logger.BeginScope(new Dictionary<string, object> { ["traceId"] = "inner" }))
            {
                logger.LogInformation("a");
            }

            using (logger.BeginScope(new Dictionary<string, object> { ["other"] = "field" }))
            {
                logger.LogInformation("b");
            }
        }

        var lines = _output.ToString().Split(Environment.NewLine);
        Assert.Contains("[inner] [demo] - a", lines[0], StringComparison.Ordinal);
        Assert.Contains("[outer] [demo] - b", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Scopes_without_a_trace_id_are_accepted_and_cost_nothing()
    {
        var logger = Logger(LogFormat.Bare);

        Assert.Null(logger.BeginScope("plain string state"));
        Assert.True(logger.IsEnabled(LogLevel.Trace));
    }

    [Fact]
    public void Factory_rejects_a_null_writer_and_takes_no_providers()
    {
        Assert.Throws<ArgumentNullException>(() => new ConsoleLoggerFactory(null!, LogFormat.Bare));
        using var factory = new ConsoleLoggerFactory(_output, LogFormat.Bare);
        Assert.Throws<NotSupportedException>(() => factory.AddProvider(null!));
    }
}
