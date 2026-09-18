// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
namespace NarrativeTrace.Clarity;

/// <summary>
/// The three built-in word lists <see cref="GenericTokenDetector"/> classifies tokens against.
/// Vocabulary data, not logic — extracted into its own file so Stryker's <c>mutate</c> filter can
/// exclude exactly this file (see <c>stryker-config.clarity.json</c>'s excluded-paths reasons)
/// while <see cref="GenericTokenDetector"/>'s own classification methods (<c>Classify</c>,
/// <c>Score</c>, <c>IsMeaninglessSingleLetter</c>) stay in the mutated set.
/// </summary>
internal static class GenericTokenVocabulary
{
    internal static readonly HashSet<string> MeaninglessPlaceholders =
        ["foo", "bar", "baz", "qux", "quux", "temp", "tmp", "test",
         "dummy", "sample", "example", "xxx", "yyy", "zzz", "todo", "fixme"];

    internal static readonly HashSet<string> VagueWords =
        ["data", "info", "object", "thing", "item", "element", "stuff",
         "result", "response", "output", "input", "value", "content",
         "payload", "resource", "record", "entry", "detail", "details",
         "entity", "bean", "model", "wrapper", "holder", "container",
         "bundle", "batch", "chunk", "block", "piece", "part", "unit",
         "instance", "param", "argument", "body", "obj", "val", "arg",
         "meta", "metadata", "blob", "document", "artifact", "messagebody",
         "dataset", "modeloutput", "modelinput"];

    internal static readonly HashSet<string> TypedGenericWords =
        ["id", "name", "type", "status", "state", "count", "size", "length",
         "index", "key", "flag", "code", "text", "message", "label",
         "number", "amount", "total", "level", "mode", "kind", "category",
         "group", "list", "map", "set", "queue", "stack", "array",
         "collection", "table", "row", "column", "field", "property", "tag",
         "version", "timestamp", "date", "time", "duration", "interval",
         "timeout", "limit", "offset", "page", "sort", "order", "direction",
         "position", "priority", "weight", "rank", "score", "rating",
         "percentage", "ratio", "factor", "coefficient", "path", "url",
         "uri", "host", "port", "endpoint", "topic", "channel", "session",
         "token", "trace", "metric", "tenant"];
}
