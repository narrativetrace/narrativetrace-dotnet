// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using Xunit;

namespace NarrativeTrace.Core.Tests;

public class RunIdentityTests
{
    [Fact]
    public void Generate_produces_a_well_formed_id_and_a_matching_name()
    {
        var run = RunIdentity.Generate();

        Assert.False(run.Id.IsEmpty);
        Assert.Equal(TraceNamer.Name(run.Id.Value), run.Name);
    }

    [Fact]
    public void Generate_called_twice_names_two_different_runs()
    {
        var first = RunIdentity.Generate();
        var second = RunIdentity.Generate();

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public void Constructor_rejects_an_empty_trace_id()
    {
        Assert.Throws<ArgumentException>(() => new RunIdentity(TraceId.Empty));
    }

    [Fact]
    public void Name_is_always_derived_from_id_never_carried_separately()
    {
        var id = new TraceId("a1b2c3d4a1b2c3d4a1b2c3d4a1b2c3d4");
        var run = new RunIdentity(id);

        Assert.Equal(id.HumanName, run.Name);
    }
}
