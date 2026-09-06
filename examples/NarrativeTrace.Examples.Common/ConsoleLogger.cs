// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Globalization;
using Microsoft.Extensions.Logging;

namespace NarrativeTrace.Examples.Common;

/// <summary>Writes each log entry as one console line in the chosen <see cref="LogFormat"/>.</summary>
internal sealed class ConsoleLogger(
    TextWriter output, LogFormat format, string category, TraceIdScope scope) : ILogger
{
    private const string TraceIdKey = "traceId";

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return TraceIdOf(state) is { } traceId ? scope.Push(traceId) : null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        var message = formatter(state, exception);
        output.WriteLine(
            format == LogFormat.Classic ? ClassicPrefix(logLevel) + message : message);
    }

    private static string? TraceIdOf<TState>(TState state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object>> fields)
        {
            return null;
        }

        return fields
            .Where(f => f.Key == TraceIdKey)
            .Select(f => f.Value.ToString())
            .FirstOrDefault();
    }

    private string ClassicPrefix(LogLevel logLevel)
    {
        var timestamp = DateTime.Now.ToString(
            "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        var thread = Thread.CurrentThread.Name
            ?? string.Create(CultureInfo.InvariantCulture, $"thread-{Environment.CurrentManagedThreadId}");
        return $"{timestamp} {LevelName(logLevel),-5} [{thread}] [{scope.Current}] [{category}] - ";
    }

    private static string LevelName(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRACE",
            LogLevel.Debug => "DEBUG",
            LogLevel.Information => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "FATAL",
        };
    }
}
