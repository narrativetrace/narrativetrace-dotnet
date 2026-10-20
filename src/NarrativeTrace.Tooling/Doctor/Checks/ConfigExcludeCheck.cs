// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Tooling.Doctor.Checks;

/// <summary>
/// <c>config.exclude</c>: reports the effective exclude list for auto-wrapped tracing — the shipped
/// default, or the whole list <c>NARRATIVETRACE_EXCLUDE</c> replaces it with — and fails on a prefix
/// that can never match a namespace. The variable's separator is <c>;</c>; a comma or slash is the
/// silent trap, because the prefix is then one odd name that excludes nothing.
/// </summary>
public static class ConfigExcludeCheck
{
    private const string Id = "config.exclude";
    private const string Key = "NARRATIVETRACE_EXCLUDE";
    private const string ResourceName = "NarrativeTrace.Tooling.default-exclude.txt";

    /// <summary>The default exclude list the library ships, read from the file both assemblies embed.</summary>
    internal static IReadOnlyList<string> ShippedDefault { get; } = ReadDefault();

    /// <summary>Runs the check.</summary>
    public static DoctorFinding Run(DoctorSnapshot snapshot)
    {
        if (!snapshot.Env.TryGetValue(Key, out var raw) || raw is null)
        {
            return DoctorFinding.Pass(
                Id,
                $"{Key} is not set — the shipped default list applies: {string.Join("; ", ShippedDefault)}",
                DoctorDocUrls.InstrumentationExclusions);
        }

        var prefixes = Normalise(raw.Split(';'));
        var bad = prefixes.FirstOrDefault(CannotMatchANamespace);
        return bad is not null ? Fail(raw, bad) : Pass(prefixes);
    }

    private static DoctorFinding Pass(IReadOnlyList<string> prefixes)
    {
        var message = prefixes.Count == 0
            ? $"{Key} is set and empty — nothing is excluded (the shipped default is switched off)"
            : $"{Key} replaces the shipped default; effective list: {string.Join("; ", prefixes)}";
        return DoctorFinding.Pass(Id, message, DoctorDocUrls.InstrumentationExclusions);
    }

    private static DoctorFinding Fail(string raw, string bad)
    {
        return DoctorFinding.Fail(
            Id,
            $"{Key}='{raw}' contains '{bad}', which cannot match a namespace",
            "Separate prefixes with \";\" (not a comma, slash or space) and write each as a dotted " +
            "namespace such as Acme.Legacy. The value replaces the shipped default, so repeat any default " +
            "prefix you still want.",
            DoctorDocUrls.InstrumentationExclusions);
    }

    private static bool CannotMatchANamespace(string prefix)
    {
        return prefix.Any(c => c is ',' or '/' or '\\' || char.IsWhiteSpace(c));
    }

    private static List<string> Normalise(IEnumerable<string> raw)
    {
        return raw
            .Select(p => p.Trim())
            .Select(p => p.EndsWith(".*", StringComparison.Ordinal) ? p[..^2] : p)
            .Select(p => p.TrimEnd('.'))
            .Where(p => p.Length > 0)
            .ToList();
    }

    private static IReadOnlyList<string> ReadDefault()
    {
        using var stream = typeof(ConfigExcludeCheck).Assembly.GetManifestResourceStream(ResourceName)!;
        using var reader = new StreamReader(stream);
        return Normalise(reader.ReadToEnd()
            .Split('\n')
            .Where(line => line.Length > 0 && line[0] != '#')
            .Select(line => line.Split('\t')[0]));
    }
}
