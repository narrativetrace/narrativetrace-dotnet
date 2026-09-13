// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Cli.Doctor;
using NarrativeTrace.Cli.Doctor.Checks;
using Xunit;

namespace NarrativeTrace.Cli.Tests.Doctor.Checks;

public sealed class ConfigAppSettingsKeysCheckTests
{
    [Fact]
    public void Passes_when_no_appsettings_files_exist()
    {
        var finding = ConfigAppSettingsKeysCheck.Run(DoctorSnapshotFixtures.Empty());

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_section_absent()
    {
        var snapshot = WithAppSettings("""{"Logging":{}}""");

        var finding = ConfigAppSettingsKeysCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Passes_when_all_keys_are_recognized()
    {
        var snapshot = WithAppSettings("""{"NarrativeTrace":{"Level":"Detail","LoggerName":"x"}}""");

        var finding = ConfigAppSettingsKeysCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    [Fact]
    public void Fails_when_a_key_is_misspelled()
    {
        var snapshot = WithAppSettings("""{"NarrativeTrace":{"Leve":"Detail"}}""");

        var finding = ConfigAppSettingsKeysCheck.Run(snapshot);

        Assert.False(finding.Passed);
        Assert.Contains("Leve", finding.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Passes_when_json_is_malformed()
    {
        var snapshot = WithAppSettings("{not valid json");

        var finding = ConfigAppSettingsKeysCheck.Run(snapshot);

        Assert.True(finding.Passed);
    }

    private static DoctorSnapshot WithAppSettings(string json)
    {
        return DoctorSnapshotFixtures.Empty() with
        {
            AppSettingsFiles = new Dictionary<string, string> { ["appsettings.json"] = json },
        };
    }
}
