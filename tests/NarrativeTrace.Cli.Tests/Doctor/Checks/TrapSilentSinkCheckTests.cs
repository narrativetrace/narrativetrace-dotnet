// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class TrapSilentSinkCheckTests
{
    [Fact]
    public void Passes_when_tracing_is_not_wired_up()
    {
        var finding = TrapSilentSinkCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_a_consumer_is_attached()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs",
             "NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context); " +
             "IndentedTextRenderer.Render(context.CaptureTrace());"));

        var finding = TrapSilentSinkCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_wired_up_with_no_sink()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);"));

        var finding = TrapSilentSinkCheck.Run(snapshot);

        Assert.False(finding.Passed);
    }
}
