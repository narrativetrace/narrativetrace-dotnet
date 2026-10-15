// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Doctor;
using NarrativeTrace.Tooling.Init;
using Xunit;

namespace NarrativeTrace.Tooling.Tests.Doctor;

/// <summary>The doctor's snapshot carries the installer's reading of each detected platform's pointer.</summary>
public sealed class DoctorSnapshotPointerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "nt-doctor-ptr-" + Guid.NewGuid().ToString("N"));

    public DoctorSnapshotPointerTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void A_project_with_a_cursor_directory_and_no_rule_reads_the_pointer_as_absent()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".cursor"));

        var reading = Assert.Single(DoctorSnapshotBuilder.Build(_root, _ => null).PointerReadings);

        Assert.Equal("cursor", reading.PlatformId);
        Assert.Equal(PointerReading.PointerStatus.Absent, reading.Status);
    }

    [Fact]
    public void A_project_on_no_platform_reads_no_pointers()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".cursorx"));

        Assert.Empty(DoctorSnapshotBuilder.Build(_root, _ => null).PointerReadings);
    }

    [Fact]
    public void A_directory_that_does_not_exist_reads_no_pointers_and_does_not_throw()
    {
        Assert.Empty(DoctorSnapshotBuilder.Build(Path.Combine(_root, "missing"), _ => null).PointerReadings);
    }
}
