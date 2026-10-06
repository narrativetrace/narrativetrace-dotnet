// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Cli.Tests.Init;

/// <summary>Writes an exploded directory carrier (two skills, both flavours) for the verb tests.</summary>
internal static class TestCarriers
{
    internal const string Doctor = "narrativetrace-doctor";
    internal const string AddTracing = "add-narrative-tracing";

    internal static string Write(string root, string? version = null)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "catalogue.json"), Catalogue());
        foreach (var flavour in new[] { "agents", "claude" })
        {
            foreach (var name in new[] { Doctor, AddTracing })
            {
                var directory = Path.Combine(root, flavour, name);
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "SKILL.md"), Page(flavour, name));
            }
        }

        if (version is not null)
        {
            File.WriteAllText(Path.Combine(root, "narrativetrace-version.properties"), "version=" + version + "\n");
        }

        return root;
    }

    private static string Page(string flavour, string name) =>
        "---\nname: " + name + "\ndescription: d\n---\n\n" + name + " " + flavour + "\n";

    private static string Catalogue() =>
        "{\"runtime\": \"dotnet\", \"skills\": [" + Skill(Doctor) + ", " + Skill(AddTracing) + "]}";

    private static string Skill(string name) =>
        "{\"name\": \"" + name + "\", \"description\": \"d\","
        + " \"agents\": \"agents/" + name + "/SKILL.md\","
        + " \"claude\": \"claude/" + name + "/SKILL.md\"}";
}
