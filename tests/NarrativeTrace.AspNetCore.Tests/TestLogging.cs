// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using Microsoft.Extensions.Logging;

namespace NarrativeTrace.AspNetCore.Tests;

/// <summary>
/// A minimal <see cref="ILogger"/> that records the state of every scope
/// opened on it, so tests can assert what correlation fields a component
/// pushes into logger scope.
/// </summary>
public sealed class ScopeCapturingLogger : ILogger
{
    private readonly List<object?> _scopes = new();

    public IReadOnlyList<object?> Scopes => _scopes;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        _scopes.Add(state);
        return NullScope.Instance;
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state,
        Exception? exception, Func<TState, Exception?, string> formatter)
    {
    }

    public IReadOnlyDictionary<string, object?> LastScopeAsMap()
    {
        var map = new Dictionary<string, object?>();
        if (_scopes.Count > 0
            && _scopes[^1] is IEnumerable<KeyValuePair<string, object?>> kvps)
        {
            foreach (var kvp in kvps)
            {
                map[kvp.Key] = kvp.Value;
            }
        }

        return map;
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}

public sealed class SingleLoggerFactory : ILoggerFactory
{
    private readonly ILogger _logger;

    public SingleLoggerFactory(ILogger logger) => _logger = logger;

    public ILogger CreateLogger(string categoryName) => _logger;

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}
