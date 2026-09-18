// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;
using NarrativeTrace.Core;
using NarrativeTrace.Logging;
using NarrativeTrace.Proxy;
using NarrativeTrace.Runtime;

namespace NarrativeTrace.ContractProbe.Probes;

/// <summary>
/// <c>probed-default</c>: a logging bridge's own minimum level never affects what
/// <see cref="INarrativeContext.CaptureTrace"/> returns — configuration.md "Two dials, two paths".
/// Wraps a context in <see cref="LoggingNarrativeContext"/> with a logger that reports every level
/// disabled (the doc's "logger set to its highest level" case: nothing should ever print), traces
/// one call, and asserts the captured tree still holds the call and its return value.
/// </summary>
internal static class LoggerLevelDoesNotAffectCaptureProbe
{
    public interface IGreeter
    {
        string Greet(string name);
    }

    private sealed class Greeter : IGreeter
    {
        public string Greet(string name) => $"hello {name}";
    }

    private sealed class SilentLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Every level reports disabled — "the logger set to its highest level". LoggerMessage.Define
        // delegates check this before ever calling Log, so Log below must never run.
        public bool IsEnabled(LogLevel logLevel) => false;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            throw new InvalidOperationException(
                "IsEnabled() is false for every level — Log must never be called");
    }

    public static string Observe()
    {
        var inner = new SyncNarrativeContext(new NarrativeTraceConfig());
        INarrativeContext logging = new LoggingNarrativeContext(inner, new SilentLogger());
        var proxy = NarrativeTraceProxy.Create<IGreeter>(new Greeter(), logging);
        proxy.Greet("world");

        var call = inner.CaptureTrace().Roots
            .FirstOrDefault(n => n.Signature.MethodName == "Greet");
        var complete = call is { Outcome: Returned { RenderedValue: { } value } }
            && value.Contains("hello world", StringComparison.Ordinal);
        return complete ? "true" : "false";
    }
}
