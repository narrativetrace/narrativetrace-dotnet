// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json.Nodes;
using Json.Schema;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Validates exporter output against the canonical NarrativeTrace JSON
/// schemas (draft 2020-12) copied verbatim from the cross-language schema
/// set. Guards against silent drift between the emitted wire format and the
/// interoperability contract shared with the Java implementation.
/// </summary>
internal static class SchemaValidator
{
    private static readonly string SchemaDir =
        Path.Combine(AppContext.BaseDirectory, "Schemas");

    private static readonly EvaluationOptions Options =
        new() { OutputFormat = Json.Schema.OutputFormat.List };

    public static EvaluationResults Validate(string schemaFile, string json)
    {
        var schema = JsonSchema.FromFile(
            Path.Combine(SchemaDir, schemaFile));
        var node = JsonNode.Parse(json);
        return schema.Evaluate(node, Options);
    }

    public static void AssertValid(string schemaFile, string json)
    {
        var results = Validate(schemaFile, json);
        Assert.True(results.IsValid, Describe(results, json));
    }

    private static string Describe(EvaluationResults results, string json)
    {
        var errors = results.Details
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!
                .Select(e =>
                    $"  {d.InstanceLocation}: {e.Key} => {e.Value}"));
        return "Schema validation failed:\n"
            + string.Join("\n", errors) + "\n---\n" + json;
    }
}
