// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text;
using System.Text.Json;

namespace NarrativeTrace.Clarity;

/// <summary>
/// Merges the per-test single-scenario clarity objects emitted at runtime (the
/// <c>*.clarity.json</c> artifacts) into one Java-compatible
/// <c>{"version","scenarios":[…]}</c> envelope so the build gate can score real
/// captured traces instead of the static reflection scan. See CLARITY-13.
/// </summary>
public static class ClarityResultsAggregator
{
    private static readonly JsonWriterOptions WriterOptions =
        new() { Indented = true };

    /// <summary>Wraps each scenario JSON object into the versioned envelope, preserving order.</summary>
    public static string Aggregate(IEnumerable<string> scenarioJsonObjects)
    {
        var documents = new List<JsonDocument>();
        try
        {
            foreach (var json in scenarioJsonObjects)
            {
                documents.Add(JsonDocument.Parse(json));
            }

            return WriteEnvelope(documents);
        }
        finally
        {
            for (var i = 0; i < documents.Count; i++)
            {
                documents[i].Dispose();
            }
        }
    }

    private static string WriteEnvelope(List<JsonDocument> scenarios)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        writer.WriteStartObject();
        writer.WriteString("version", ClarityJsonExporter.Version);
        writer.WriteStartArray("scenarios");
        for (var i = 0; i < scenarios.Count; i++)
        {
            scenarios[i].RootElement.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
