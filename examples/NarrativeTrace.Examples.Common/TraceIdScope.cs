// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Examples.Common;

/// <summary>
/// The ambient <c>traceId</c> shared by every logger of one
/// <see cref="ConsoleLoggerFactory"/> — flows with the async context, so a
/// scope opened before an <c>await</c> still stamps the lines after it.
/// </summary>
internal sealed class TraceIdScope
{
    private readonly AsyncLocal<string?> _current = new();

    /// <summary>The innermost open trace id, or <c>null</c> outside any scope.</summary>
    public string? Current => _current.Value;

    /// <summary>Makes <paramref name="traceId"/> current until the returned handle is disposed.</summary>
    public IDisposable Push(string traceId)
    {
        var previous = _current.Value;
        _current.Value = traceId;
        return new Restore(() => _current.Value = previous);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose()
        {
            restore();
        }
    }
}
