// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using NarrativeTrace.Core;
using NarrativeTrace.Runtime;
using System.Text.Json;
using Xunit;

namespace NarrativeTrace.Core.Tests;

/// <summary>
/// Pins every canonical <c>nt.schemaVersion</c> stamp to one constant. The
/// stamps were independently hard-coded string literals across six files, so a
/// version bump could land in some artifacts and not others — a divergence no
/// schema check would catch, because each artifact would still be valid.
/// </summary>
public class CanonicalSchemaTests
{
    [Fact]
    public void Canonical_entry_stamps_the_shared_schema_version()
    {
        Assert.Equal(
            CanonicalSchema.Version,
            new CanonicalEntry(
                Timestamp: "2026-08-28T00:00:00Z",
                Level: "INFO",
                Message: "m",
                Service: null,
                Environment: null,
                TraceId: null,
                SpanId: null,
                ParentSpanId: null,
                CodeNamespace: null,
                CodeFunction: null,
                NtEventType: "enter").NtSchemaVersion);
    }

    [Fact]
    public void Chapter_export_stamps_the_shared_schema_version()
    {
        var tree = new TraceTree([
            new TraceNode(
                new MethodSignature("Svc", "Run", []),
                new Returned(null), [], 0),
        ]);

        var json = new ChapterExporter()
            .ExportChapter(tree, new TraceMetadata("s", ScenarioResult.Success));
        using var doc = JsonDocument.Parse(json);

        Assert.Equal(
            CanonicalSchema.Version,
            doc.RootElement.GetProperty("nt.schemaVersion").GetString());
    }

    [Fact]
    public void Version_is_a_dotted_numeric_version()
    {
        // The schema constrains it; a stray value would fail validation only
        // where a schema is actually applied, which is not everywhere it is
        // stamped.
        Assert.Matches(@"^\d+\.\d+$", CanonicalSchema.Version);
    }
}
