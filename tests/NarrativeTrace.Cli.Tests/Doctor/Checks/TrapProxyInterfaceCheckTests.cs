// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class TrapProxyInterfaceCheckTests
{
    [Fact]
    public void Passes_when_no_create_calls_exist()
    {
        var finding = TrapProxyInterfaceCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_the_traced_type_is_an_interface()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs",
             "public interface IOrderService {} " +
             "NarrativeTraceProxy.Create<IOrderService>(target, context);"));

        var finding = TrapProxyInterfaceCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_the_traced_type_cannot_be_resolved_locally()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "NarrativeTraceProxy.Create<IExternalService>(target, context);"));

        var finding = TrapProxyInterfaceCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_the_traced_type_is_a_concrete_class()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs",
             "public class OrderService {} " +
             "NarrativeTraceProxy.Create<OrderService>(target, context);"));

        var finding = TrapProxyInterfaceCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("OrderService", finding.Message, StringComparison.Ordinal);
        Assert.Contains("interface", finding.Fix, StringComparison.Ordinal);
    }
}
