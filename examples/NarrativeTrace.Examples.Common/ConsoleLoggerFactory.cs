// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;

namespace NarrativeTrace.Examples.Common;

/// <summary>How a console log line is laid out.</summary>
public enum LogFormat
{
    /// <summary>The message alone — what the demo launcher styles.</summary>
    Bare,

    /// <summary>
    /// The traditional line every log tool shows: timestamp, level, thread,
    /// trace id, logger name, message — the same shape as logback's
    /// <c>%d %-5level [%thread] [%X{traceId}] [%logger] - %msg</c>.
    /// </summary>
    Classic,
}

/// <summary>
/// A dependency-free console <see cref="ILoggerFactory"/> for the examples:
/// the narration is ordinary <see cref="ILogger"/> traffic, and this is the
/// ordinary sink it lands in. Real applications plug in their own provider.
/// </summary>
public sealed class ConsoleLoggerFactory : ILoggerFactory
{
    private readonly TextWriter _output;
    private readonly LogFormat _format;

    // One scope state for every logger the factory creates: a traceId scope
    // opened on the example's logger must show on the event stream's lines
    // too, exactly as one MDC serves every SLF4J logger.
    private readonly TraceIdScope _scope = new();

    /// <summary>Creates a factory writing <paramref name="format"/> lines to <paramref name="output"/>.</summary>
    public ConsoleLoggerFactory(TextWriter output, LogFormat format)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
        _format = format;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new ConsoleLogger(_output, _format, categoryName, _scope);
    }

    /// <inheritdoc />
    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException(
            "ConsoleLoggerFactory writes to one console; it takes no providers.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // Nothing to release: the writer is owned by the caller.
    }
}
