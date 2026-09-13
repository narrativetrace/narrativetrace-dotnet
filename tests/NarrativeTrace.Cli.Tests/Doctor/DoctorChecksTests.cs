// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor;

public sealed class DoctorChecksTests
{
    [Fact]
    public void Runs_every_registered_check_exactly_once()
    {
        var report = DoctorChecks.Run(DoctorSnapshotFixtures.Empty());

        Assert.Equal(DoctorChecks.All.Count, report.Findings.Count);
    }

    [Fact]
    public void Every_check_id_is_unique()
    {
        var report = DoctorChecks.Run(DoctorSnapshotFixtures.Empty());

        Assert.Equal(report.Findings.Count, report.Findings.Select(f => f.Id).Distinct().Count());
    }

    [Fact]
    public void Every_finding_carries_a_message_and_a_doc_url()
    {
        var report = DoctorChecks.Run(DoctorSnapshotFixtures.Empty());

        Assert.All(report.Findings, f =>
        {
            Assert.NotEmpty(f.Message);
            Assert.StartsWith("https://", f.DocUrl, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Fix_is_empty_if_and_only_if_the_finding_passed()
    {
        var report = DoctorChecks.Run(DoctorSnapshotFixtures.Empty());

        Assert.All(report.Findings, f => Assert.Equal(f.Passed, f.Fix.Length == 0));
    }

    [Fact]
    public void Exit_code_is_zero_when_every_check_passes()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("OrderServiceTests.cs", "Assert.Contains(\"[REDACTED]\", rendered);"));

        var report = DoctorChecks.Run(snapshot);

        Assert.Equal(0, report.ExitCode);
        Assert.All(report.Findings, f => Assert.True(f.Passed));
    }

    [Fact]
    public void Exit_code_is_one_when_any_check_fails()
    {
        var report = DoctorChecks.Run(DoctorSnapshotFixtures.Empty());

        Assert.Equal(1, report.ExitCode);
    }
}
