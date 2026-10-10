// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Feedback;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Feedback;

/// <summary>
/// The gh probe's DECISION — which of the four outcomes mean "signed in" — over an injected result,
/// so every outcome is exercised and no process is ever started in a test.
/// </summary>
public sealed class GhAuthProbeTests
{
    [Fact]
    public void A_clean_exit_zero_means_signed_in()
    {
        Assert.True(GhAuthProbe.Authenticated(() => new GhProbeResult(Exited: true, ExitCode: 0)));
    }

    [Fact]
    public void A_non_zero_exit_means_not_signed_in()
    {
        Assert.False(GhAuthProbe.Authenticated(() => new GhProbeResult(Exited: true, ExitCode: 1)));
    }

    [Fact]
    public void A_timeout_means_not_available()
    {
        Assert.False(GhAuthProbe.Authenticated(() => new GhProbeResult(Exited: false, ExitCode: 0)));
    }

    [Fact]
    public void An_absent_gh_that_cannot_start_means_not_available()
    {
        Assert.False(GhAuthProbe.Authenticated(
            () => throw new System.ComponentModel.Win32Exception("gh: command not found")));
    }
}
