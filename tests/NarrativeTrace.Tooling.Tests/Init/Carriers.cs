// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Tooling.Init;

namespace NarrativeTrace.Tooling.Tests.Init;

/// <summary>
/// Builds exploded directory carriers for the installer tests — the .NET equivalent of Java's
/// <c>Carriers</c> test helper, which builds jars. Two free skills, both flavours, each page carrying
/// YAML frontmatter so provenance stamping has somewhere to land.
/// </summary>
internal static class Carriers
{
    internal const string Doctor = "narrativetrace-doctor";
    internal const string AddTracing = "add-narrative-tracing";
    internal const string DoctorDescription = "Diagnoses an install.";
    internal const string AddTracingDescription = "Adds NarrativeTrace to a project.";

    /// <summary>Writes a carrier into <paramref name="root"/> and returns that path.</summary>
    internal static string Write(string root, string? version = null)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, Carrier.CatalogueFile), Catalogue());
        WritePage(root, "agents", Doctor, DoctorDescription);
        WritePage(root, "claude", Doctor, DoctorDescription);
        WritePage(root, "agents", AddTracing, AddTracingDescription);
        WritePage(root, "claude", AddTracing, AddTracingDescription);
        if (version is not null)
        {
            File.WriteAllText(
                Path.Combine(root, "narrativetrace-version.properties"), "version=" + version + "\n");
        }

        return root;
    }

    /// <summary>Writes a carrier and opens it in one step.</summary>
    internal static Carrier Open(string root, string? version = null) => Carrier.Open(Write(root, version));

    private static Carrier? _shared;

    /// <summary>
    /// A single carrier reused across the property tests. A carrier reads every page into memory at
    /// open time, so the temp directory it was written from is not needed afterwards.
    /// </summary>
    internal static Carrier Shared()
    {
        _shared ??= Open(
            Path.Combine(Path.GetTempPath(), "nt-shared-carrier-" + Guid.NewGuid().ToString("N")), "1.2.3");
        return _shared;
    }

    /// <summary>The rendered page a carrier carries for one skill and flavour, before provenance.</summary>
    internal static string PageBody(string flavour, string name, string description) =>
        "---\n"
        + "name: " + name + "\n"
        + "description: " + description + "\n"
        + "---\n\n"
        + "The " + name + " skill, " + flavour + " flavour.\n";

    private static void WritePage(string root, string flavour, string name, string description)
    {
        var directory = Path.Combine(root, flavour, name);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "SKILL.md"), PageBody(flavour, name, description));
    }

    private static string Catalogue() =>
        "{\"runtime\": \"dotnet\", \"skills\": ["
        + Skill(Doctor, DoctorDescription) + ", "
        + Skill(AddTracing, AddTracingDescription)
        + "]}";

    private static string Skill(string name, string description) =>
        "{\"name\": \"" + name + "\", \"description\": \"" + description + "\","
        + " \"agents\": \"agents/" + name + "/SKILL.md\","
        + " \"claude\": \"claude/" + name + "/SKILL.md\"}";
}
