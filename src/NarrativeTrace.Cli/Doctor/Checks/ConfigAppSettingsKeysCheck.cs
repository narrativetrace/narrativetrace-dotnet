// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;

namespace NarrativeTrace.Cli.Doctor.Checks;

/// <summary>
/// <c>config.appsettings-keys</c>: <c>NarrativeTraceOptions</c> binds from
/// the <c>NarrativeTrace</c> configuration section with the standard
/// <c>IConfiguration</c> binder, which silently ignores a key with no
/// matching property. A misspelled key in <c>appsettings.json</c> never
/// throws — it just quietly does nothing.
/// </summary>
public static class ConfigAppSettingsKeysCheck
{
    private const string Id = "config.appsettings-keys";
    private const string SectionName = "NarrativeTrace";

    private static readonly HashSet<string> KnownKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Level", "LoggerName", "ServiceIdentity", "ExcludedPaths", "Redaction",
    };

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        foreach (var (path, content) in snapshot.AppSettingsFiles)
        {
            var unknown = UnknownKeys(content);
            if (unknown.Count > 0)
            {
                return Fail(path, unknown);
            }
        }

        return DoctorFinding.Pass(
            Id, "no unrecognized keys under the NarrativeTrace configuration section",
            DoctorDocUrls.AspNetCoreRegistration);
    }

    private static List<string> UnknownKeys(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty(SectionName, out var section)
                || section.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return section.EnumerateObject()
                .Select(p => p.Name)
                .Where(name => !KnownKeys.Contains(name))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static DoctorFinding Fail(string path, List<string> unknown)
    {
        return DoctorFinding.Fail(
            Id,
            $"{path} has unrecognized key(s) under NarrativeTrace: {string.Join(", ", unknown)}",
            "IConfiguration binding ignores unknown keys silently — check for a typo against " +
            "Level, LoggerName, ServiceIdentity, ExcludedPaths, Redaction.",
            DoctorDocUrls.AspNetCoreRegistration);
    }
}
