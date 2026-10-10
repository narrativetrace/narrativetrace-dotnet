// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor.Checks;
using NarrativeTrace.Tooling.Frameworks;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor.Checks;

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

    /// <summary>
    /// The default-logger row of the framework table folds into this check: its fix carries that
    /// row's lines, verbatim from the compiled fixture.
    /// </summary>
    [Fact]
    public void The_fix_carries_the_default_logger_rows_package_and_snippet()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);"));

        var finding = TrapSilentSinkCheck.Run(snapshot);

        Assert.Contains("dotnet add package NarrativeTrace.Logging", finding.Fix, StringComparison.Ordinal);
        Assert.EndsWith(WiringSnippets.Text("default-logger"), finding.Fix, StringComparison.Ordinal);
    }

    [Fact]
    public void The_logger_package_line_pins_the_projects_own_family_version_when_known()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);")) with
        {
            ProjectResolvedFamilyVersion = "0.2.5",
        };

        Assert.Contains(
            "dotnet add package NarrativeTrace.Logging --version 0.2.5",
            TrapSilentSinkCheck.Run(snapshot).Fix,
            StringComparison.Ordinal);
    }

    /// <summary>Cross-port item 3: the fix the doctor prints must be the fix it accepts.</summary>
    [Fact]
    public void The_logger_rows_own_snippet_clears_the_finding_it_is_printed_for()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "NarrativeTraceProxy.Create<IOrderService>(new OrderService(), context);"),
            ("Logging.cs", WiringSnippets.Text("default-logger")));

        Assert.True(TrapSilentSinkCheck.Run(snapshot).Passed);
    }

    [Fact]
    public void The_hosted_logging_bridge_counts_as_a_sink()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "services.AddNarrativeTracing(o => o.Namespaces(\"A\")); services.AddNarrativeLogging();"));

        Assert.True(TrapSilentSinkCheck.Run(snapshot).Passed);
    }

    /// <summary>
    /// <c>AddNarrativeTrace</c> registers the middleware AND its default exporter
    /// (<c>LoggerTraceExporter</c>, under <c>NarrativeTrace.Export</c>): a web app wired with it alone
    /// already writes every request's trace to its logger.
    /// </summary>
    [Fact]
    public void The_aspnetcore_registration_brings_its_own_logger_exporter()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "builder.Services.AddNarrativeTrace(builder.Configuration); app.UseNarrativeTrace();"));

        Assert.True(TrapSilentSinkCheck.Run(snapshot).Passed);
    }
}
