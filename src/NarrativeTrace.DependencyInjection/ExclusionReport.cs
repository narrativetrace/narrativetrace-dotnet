// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.DependencyInjection;

/// <summary>
/// Says, once, what tracing left alone: one line per exclude prefix that carved a target out of the
/// include list (never per target), and one per service type NarrativeTrace could not proxy.
/// </summary>
internal sealed class ExclusionReport
{
    private readonly TextWriter _writer;
    private readonly HashSet<string> _said = [];

    public ExclusionReport(TextWriter writer)
    {
        _writer = writer;
    }

    public void Excluded(string prefix, Type target)
    {
        Say("exclude:" + prefix, $"NarrativeTrace: not tracing {prefix}.* (excluded; first seen on {target.FullName}).");
    }

    public void CannotReach(Type serviceType, Exception reason)
    {
        Say("reach:" + serviceType.FullName, $"NarrativeTrace: not tracing {serviceType.FullName}: it cannot be proxied ({reason.Message}).");
    }

    private void Say(string key, string line)
    {
        lock (_said)
        {
            if (_said.Add(key))
            {
                _writer.WriteLine(line);
            }
        }
    }
}
