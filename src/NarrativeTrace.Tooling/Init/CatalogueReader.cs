// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;

namespace NarrativeTrace.Tooling.Init;

/// <summary>
/// Reads a carrier's <c>catalogue.json</c> into a <see cref="SkillCatalogue"/>.
/// </summary>
/// <remarks>
/// INTENT: one place knows the catalogue's shape. Every field is mandatory and every type is checked
/// here, so a malformed carrier is refused at open time with a message naming the entry — never
/// half-installed. The catalogue carries no version by design; the stamp is the carrier's own
/// coordinate, which the carrier derives separately. Parsing is the in-box
/// <see cref="System.Text.Json"/> (this is the one place this port diverges from Java's hand-rolled
/// reader — net10.0 ships a JSON reader, so a hand-rolled one would be dead weight and the "no
/// package references" rule still holds).
/// </remarks>
internal static class CatalogueReader
{
    /// <param name="json">The whole <c>catalogue.json</c> text.</param>
    /// <exception cref="ArgumentException">
    /// When the document is malformed, a field is missing or wrongly typed, or a skill name repeats.
    /// </exception>
    internal static SkillCatalogue Read(string json)
    {
        using var document = Parse(json);
        var root = AsObject(document.RootElement, "catalogue");
        var runtime = Str(root, "runtime", "catalogue");
        var skills = new List<SkillEntry>();
        foreach (var element in Array(root, "skills"))
        {
            skills.Add(Entry(AsObject(element, "skill")));
        }

        return new SkillCatalogue(runtime, skills);
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("catalogue: malformed JSON: " + exception.Message, exception);
        }
    }

    private static SkillEntry Entry(JsonElement skill)
    {
        var name = Str(skill, "name", "skill");
        return new SkillEntry(
            name,
            Str(skill, "description", name),
            Str(skill, "agents", name),
            Str(skill, "claude", name));
    }

    private static JsonElement AsObject(JsonElement value, string what)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("catalogue: " + what + " must be a JSON object");
        }

        return value;
    }

    private static List<JsonElement> Array(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("catalogue: \"" + key + "\" must be a JSON array");
        }

        return value.EnumerateArray().ToList();
    }

    private static string Str(JsonElement owner, string key, string what)
    {
        if (!owner.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new ArgumentException("catalogue: " + what + " has no \"" + key + "\" string field");
        }

        return value.GetString()!;
    }
}
