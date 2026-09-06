// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class ContextSnapshotExtensionsTests
{
    [Fact]
    public void Wrap_action_activates_around_task_then_restores()
    {
        var snap = new RecordingSnapshot();
        var ranWhileActive = false;

        // Block body forces the Action overload; an expression-bodied
        // assignment would bind to Wrap<T>(Func<T>) instead.
        var wrapped = snap.Wrap(() => { ranWhileActive = snap.Active; });

        Assert.False(snap.Active);
        wrapped();
        Assert.True(ranWhileActive);
        Assert.False(snap.Active);
    }

    [Fact]
    public void Wrap_func_returns_value_computed_inside_scope()
    {
        var snap = new RecordingSnapshot();

        var wrapped = snap.Wrap(() => snap.Active ? 42 : -1);

        Assert.Equal(42, wrapped());
        Assert.False(snap.Active);
    }

    private sealed class RecordingSnapshot
        : IContextSnapshot, IContextScope
    {
        public bool Active { get; private set; }

        public IContextScope Activate()
        {
            Active = true;
            return this;
        }

        public IContextScope ActivateWithoutAdoption() => Activate();

        public void Dispose() => Active = false;
    }
}
