// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NarrativeTrace.Skills.Render;

/// <summary>
/// Renders <c>catalogue.json</c>, the carrier's index (design D2): <c>{runtime, skills[{name,
/// description, agents, claude}]}</c>, byte-identical in shape to the Java carrier's own
/// <c>catalogue.json</c>. Carries no version literal on purpose — the carrier's own coordinate
/// (the package or embedding assembly's version) is the stamp; a version baked into this file
/// would have to be rewritten on every release and could drift stale.
/// </summary>
public static class CatalogueJsonRenderer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Renders <c>catalogue.json</c> for <paramref name="skills"/>, in catalogue order.</summary>
    public static string Render(IReadOnlyList<Skill> skills)
    {
        var document = new CatalogueDocument(
            "dotnet",
            skills.Select(ToEntry).ToList());
        var json = JsonSerializer.Serialize(document, Options).Replace("\r\n", "\n", StringComparison.Ordinal);
        return json + "\n";
    }

    private static SkillEntryDocument ToEntry(Skill skill) => new(
        skill.CanonicalName,
        skill.Description,
        $"agents/{skill.CanonicalName}/SKILL.md",
        $"claude/{skill.CanonicalName}/SKILL.md");

    private sealed record CatalogueDocument(
        [property: JsonPropertyName("runtime")] string Runtime,
        [property: JsonPropertyName("skills")] IReadOnlyList<SkillEntryDocument> Skills);

    private sealed record SkillEntryDocument(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("description")] string Description,
        [property: JsonPropertyName("agents")] string Agents,
        [property: JsonPropertyName("claude")] string Claude);
}
