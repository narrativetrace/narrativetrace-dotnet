// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>config.output-env</c>: mirrors <c>ConfigResolver</c>'s own lenience —
/// only <c>"false"</c>/<c>"0"</c> (case-insensitive) disable output; every
/// other value, including a typo someone intended as "off", is read as
/// enabled. A doctor check exists precisely because that silent lenience is
/// the trap: a misspelled value never fails loudly on its own.
/// </summary>
public static class ConfigOutputEnvCheck
{
    private const string Id = "config.output-env";
    private const string Key = "NARRATIVETRACE_OUTPUT";
    private static readonly string[] Recognized = ["true", "false", "1", "0"];

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        if (!snapshot.Env.TryGetValue(Key, out var raw) || raw is null)
        {
            return DoctorFinding.Pass(
                Id, $"{Key} is not set — output stays on its default (on)",
                DoctorDocUrls.ConfigurationEnvVars);
        }

        var trimmed = raw.Trim();
        return Recognized.Contains(trimmed, StringComparer.OrdinalIgnoreCase)
            ? DoctorFinding.Pass(Id, $"{Key}={raw}", DoctorDocUrls.ConfigurationEnvVars)
            : DoctorFinding.Fail(
                Id,
                $"{Key} is set to '{raw}', which is not \"true\"/\"false\"/\"1\"/\"0\"",
                $"Only \"false\" or \"0\" (case-insensitive) actually disable output — '{raw}' is " +
                "read as enabled. Set it to \"false\" to disable, or remove it to use the default.",
                DoctorDocUrls.ConfigurationEnvVars);
    }
}
