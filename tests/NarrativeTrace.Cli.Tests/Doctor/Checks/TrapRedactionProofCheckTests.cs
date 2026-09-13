// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class TrapRedactionProofCheckTests
{
    [Fact]
    public void Fails_when_no_test_asserts_the_marker()
    {
        var finding = TrapRedactionProofCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.False(finding.Passed);
    }

    [Fact]
    public void Fails_when_the_marker_appears_outside_a_test_file()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("Program.cs", "Console.WriteLine(\"[REDACTED]\");"));

        var finding = TrapRedactionProofCheck.Run(snapshot);

        Assert.False(finding.Passed);
    }

    [Fact]
    public void Passes_when_a_test_file_asserts_the_marker()
    {
        var snapshot = DoctorSnapshotFixtures.WithSource(
            ("OrderServiceTests.cs", "Assert.Contains(\"[REDACTED]\", rendered);"));

        var finding = TrapRedactionProofCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }
}
