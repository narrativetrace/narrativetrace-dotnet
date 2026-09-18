// SPDX-License-Identifier: BUSL-1.1
// Licensed under the Business Source License 1.1 (see LICENSE); Change Date: four years from publication; Change License: Apache-2.0
// Copyright (c) 2026 Empower Agile
using System.Text.Json;

namespace NarrativeTrace.SecurityTests;

/// <summary>
/// Reads a hostile-corpus file's <c>cases</c> array as raw JSON text keyed by <c>id</c> — the
/// byte-identity unit every master-sync gate (<see cref="HostileCorpusMasterSyncTests"/>,
/// <see cref="RedactionCorpusMasterSyncTests"/>, …) compares a local mirror row against.
/// </summary>
/// <remarks>
/// <see cref="JsonElement.GetRawText"/> reflects the source exactly — whitespace, key order, and
/// escape spelling included — so a formatting drift fails the gate the same as a content drift.
/// </remarks>
internal static class CorpusFileRows
{
    internal static IReadOnlyDictionary<string, string> ById(string path) =>
        ById(File.ReadAllBytes(path));

    /// <summary>
    /// Same as <see cref="ById(string)"/>, from bytes already in hand — the master-sync gates read
    /// the master file's committed <c>HEAD</c> content (<see cref="MasterCorpusPath"/>), which
    /// never lives at a working-tree path this method could take instead.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> ById(byte[] content)
    {
        using var document = JsonDocument.Parse(content);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = element.GetProperty("id").GetString()!;
            result[id] = element.GetRawText();
        }

        return result;
    }
}
