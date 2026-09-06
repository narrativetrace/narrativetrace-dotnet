// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.TestingNUnit;

/// <summary>
/// Holds the <see cref="NarrativeSuiteReport"/> active for the current suite run.
/// A <see cref="NarrativeSuiteSetup"/> begins the scope in its one-time set-up and
/// flushes it in one-time tear-down; <see cref="NarrativeTestBase"/> records each
/// test's trace into the scope when one is active.
/// </summary>
internal static class NarrativeSuiteScope
{
    private static readonly object Lock = new();
    private static NarrativeSuiteReport? _current;

    internal static NarrativeSuiteReport? Current
    {
        get
        {
            lock (Lock)
            {
                return _current;
            }
        }
    }

    internal static void Begin(NarrativeSuiteReport report)
    {
        lock (Lock)
        {
            _current = report;
        }
    }

    internal static void End(TextWriter console)
    {
        NarrativeSuiteReport? report;
        lock (Lock)
        {
            report = _current;
            _current = null;
        }

        report?.Flush(console);
    }
}
